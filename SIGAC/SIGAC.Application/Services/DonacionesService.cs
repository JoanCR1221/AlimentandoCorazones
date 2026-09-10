using SIGAC.Application.DTOs.Donaciones;
using SIGAC.Application.DTOs.Inventario;
using SIGAC.Application.Exceptions;
using SIGAC.Application.Interfaces;
using SIGAC.Application.Validators;
using SIGAC.Domain;
using SIGAC.Domain.Entities;

namespace SIGAC.Application.Services
{
    // Orquesta el registro y la consulta de donaciones, recibidas y entregadas.
    //
    // REGLA DE ARQUITECTURA: Inventario es el dueño del stock. Este servicio no
    // conoce IInventarioRepository ni toca StockActual: cuando una donación mueve
    // existencias, delega en IInventarioService. Esa frontera es lo que protege la
    // atomicidad que ya está implementada del otro lado (insertar el movimiento y
    // ajustar el stock en una sola transacción, con el descuento resuelto dentro
    // del WHERE del UPDATE). Saltarla para "ahorrarse una llamada" reabriría
    // exactamente los bugs de stock desfasado que ese diseño cerró.
    public class DonacionesService : IDonacionesService
    {
        // Etiquetas del historial unificado. No son columnas de ninguna tabla: son
        // el discriminador que distingue de cuál de las dos consultas salió cada
        // fila, y también lo que llega en FiltrosHistorialDonacionDto.TipoDonacion.
        //
        // Van acá como constantes públicas y no en un catálogo cerrado de Domain
        // (que sería su lugar natural, junto a TiposPersonaDonante y compañía)
        // para no crear un tipo que no estaba en el alcance de este paso. Vale la
        // pena moverlas a un TiposDonacion cuando se arme la pantalla.
        public const string TipoDonacionDinero = "Dinero";
        public const string TipoDonacionEspecie = "Especie";

        // Longitud de SalidaInventario.ComunidadDestinataria: el texto que se le
        // manda a Inventario como destino se recorta a esta medida.
        private const int LongitudMaximaDestinoSalida = 150;

        private readonly IDonacionesRepository _repository;
        private readonly IDonantesRepository _donantesRepository;

        // Se inyecta el SERVICIO de inventario, no su repositorio: ver la regla de
        // arquitectura del encabezado de la clase.
        private readonly IInventarioService _inventarioService;

        // Acá sí el REPOSITORIO y no IBeneficiariosService, al revés que con
        // Inventario. El criterio es qué protege cada servicio:
        //
        //  - IInventarioService custodia un invariante transaccional (el stock y su
        //    movimiento se escriben juntos o no se escriben). Entrar por su
        //    repositorio permitiría escribir uno sin el otro, así que la frontera
        //    tiene que respetarse.
        //  - IBeneficiariosService no custodia ningún invariante sobre la lectura de
        //    un beneficiario: acá solo se necesita comprobar que existe y está
        //    activo, que es una lectura sin regla de negocio detrás.
        //
        // Y hay una razón práctica: IBeneficiariosService no expone Estado por
        // ningún método (ObtenerParaEditarAsync devuelve un DTO que no lo incluye),
        // así que literalmente no puede responder la pregunta. Agregárselo sería
        // modificar el módulo de Beneficiarios, que está fuera del alcance de este
        // paso.
        private readonly IBeneficiariosRepository _beneficiariosRepository;

        public DonacionesService(
            IDonacionesRepository repository,
            IDonantesRepository donantesRepository,
            IInventarioService inventarioService,
            IBeneficiariosRepository beneficiariosRepository)
        {
            _repository = repository;
            _donantesRepository = donantesRepository;
            _inventarioService = inventarioService;
            _beneficiariosRepository = beneficiariosRepository;
        }

        // ------------------------------------------------------------------
        // Donaciones recibidas: dinero
        // ------------------------------------------------------------------

        public async Task RegistrarDonacionDineroAsync(DonacionDineroCrearDto dto)
        {
            try
            {
                if (dto.Monto <= 0)
                    throw new ValidationException("El monto debe ser mayor a 0.");

                // Misma regla que RegistrarEntradaAsync de Inventario: la donación se
                // registra el día en que ocurre, una fecha futura no representa nada
                // recibido todavía.
                ValidarFechaNoFutura(dto.Fecha, "la donación");

                var donanteId = await ResolverDonanteAsync(dto.DonanteId, dto.NuevoDonante);

                var donacion = new DonacionDinero
                {
                    DonanteId = donanteId,
                    Monto = dto.Monto,
                    Fecha = dto.Fecha,
                    Observaciones = dto.Observaciones
                };

                // Una sola escritura y nada de inventario: el dinero no es un
                // artículo con stock, así que esta donación no genera movimiento.
                await _repository.AgregarDonacionDineroAsync(donacion);
            }
            catch (Exception ex) when (ex is not ValidationException and not NotFoundException and not DuplicateException)
            {
                throw new Exception("Error al registrar la donación en dinero.", ex);
            }
        }

        // ------------------------------------------------------------------
        // Donaciones recibidas: especie
        // ------------------------------------------------------------------

        public async Task RegistrarDonacionEspecieAsync(DonacionEspecieCrearDto dto)
        {
            try
            {
                if (dto.Articulos is null || dto.Articulos.Count == 0)
                    throw new ValidationException("Debe agregar al menos un artículo a la donación.");

                ValidarFechaNoFutura(dto.Fecha, "la donación");

                // TODAS las líneas se validan ANTES de escribir nada. Es lo que hace
                // que el reparto en dos fases de más abajo sea tolerable: si la línea
                // 3 de 5 tiene una categoría inválida, se rechaza la donación entera
                // sin haber guardado ni movido stock. Validando dentro del bucle, esa
                // misma línea explotaría con las dos primeras ya ingresadas.
                var lineas = dto.Articulos.Select(ValidarLinea).ToList();

                var donanteId = await ResolverDonanteAsync(dto.DonanteId, dto.NuevoDonante);

                var donacion = new DonacionEspecie
                {
                    DonanteId = donanteId,
                    Fecha = dto.Fecha,
                    Observaciones = dto.Observaciones,
                    Detalles = lineas.Select(l => new DetalleDonacionEspecie
                    {
                        NombreArticulo = l.NombreArticulo,
                        Cantidad = l.Cantidad,
                        Categoria = l.Categoria,
                        UnidadMedida = l.UnidadMedida
                    }).ToList()
                };

                // FASE 1: se guarda la donación con su detalle. Una sola operación
                // atómica del lado del repositorio (cabecera y líneas en el mismo
                // SaveChanges).
                await _repository.AgregarDonacionEspecieAsync(donacion);

                // FASE 2: cada línea entra al stock como EntradaInventario con origen
                // "Donacion", delegando en Inventario.
                //
                // Las dos fases NO comparten transacción y no pueden compartirla sin
                // romper la frontera entre módulos: cada repositorio pide su propio
                // DbContext a la factory, así que son conexiones distintas. Ver el
                // análisis del bloque de comentarios de RegistrarEntradasEnInventarioAsync.
                await RegistrarEntradasEnInventarioAsync(donacion, lineas);
            }
            catch (Exception ex) when (ex is not ValidationException and not NotFoundException and not DuplicateException)
            {
                throw new Exception("Error al registrar la donación en especie.", ex);
            }
        }

        // Qué pasa si la donación se guardó y falla el ingreso del tercero de cinco
        // artículos:
        //
        // Queda una inconsistencia real. La donación figura completa en el historial
        // (cabecera y cinco líneas) pero el inventario solo tiene dos: el stock queda
        // SUBVALUADO respecto de lo que físicamente entró a la bodega.
        //
        // No se puede evitar del todo desde acá. Una transacción que abarque las dos
        // fases exigiría un contexto compartido entre IDonacionesRepository e
        // IInventarioRepository, o un método de Inventario que reciba el lote entero
        // y lo resuelva todo-o-nada. Las dos cosas son cambios en el módulo de
        // Inventario, fuera del alcance de este paso, y la primera además disolvería
        // la frontera que protege su atomicidad.
        //
        // Lo que sí se hace:
        //
        //  1. Validar todo antes de escribir (arriba), para que la causa más probable
        //     de falla a mitad de camino —datos malos— no exista. Lo que queda son
        //     fallas de infraestructura (base caída) o la carrera de que otro usuario
        //     cree el mismo artículo nuevo en el medio.
        //  2. Elegir el ORDEN que deja el daño menos grave. Guardar primero la
        //     donación deja el stock subvaluado, que es la dirección segura: el
        //     sistema nunca promete existencias que no tiene, y el comprobante de lo
        //     que el donante entregó —que es el documento que hay que poder mostrar—
        //     queda guardado. Al revés (inventario primero) una falla dejaría stock
        //     sumado sin ninguna donación que lo respalde.
        //  3. Fallar con el detalle EXACTO de qué entró y qué no, para que se pueda
        //     completar a mano desde la pantalla de entradas de Inventario. Sin ese
        //     detalle la inconsistencia sería invisible y nadie la corregiría.
        //
        // El trade-off aceptado: se cambia atomicidad por independencia entre
        // módulos, y se compensa con un mensaje que convierte el problema en una
        // tarea manual concreta en vez de un dato silenciosamente mal. La solución
        // definitiva es un método de lote en IInventarioService que registre las N
        // entradas en una sola transacción; queda como trabajo pendiente de
        // Inventario.
        private async Task RegistrarEntradasEnInventarioAsync(
            DonacionEspecie donacion, IReadOnlyList<LineaValidada> lineas)
        {
            var ingresados = new List<string>();

            foreach (var linea in lineas)
            {
                try
                {
                    await _inventarioService.RegistrarEntradaAsync(new EntradaInventarioCrearDto
                    {
                        NombreArticulo = linea.NombreArticulo,
                        Categoria = linea.Categoria,
                        UnidadMedida = linea.UnidadMedida,
                        Cantidad = linea.Cantidad,
                        Fecha = donacion.Fecha,
                        Origen = OrigenesEntradaInventario.Donacion,
                        DonanteId = donacion.DonanteId,
                        Observaciones = $"Donación en especie #{donacion.Id}"
                    });

                    ingresados.Add(linea.NombreArticulo);
                }
                catch (Exception ex)
                {
                    var faltantes = lineas
                        .Skip(ingresados.Count)
                        .Select(l => $"{l.Cantidad} {l.UnidadMedida} de {l.NombreArticulo}");

                    // ValidationException y no un Exception genérico: es el único tipo
                    // que el filtro de excepciones deja pasar sin envolver, así que es
                    // el que garantiza que este mensaje llegue textual al usuario. Un
                    // Exception quedaría tapado por "Error al registrar la donación en
                    // especie" y se perdería justo el detalle que permite corregir.
                    //
                    // (Es el punto débil de este manejo: el tipo dice "dato inválido"
                    // cuando en realidad es una falla parcial. Un tipo propio sería
                    // más honesto, pero agregarlo obliga a revisar los filtros de
                    // excepción de todo el proyecto.)
                    throw new ValidationException(
                        $"La donación #{donacion.Id} quedó registrada, pero solo {ingresados.Count} de " +
                        $"{lineas.Count} artículos entraron al inventario. " +
                        $"Falta ingresar: {string.Join("; ", faltantes)}. " +
                        "Registralos como entrada con origen Donación desde el módulo de Inventario. " +
                        $"Motivo de la interrupción: {ex.Message}");
                }
            }
        }

        // ------------------------------------------------------------------
        // Donaciones entregadas
        // ------------------------------------------------------------------

        public async Task RegistrarDonacionEntregadaAsync(DonacionEntregadaCrearDto dto)
        {
            try
            {
                if (dto.Cantidad <= 0)
                    throw new ValidationException("La cantidad debe ser mayor a 0.");

                ValidarFechaNoFutura(dto.Fecha, "la entrega");

                if (!TiposDestinatarioDonacion.EsValido(dto.TipoDestinatario))
                {
                    throw new ValidationException(
                        $"El tipo de destinatario '{dto.TipoDestinatario}' no es válido. " +
                        $"Valores válidos: {string.Join(", ", TiposDestinatarioDonacion.Todos)}.");
                }

                // Exclusión mutua: se resuelve acá, no en el DTO, porque mira dos
                // propiedades a la vez y una DataAnnotation solo ve la suya. El CHECK
                // CK_DonacionesEntregadas_Destinatario lo respalda en la base; esta
                // validación existe para dar el mensaje entendible antes de llegar ahí.
                var (beneficiarioId, comunidad, textoDestino) = await ResolverDestinatarioAsync(dto);

                // ORDEN: primero el descuento de stock, después el registro de la
                // entrega. Es la dirección segura, por lo mismo que en la donación en
                // especie pero al revés:
                //
                //  - Descontando primero, una falla al guardar la entrega deja el
                //    stock correcto (la mercadería salió) y el movimiento asentado en
                //    SalidasInventario; lo que falta es el "a quién", recuperable.
                //  - Guardando primero, una falla al descontar dejaría una entrega
                //    registrada con el stock sin bajar: el sistema prometería
                //    existencias que ya no están en la bodega.
                //
                // Además el chequeo de stock suficiente vive DENTRO del descuento
                // (WHERE StockActual >= cantidad, atómico): si no alcanza, falla antes
                // de crear el registro de una entrega que no pudo ocurrir.
                await _inventarioService.RegistrarSalidaDonacionAsync(new SalidaDonacionCrearDto
                {
                    ArticuloId = dto.ArticuloId,
                    Cantidad = dto.Cantidad,
                    Fecha = dto.Fecha,
                    ComunidadDestinataria = textoDestino,
                    Observaciones = dto.Observaciones
                });

                var entrega = new DonacionEntregada
                {
                    ArticuloId = dto.ArticuloId,
                    Cantidad = dto.Cantidad,
                    Fecha = dto.Fecha,
                    TipoDestinatario = dto.TipoDestinatario,
                    BeneficiarioId = beneficiarioId,
                    ComunidadDestinataria = comunidad,
                    Observaciones = dto.Observaciones
                };

                try
                {
                    await _repository.AgregarDonacionEntregadaAsync(entrega);
                }
                catch (Exception ex)
                {
                    // Misma situación que en la donación en especie y mismo manejo:
                    // el stock ya bajó y la salida quedó registrada, así que no se
                    // puede reintentar la operación completa sin descontar dos veces.
                    // El mensaje dice exactamente qué quedó hecho y qué falta.
                    throw new ValidationException(
                        $"El stock se descontó y la salida quedó registrada en Inventario, " +
                        $"pero no se pudo guardar la entrega a {textoDestino}. " +
                        "NO vuelvas a registrarla desde acá (descontaría el stock otra vez): " +
                        "el movimiento ya figura en el historial de Inventario. " +
                        $"Motivo: {ex.Message}");
                }
            }
            catch (Exception ex) when (ex is not ValidationException and not NotFoundException and not DuplicateException)
            {
                throw new Exception("Error al registrar la donación entregada.", ex);
            }
        }

        // ------------------------------------------------------------------
        // Historiales
        // ------------------------------------------------------------------

        public async Task<HistorialDonacionesResultadoDto> ObtenerHistorialDonacionesAsync(
            FiltrosHistorialDonacionDto filtros)
        {
            try
            {
                // TipoDonacion no es una columna: decide CUÁLES de las dos consultas
                // se hacen. Con un tipo puntual se ahorra el viaje de la otra tabla.
                var incluyeDinero = filtros.TipoDonacion is null or TipoDonacionDinero;
                var incluyeEspecie = filtros.TipoDonacion is null or TipoDonacionEspecie;

                var enDinero = incluyeDinero
                    ? await _repository.ObtenerDonacionesDineroAsync(filtros)
                    : Enumerable.Empty<DonacionDinero>();

                var enEspecie = incluyeEspecie
                    ? await _repository.ObtenerDonacionesEspecieAsync(filtros)
                    : Enumerable.Empty<DonacionEspecie>();

                var donaciones = new List<HistorialDonacionDto>();

                donaciones.AddRange(enDinero.Select(d => new HistorialDonacionDto
                {
                    Id = d.Id,
                    TipoDonacion = TipoDonacionDinero,
                    NombreDonante = d.Donante?.Nombre ?? string.Empty,
                    Monto = d.Monto,
                    // En dinero no hay artículos que describir: lo único que aporta
                    // contexto son las observaciones.
                    Descripcion = d.Observaciones ?? string.Empty,
                    Fecha = d.Fecha
                }));

                donaciones.AddRange(enEspecie.Select(d => new HistorialDonacionDto
                {
                    Id = d.Id,
                    TipoDonacion = TipoDonacionEspecie,
                    NombreDonante = d.Donante?.Nombre ?? string.Empty,
                    // Monto queda en null: las donaciones en especie no se valorizan.
                    // Un 0 se leería como "donó cero colones", que es otra cosa.
                    Monto = null,
                    Descripcion = DescribirDetalles(d),
                    Fecha = d.Fecha
                }));

                return new HistorialDonacionesResultadoDto
                {
                    // Desempate por Id además de la fecha: las dos listas vienen
                    // ordenadas por separado y sin el desempate dos donaciones del
                    // mismo instante podrían intercambiarse entre consultas.
                    Donaciones = donaciones
                        .OrderByDescending(d => d.Fecha)
                        .ThenByDescending(d => d.Id)
                        .ToList(),

                    // Solo el dinero: no hay nada que sumar de las de especie.
                    // Se calcula sobre TODAS las filas que cumplen el filtro, que es
                    // el motivo por el que el total viaja en el resultado y no lo
                    // deduce la grilla de lo que muestra.
                    TotalDinero = enDinero.Sum(d => d.Monto)
                };
            }
            catch (Exception ex)
            {
                throw new Exception("Error al consultar el historial de donaciones.", ex);
            }
        }

        public async Task<HistorialEntregasResultadoDto> ObtenerHistorialEntregasAsync(
            FiltrosHistorialEntregaDto filtros)
        {
            try
            {
                var entregas = await _repository.ObtenerEntregasAsync(filtros);

                var filas = entregas.Select(e => new HistorialDonacionEntregadaDto
                {
                    Id = e.Id,
                    Articulo = e.Articulo?.Nombre ?? string.Empty,
                    Cantidad = e.Cantidad,
                    UnidadMedida = e.Articulo?.UnidadMedida ?? string.Empty,
                    Fecha = e.Fecha,
                    TipoDestinatario = e.TipoDestinatario,
                    NombreDestinatario = ResolverNombreDestinatario(e),
                    Observaciones = e.Observaciones
                }).ToList();

                return new HistorialEntregasResultadoDto
                {
                    Entregas = filas,

                    // Suma de unidades, con la reserva de que mezcla unidades de
                    // medida distintas: sirve como volumen de entregas, no como una
                    // magnitud física.
                    TotalEntregado = filas.Sum(f => f.Cantidad)
                };
            }
            catch (Exception ex)
            {
                throw new Exception("Error al consultar el historial de entregas.", ex);
            }
        }

        // ------------------------------------------------------------------
        // Apoyo
        // ------------------------------------------------------------------

        // Resuelve el par excluyente DonanteId / NuevoDonante y devuelve el Id con el
        // que hay que grabar la donación. Es el mismo procedimiento para dinero y
        // para especie, por eso está una sola vez.
        private async Task<int> ResolverDonanteAsync(int? donanteId, NuevoDonanteDto? nuevoDonante)
        {
            var tieneExistente = donanteId.HasValue && donanteId.Value > 0;
            var tieneNuevo = nuevoDonante is not null;

            // Los dos casos malos se distinguen en el mensaje: "no elegiste ninguno"
            // y "mandaste los dos" se corrigen de formas distintas.
            if (tieneExistente && tieneNuevo)
            {
                throw new ValidationException(
                    "Se recibieron a la vez un donante existente y uno nuevo. " +
                    "Elegí uno de los dos.");
            }

            if (!tieneExistente && !tieneNuevo)
                throw new ValidationException("Debe indicar el donante de la donación.");

            if (tieneNuevo)
            {
                // Se valida con el mismo DonanteValidator que el alta desde la
                // pantalla de donantes: registrar desde el formulario de donación no
                // puede ser una puerta con reglas más flojas.
                var datos = DonanteValidator.Validar(nuevoDonante!);

                var donante = new Donante
                {
                    Nombre = datos.Nombre,
                    TipoPersona = datos.TipoPersona,
                    Telefono = datos.Telefono,
                    Correo = datos.Correo,
                    Estado = true,
                    FechaRegistro = DateTime.Now
                };

                // El repositorio deja el Id generado en la misma instancia.
                await _donantesRepository.AgregarAsync(donante);

                return donante.Id;
            }

            var existente = await _donantesRepository.ObtenerPorIdAsync(donanteId!.Value)
                ?? throw new NotFoundException("El donante seleccionado no existe.");

            // Un donante inactivo salió de circulación a propósito: aceptar una
            // donación a su nombre lo devolvería al historial por la puerta de atrás.
            // Se avisa cómo destrabarlo en vez de solo negar.
            if (!existente.Estado)
            {
                throw new ValidationException(
                    $"El donante '{existente.Nombre}' está inactivo. " +
                    "Activalo desde el módulo de donantes para registrarle donaciones.");
            }

            return existente.Id;
        }

        // Valida la exclusión mutua y devuelve los tres valores que hacen falta:
        // el par (BeneficiarioId, ComunidadDestinataria) tal como se persiste, y el
        // texto de destino que se le manda a Inventario.
        private async Task<(int? BeneficiarioId, string? Comunidad, string TextoDestino)>
            ResolverDestinatarioAsync(DonacionEntregadaCrearDto dto)
        {
            if (dto.TipoDestinatario == TiposDestinatarioDonacion.Beneficiario)
            {
                if (!dto.BeneficiarioId.HasValue || dto.BeneficiarioId.Value <= 0)
                    throw new ValidationException("Debe seleccionar el beneficiario que recibe la entrega.");

                if (!string.IsNullOrWhiteSpace(dto.ComunidadDestinataria))
                {
                    throw new ValidationException(
                        "Una entrega a un beneficiario no puede llevar además una comunidad destinataria.");
                }

                var beneficiario = await _beneficiariosRepository.ObtenerPorIdAsync(dto.BeneficiarioId.Value)
                    ?? throw new NotFoundException("El beneficiario seleccionado no existe.");

                if (!beneficiario.Estado)
                {
                    throw new ValidationException(
                        $"El beneficiario '{beneficiario.NombreCompleto}' está inactivo. " +
                        "Activalo desde el módulo de beneficiarios para registrarle entregas.");
                }

                // TEXTO DE DESTINO PARA INVENTARIO.
                //
                // RegistrarSalidaDonacionAsync EXIGE una ComunidadDestinataria no
                // vacía, y una entrega a un beneficiario no tiene comunidad. Como no
                // se puede tocar Inventario en este paso, se le manda el nombre de la
                // persona rotulado, que es lo que esa columna representa en la
                // práctica (a dónde fue la mercadería) y lo que el historial de
                // movimientos muestra en OrigenODestino.
                //
                // Es un parche declarado: la solución correcta es que
                // IInventarioService acepte un destino genérico —o directamente un
                // DonacionEntregadaId, como ya hace con SolicitudPrestamoId para los
                // préstamos— en lugar de exigir una comunidad.
                var destino = $"Beneficiario: {beneficiario.NombreCompleto}";

                return (beneficiario.Id, null, Recortar(destino, LongitudMaximaDestinoSalida));
            }

            // TipoDestinatario == Comunidad (el catálogo solo tiene estos dos valores
            // y ya se validó contra él antes de llegar acá).
            var comunidad = TextoNormalizador.CompactarEspacios(dto.ComunidadDestinataria);

            if (comunidad.Length == 0)
                throw new ValidationException("Debe indicar la comunidad que recibe la entrega.");

            if (dto.BeneficiarioId.HasValue && dto.BeneficiarioId.Value > 0)
            {
                throw new ValidationException(
                    "Una entrega a una comunidad no puede llevar además un beneficiario.");
            }

            if (comunidad.Length > LongitudMaximaDestinoSalida)
            {
                throw new ValidationException(
                    $"La comunidad no puede superar los {LongitudMaximaDestinoSalida} caracteres.");
            }

            return (null, comunidad, comunidad);
        }

        // Una línea de detalle ya validada y normalizada. Mismo criterio que los
        // record *Validado de los validadores: lo que sale de acá se copia sin
        // volver a limpiar.
        private sealed record LineaValidada(
            string NombreArticulo,
            int Cantidad,
            string Categoria,
            string UnidadMedida);

        private static LineaValidada ValidarLinea(DetalleArticuloDto linea)
        {
            // El nombre se normaliza con el MISMO validador que usa Inventario: esta
            // línea termina buscando o creando un artículo por nombre, que es la
            // clave natural del catálogo. Normalizar distinto acá haría que " Arroz"
            // se registre como un artículo aparte de "Arroz" y el stock quede partido.
            var nombre = ArticuloValidator.ValidarNombre(linea.NombreArticulo);

            if (linea.Cantidad <= 0)
                throw new ValidationException($"La cantidad de '{nombre}' debe ser mayor a 0.");

            // ValidarCategoriaYUnidad cubre lo obligatorio, la longitud, que la
            // categoría exista en CategoriasArticulo y que la unidad sea válida PARA
            // esa categoría (UnidadesMedidaArticulo.EsValidaParaCategoria). Se reusa
            // el de Inventario en vez de repetir la regla: es la misma tabla de
            // correspondencias y dos copias terminarían divergiendo.
            var (categoria, unidadMedida) =
                ArticuloValidator.ValidarCategoriaYUnidad(linea.Categoria, linea.UnidadMedida);

            return new LineaValidada(nombre, linea.Cantidad, categoria, unidadMedida);
        }

        // "3 Kilogramo de Arroz, 2 Paquete de Frijoles". Incluye la unidad porque sin
        // ella "3 Arroz" no dice si son tres kilos o tres cajas.
        private static string DescribirDetalles(DonacionEspecie donacion)
        {
            if (donacion.Detalles.Count == 0)
            {
                // Solo debería verse si el Include de Detalles no se hizo o si la
                // cabecera quedó sin líneas por una escritura externa: el servicio
                // nunca crea una donación en especie sin detalle.
                return donacion.Observaciones ?? string.Empty;
            }

            return string.Join(", ", donacion.Detalles
                .Select(d => $"{d.Cantidad} {d.UnidadMedida} de {d.NombreArticulo}"));
        }

        // Una sola columna de texto para las dos clases de destinatario: la grilla no
        // tiene que saber de cuál de los dos campos salió.
        private static string ResolverNombreDestinatario(DonacionEntregada entrega)
        {
            if (entrega.TipoDestinatario == TiposDestinatarioDonacion.Beneficiario)
            {
                // Si el Include no trajo el beneficiario, se cae a la cadena vacía en
                // lugar de romper el historial entero por una fila.
                return entrega.Beneficiario?.NombreCompleto ?? string.Empty;
            }

            return entrega.ComunidadDestinataria ?? string.Empty;
        }

        private static void ValidarFechaNoFutura(DateTime fecha, string queCosa)
        {
            if (fecha.Date > DateTime.Today)
                throw new ValidationException($"La fecha de {queCosa} no puede ser futura.");
        }

        private static string Recortar(string valor, int longitudMaxima) =>
            valor.Length <= longitudMaxima ? valor : valor[..longitudMaxima];
    }
}
