using SIGAC.Application.DTOs;
using SIGAC.Application.DTOs.Inventario;
using SIGAC.Application.Exceptions;
using SIGAC.Application.Interfaces;
using SIGAC.Application.Validators;
using SIGAC.Domain;
using SIGAC.Domain.Entities;

namespace SIGAC.Application.Services
{
    public class InventarioService : IInventarioService
    {
        private readonly IInventarioRepository _repository;
        private readonly IBitacoraService _bitacora;

        public InventarioService(IInventarioRepository repository, IBitacoraService bitacora)
        {
            _repository = repository;
            _bitacora = bitacora;
        }

        public async Task RegistrarEntradaAsync(EntradaInventarioCrearDto dto)
        {
            try
            {
                var preparada = await PrepararEntradaAsync(dto, new List<Articulo>());

                // Una sola llamada: crear el artículo (si hace falta), insertar la
                // entrada y sumar el stock son todo-o-nada.
                await _repository.RegistrarEntradaConStockAsync(
                    preparada.Entrada, preparada.CreaArticulo ? preparada.Articulo : null);

                await _bitacora.RegistrarAsync(AccionesBitacora.Registrar, ModulosSistema.Inventario,
                    DescribirEntrada(preparada));
            }
            catch (Exception ex) when (ex is not ValidationException and not DuplicateException)
            {
                throw new Exception("Error al registrar la entrada de inventario.", ex);
            }
        }

        // Valida un lote de entradas y resuelve a qué artículo suma cada una, SIN
        // escribir nada: la escritura la hace quien llama, dentro de su propia
        // transacción. Lo usa Donaciones para que la donación en especie y su ingreso
        // al stock sean todo-o-nada (antes eran dos fases y una falla en la segunda
        // dejaba la donación guardada con el inventario a medias).
        //
        // Inventario sigue siendo el dueño de las reglas del catálogo: qué artículo
        // corresponde a (nombre, estado), qué categoría y unidad lleva uno nuevo y
        // qué se rechaza. Solo se presta la ESCRITURA.
        public async Task<IReadOnlyList<EntradaInventarioPreparada>> PrepararEntradasAsync(
            IReadOnlyList<EntradaInventarioCrearDto> entradas)
        {
            try
            {
                var creadosEnElLote = new List<Articulo>();
                var preparadas = new List<EntradaInventarioPreparada>(entradas.Count);

                foreach (var dto in entradas)
                    preparadas.Add(await PrepararEntradaAsync(dto, creadosEnElLote));

                return preparadas;
            }
            catch (Exception ex) when (ex is not ValidationException and not DuplicateException)
            {
                throw new Exception("Error al preparar las entradas de inventario.", ex);
            }
        }

        public static string DescribirEntrada(EntradaInventarioPreparada preparada) =>
            $"Entrada de {preparada.Entrada.Cantidad} de '{preparada.Articulo.Etiqueta}' ({preparada.Entrada.Origen})" +
            (preparada.CreaArticulo ? ", artículo nuevo" : string.Empty);

        private async Task<EntradaInventarioPreparada> PrepararEntradaAsync(
            EntradaInventarioCrearDto dto, List<Articulo> creadosEnElLote)
        {
            if (string.IsNullOrWhiteSpace(dto.NombreArticulo) || dto.Cantidad <= 0)
                throw new ValidationException("Nombre del artículo y cantidad (mayor a 0) son obligatorios.");

            // Antes esta comprobación no existía: un Origen inválido pasaba de
            // largo hasta que el CHECK de la base lo rechazaba con un error
            // genérico. Se valida acá para dar un mensaje claro.
            if (!OrigenesEntradaInventario.EsValido(dto.Origen))
                throw new ValidationException(
                    $"El origen debe ser uno de los siguientes valores: {string.Join(", ", OrigenesEntradaInventario.Todos)}.");

            // Se normaliza ANTES de buscar y ANTES de guardar, con el mismo valor
            // para las dos cosas: el nombre es la clave natural del catálogo, así
            // que comparar por un texto y persistir otro es lo que hacía que
            // " Arroz" no encontrara a "Arroz" y entrara como un artículo aparte,
            // partiendo el stock en dos filas.
            var nombreArticulo = ArticuloValidator.ValidarNombre(dto.NombreArticulo);

            // La clave del catálogo es (Nombre, Estado): un mismo nombre puede
            // tener varias filas, una por estado, cuando es Equipo. Todas las
            // filas de un nombre son de la misma categoría (ver
            // ValidarNombreLibreDeOtraCategoria), así que basta mirar la primera
            // para saber si el nombre lleva estado.
            var coincidencias = (await _repository.ObtenerArticulosPorNombreAsync(nombreArticulo)).ToList();

            // Los artículos que este mismo lote ya va a crear cuentan como existentes:
            // dos líneas de una donación con el mismo artículo nuevo deben sumar a UNA
            // sola fila, no chocar entre sí contra UX_Articulos_Nombre_Estado.
            coincidencias.AddRange(creadosEnElLote.Where(a =>
                string.Equals(a.Nombre, nombreArticulo, StringComparison.OrdinalIgnoreCase)));

            Articulo? articulo = null;

            if (coincidencias.Count > 0)
            {
                if (EstadosArticulo.AplicaACategoria(coincidencias[0].Categoria))
                {
                    // Equipo: el estado elige la fila. Es "sin fila" (artículo
                    // nuevo) cuando ese nombre existe pero en otro estado.
                    var estadoEntrada = ArticuloValidator.ValidarEstado(
                        coincidencias[0].Categoria, dto.Estado);
                    articulo = coincidencias.FirstOrDefault(a => a.Estado == estadoEntrada);
                }
                else
                {
                    articulo = coincidencias[0];
                }
            }

            // La entrada se registra el día en que ocurre: una fecha futura no
            // representa un movimiento real todavía. Antes solo lo restringía el
            // calendario del formulario (MaxDate), sin repetirlo en el servidor.
            if (dto.Fecha.Date > DateTime.Today)
                throw new ValidationException("La fecha de la entrada no puede ser futura.");

            // El artículo nuevo se arma acá pero NO se guarda por separado: se le
            // pasa al repositorio para que lo cree dentro de la misma transacción
            // que la entrada y el stock. Guardarlo antes, por su cuenta, dejaba un
            // artículo en el catálogo con stock 0 si la entrada fallaba después.
            Articulo? articuloNuevo = null;

            if (articulo is null)
            {
                // Categoría, unidad, código y ubicación solo se validan cuando hay
                // artículo nuevo: son los únicos casos en que se persisten. Si el
                // artículo ya existe, el formulario deshabilita esos campos y el
                // servicio usa los que ya tiene guardados, así que exigirlos acá
                // rechazaría entradas de artículos viejos cuya categoría no esté en
                // el catálogo actual.
                //
                // ValidarCategoriaYUnidad cubre lo obligatorio, la longitud máxima
                // y además que la combinación exista en el catálogo cerrado.
                var (categoria, unidadMedida) =
                    ArticuloValidator.ValidarCategoriaYUnidad(dto.Categoria, dto.UnidadMedida);

                // Un nombre que ya existe en otro estado (Equipo) no puede darse
                // de alta en otra categoría: esa fila y las de acá compartirían
                // nombre siendo de mundos distintos.
                ValidarNombreLibreDeOtraCategoria(coincidencias, categoria, nombreArticulo);

                var estado = ArticuloValidator.ValidarEstado(categoria, dto.Estado);

                // Normalizados con el mismo criterio que EditarArticuloAsync: el
                // valor que se compara contra el índice único tiene que ser el
                // mismo que se persiste. Comparar "P001 " en crudo y guardar
                // "P001" dejaba pasar el chequeo para chocar después contra
                // UX_Articulos_Codigo.
                var codigo = ArticuloValidator.ValidarCodigo(dto.Codigo);
                var ubicacion = ArticuloValidator.ValidarUbicacion(dto.Ubicacion);

                // Antes no se comprobaba: dos artículos nuevos con el mismo código
                // chocarían recién en la base, con un mensaje genérico que no dice
                // que fue el código (y no el nombre) lo que coincidió.
                if (await _repository.ExisteCodigoAsync(codigo))
                    throw new DuplicateException($"Ya existe otro artículo con el código \"{codigo}\".");

                articuloNuevo = new Articulo
                {
                    Nombre = nombreArticulo,
                    Codigo = codigo,
                    Categoria = categoria,
                    Estado = estado,
                    UnidadMedida = unidadMedida,
                    Ubicacion = ubicacion,
                    StockActual = 0
                };

                creadosEnElLote.Add(articuloNuevo);
            }

            var entrada = new EntradaInventario
            {
                // Cuando el artículo es nuevo todavía no tiene Id (lo genera la
                // base al insertarlo); el repositorio lo completa dentro de la
                // transacción, una vez creado.
                ArticuloId = articulo?.Id ?? 0,
                Cantidad = dto.Cantidad,
                Fecha = dto.Fecha,
                Origen = dto.Origen,
                Observaciones = dto.Observaciones
            };

            if (dto.Origen.Equals(OrigenesEntradaInventario.Donacion, StringComparison.OrdinalIgnoreCase))
                entrada.DonanteId = dto.DonanteId;
            else if (dto.Origen.Equals(OrigenesEntradaInventario.Compra, StringComparison.OrdinalIgnoreCase))
                entrada.GastoOperativoId = dto.GastoOperativoId;

            // Un artículo que todavía no tiene Id (nuevo en este lote, ya sea creado
            // por esta línea o por una anterior) se cuelga por navegación, para que
            // EF le ponga el Id al insertarlo. Los existentes van solo por ArticuloId:
            // adjuntar su entidad leída aparte haría que EF intentara rastrearla.
            var destino = articuloNuevo ?? articulo!;
            if (destino.Id == 0)
                entrada.Articulo = destino;

            return new EntradaInventarioPreparada(entrada, destino, articuloNuevo is not null);
        }

        // Todas las filas de un mismo nombre tienen que ser de la misma categoría. La
        // base solo garantiza que (Nombre, Estado) no se repita, no que "Mesa" no
        // exista a la vez como Alimento (sin estado) y como Equipo (con estado):
        // para evitarlo la búsqueda por nombre de RegistrarEntradaAsync, que decide
        // con la primera fila, no tendría de qué fiarse.
        private static void ValidarNombreLibreDeOtraCategoria(
            IReadOnlyList<Articulo> otrosConEseNombre, string categoria, string nombre)
        {
            var enOtraCategoria = otrosConEseNombre.FirstOrDefault(a => a.Categoria != categoria);

            if (enOtraCategoria is not null)
            {
                throw new ValidationException(
                    $"Ya existe un artículo llamado '{nombre}' en la categoría {enOtraCategoria.Categoria}. " +
                    "Un mismo nombre no puede repetirse en otra categoría.");
            }
        }

        public async Task RegistrarSalidaDonacionAsync(SalidaDonacionCrearDto dto)
        {
            try
            {
                var salida = await ValidarSalidaDonacionAsync(dto);

                // Una sola llamada: insertar la salida y descontar el stock son
                // todo-o-nada. Antes eran dos guardados independientes y una falla al
                // descontar dejaba la salida ya registrada, inflando el stock real.
                await _repository.RegistrarSalidaConStockAsync(salida);

                await _bitacora.RegistrarAsync(AccionesBitacora.Entregar, ModulosSistema.Inventario,
                    DescribirSalida(salida));
            }
            catch (Exception ex) when (ex is not ValidationException and not NotFoundException)
            {
                throw new Exception("Error al registrar la salida de inventario.", ex);
            }
        }

        // Valida la salida y la arma SIN escribir nada. Donaciones la guarda junto con
        // la entrega en una sola transacción: antes la salida (con su descuento) se
        // confirmaba primero y, si después fallaba el guardado de la entrega, el stock
        // quedaba descontado sin entrega que lo respaldara.
        public async Task<SalidaInventario> PrepararSalidaDonacionAsync(SalidaDonacionCrearDto dto)
        {
            try
            {
                return await ValidarSalidaDonacionAsync(dto);
            }
            catch (Exception ex) when (ex is not ValidationException and not NotFoundException)
            {
                throw new Exception("Error al preparar la salida de inventario.", ex);
            }
        }

        public static string DescribirSalida(SalidaInventario salida) =>
            $"Salida por donación de {salida.Cantidad} (artículo #{salida.ArticuloId}) a {salida.ComunidadDestinataria}";

        private async Task<SalidaInventario> ValidarSalidaDonacionAsync(SalidaDonacionCrearDto dto)
        {
            var articulo = await _repository.ObtenerArticuloPorIdAsync(dto.ArticuloId)
                ?? throw new NotFoundException("El artículo no existe.");

            if (dto.Cantidad <= 0)
                throw new ValidationException("La cantidad debe ser mayor a 0.");

            if (dto.Cantidad > articulo.StockActual)
                throw new ValidationException("La cantidad solicitada supera el stock disponible.");

            // Antes no se validaba: se podía registrar una salida por donación
            // sin decir a qué comunidad fue, que es justo lo que le da
            // trazabilidad a esta historia de usuario.
            if (string.IsNullOrWhiteSpace(dto.ComunidadDestinataria))
                throw new ValidationException("La comunidad destinataria es obligatoria.");

            // Sin fecha el servidor guardaba 01/01/0001: el formulario la pide, pero
            // nada lo exigía del lado del servidor. Y una salida es una mercadería que
            // ya salió, no una que va a salir, así que tampoco puede ser futura.
            if (dto.Fecha == default)
                throw new ValidationException("La fecha de la salida es obligatoria.");

            if (dto.Fecha.Date > DateTime.Today)
                throw new ValidationException("La fecha de la salida no puede ser futura.");

            return new SalidaInventario
            {
                ArticuloId = dto.ArticuloId,
                Cantidad = dto.Cantidad,
                Fecha = dto.Fecha,
                TipoSalida = TiposSalidaInventario.Donacion,
                ComunidadDestinataria = dto.ComunidadDestinataria,
                Observaciones = dto.Observaciones
            };
        }

        public async Task<ResultadoPaginado<ArticuloExistenciaDto>> ObtenerExistenciasAsync(FiltrosExistenciaDto filtros)
        {
            try
            {
                var pagina = await _repository.ObtenerExistenciasAsync(filtros);

                var elementos = pagina.Elementos.Select(a => new ArticuloExistenciaDto
                {
                    Id = a.Id,
                    Nombre = a.Nombre,
                    Codigo = a.Codigo,
                    Categoria = a.Categoria,
                    Estado = a.Estado,
                    UnidadMedida = a.UnidadMedida,
                    Ubicacion = a.Ubicacion,
                    StockActual = a.StockActual,
                    StockBajo = a.StockActual <= a.StockMinimo
                }).ToList();

                return new ResultadoPaginado<ArticuloExistenciaDto>(elementos, pagina.TotalRegistros);
            }
            catch (Exception ex)
            {
                throw new Exception("Error al consultar existencias.", ex);
            }
        }

        public async Task<int> ContarArticulosStockBajoAsync()
        {
            try
            {
                return await _repository.ContarStockBajoAsync();
            }
            catch (Exception ex)
            {
                throw new Exception("Error al consultar los artículos con stock bajo.", ex);
            }
        }

        // Sin ObtenerCategoriasAsync ni ObtenerUnidadesMedidaAsync: pertenecían al
        // catálogo ABIERTO (sugerencias armadas con un DISTINCT de la tabla más unas
        // semillas), que se descartó en favor del catálogo cerrado de
        // CategoriasArticulo/UnidadesMedidaArticulo. Las pantallas leen las opciones
        // directo de esas clases, así que no hay a quién servirle esa lista.

        public async Task<ArticuloEditarDto?> ObtenerParaEditarAsync(int id)
        {
            try
            {
                var articulo = await _repository.ObtenerArticuloPorIdAsync(id);
                if (articulo is null)
                    return null;

                return new ArticuloEditarDto
                {
                    Nombre = articulo.Nombre,
                    Codigo = articulo.Codigo,
                    Categoria = articulo.Categoria,
                    Estado = articulo.Estado,
                    UnidadMedida = articulo.UnidadMedida,
                    Ubicacion = articulo.Ubicacion,
                    StockMinimo = articulo.StockMinimo,
                    StockActual = articulo.StockActual
                };
            }
            catch (Exception ex)
            {
                throw new Exception("Error al consultar el artículo.", ex);
            }
        }

        public async Task EditarArticuloAsync(int id, ArticuloEditarDto dto)
        {
            try
            {
                var articulo = await _repository.ObtenerArticuloPorIdAsync(id)
                    ?? throw new NotFoundException("El artículo no existe.");

                // Normaliza y valida de una sola vez. Lo que devuelve es exactamente
                // lo que se compara contra los índices únicos y lo que se persiste:
                // antes el código se comprobaba crudo pero se guardaba con Trim(), así
                // que "P001 " pasaba el chequeo de unicidad y después chocaba contra
                // UX_Articulos_Codigo al guardarse como "P001".
                // Cubre lo obligatorio, la longitud máxima de cada campo y, además, que
                // la combinación categoría/unidad exista en el catálogo cerrado. Por eso
                // no se repiten acá los chequeos sueltos de no-vacío y longitud.
                var datos = ArticuloValidator.Validar(dto);

                // Antes no se comprobaba: renombrar un artículo a un nombre ya usado
                // por otro solo se detectaba cuando el índice único lo rechazaba con
                // un error genérico. Se excluye el propio id para no chocar consigo mismo.
                //
                // La identidad es (Nombre, Estado): repetir el nombre solo choca si
                // también coincide el estado (dos sillas en distinto estado conviven).
                var otrosConEseNombre = (await _repository.ObtenerArticulosPorNombreAsync(datos.Nombre))
                    .Where(a => a.Id != id)
                    .ToList();

                ValidarNombreLibreDeOtraCategoria(otrosConEseNombre, datos.Categoria, datos.Nombre);

                if (otrosConEseNombre.Any(a => a.Estado == datos.Estado))
                {
                    throw new DuplicateException(
                        $"Ya existe otro artículo con el nombre \"{Articulo.EtiquetaDe(datos.Nombre, datos.Estado)}\".");
                }

                if (await _repository.ExisteCodigoAsync(datos.Codigo, id))
                    throw new DuplicateException($"Ya existe otro artículo con el código \"{datos.Codigo}\".");

                articulo.Nombre = datos.Nombre;
                articulo.Codigo = datos.Codigo;
                articulo.Categoria = datos.Categoria;
                articulo.Estado = datos.Estado;
                articulo.UnidadMedida = datos.UnidadMedida;
                articulo.Ubicacion = datos.Ubicacion;
                articulo.StockMinimo = datos.StockMinimo;

                await _repository.ActualizarArticuloAsync(articulo);

                await _bitacora.RegistrarAsync(AccionesBitacora.Editar, ModulosSistema.Inventario,
                    $"Artículo #{articulo.Id}: {articulo.Etiqueta}");
            }
            catch (Exception ex) when (ex is not ValidationException and not NotFoundException and not DuplicateException)
            {
                throw new Exception("Error al editar el artículo.", ex);
            }
        }

        public async Task EliminarArticuloAsync(int id)
        {
            try
            {
                _ = await _repository.ObtenerArticuloPorIdAsync(id)
                    ?? throw new NotFoundException("El artículo no existe.");

                // El Restrict de las FK respalda esto en la base, pero sin este
                // chequeo el usuario vería una excepción de SQL sin traducir en vez
                // de un mensaje que explique por qué no se puede borrar.
                if (await _repository.TieneMovimientosAsync(id))
                    throw new ValidationException(
                        "No se puede eliminar: el artículo tiene entradas o salidas registradas. Puede editarlo, pero no borrarlo.");

                await _repository.EliminarArticuloAsync(id);

                await _bitacora.RegistrarAsync(AccionesBitacora.Eliminar, ModulosSistema.Inventario,
                    $"Artículo #{id}");
            }
            catch (Exception ex) when (ex is not ValidationException and not NotFoundException)
            {
                throw new Exception("Error al eliminar el artículo.", ex);
            }
        }

        public async Task<HistorialMovimientosResultadoDto> ObtenerHistorialMovimientosAsync(FiltrosMovimientoDto filtros)
        {
            try
            {
                var entradas = filtros.IncluyeEntradas
                    ? await _repository.ObtenerEntradasAsync(filtros.ArticuloId, filtros.Desde, filtros.Hasta)
                    : Enumerable.Empty<EntradaInventario>();

                var salidas = filtros.IncluyeSalidas
                    ? await _repository.ObtenerSalidasAsync(filtros.ArticuloId, filtros.Desde, filtros.Hasta)
                    : Enumerable.Empty<SalidaInventario>();

                // ObtenerSalidasAsync no filtra por TipoSalida (solo por artículo y
                // fecha), así que un sub-tipo puntual de salida se recorta
                // acá antes de mapear.
                if (filtros.TipoSalida is not null)
                    salidas = salidas.Where(s => s.TipoSalida == filtros.TipoSalida);

                var movimientos = new List<MovimientoInventarioDto>();

                movimientos.AddRange(entradas.Select(AFilaDeMovimiento));
                movimientos.AddRange(salidas.Select(AFilaDeMovimiento));

                var ordenados = movimientos.OrderByDescending(m => m.Fecha).ToList();

                return new HistorialMovimientosResultadoDto
                {
                    Movimientos = ordenados,
                    TotalRegistros = ordenados.Count,
                    TotalEntradas = entradas.Sum(e => e.Cantidad),
                    TotalSalidas = salidas.Sum(s => s.Cantidad)
                };
            }
            catch (Exception ex)
            {
                throw new Exception("Error al consultar el historial de movimientos.", ex);
            }
        }

        // La misma lista que ObtenerHistorialMovimientosAsync pero de a una página, para
        // la pantalla del historial: el repositorio trae de la base solo esas filas (y
        // cuántas hay en total), no las dos tablas completas. Los reportes siguen con el
        // historial completo porque exportan todo el período.
        public async Task<HistorialMovimientosResultadoDto> ObtenerPaginaHistorialMovimientosAsync(FiltrosMovimientoDto filtros)
        {
            try
            {
                var pagina = await _repository.ObtenerPaginaMovimientosAsync(filtros);

                // Los totales (en unidades) cubren todo el período filtrado, no la
                // página: sumar las filas de la página daría solo las de esa página.
                var totales = await _repository.ObtenerTotalesMovimientosAsync(filtros);

                var filas = new List<MovimientoInventarioDto>(pagina.Elementos.Count);

                foreach (var item in pagina.Elementos)
                {
                    if (item.Entrada is not null)
                        filas.Add(AFilaDeMovimiento(item.Entrada));
                    else if (item.Salida is not null)
                        filas.Add(AFilaDeMovimiento(item.Salida));
                }

                return new HistorialMovimientosResultadoDto
                {
                    Movimientos = filas,
                    TotalRegistros = pagina.TotalRegistros,
                    TotalEntradas = totales.Entradas,
                    TotalSalidas = totales.Salidas
                };
            }
            catch (Exception ex)
            {
                throw new Exception("Error al consultar el historial de movimientos.", ex);
            }
        }

        private static MovimientoInventarioDto AFilaDeMovimiento(EntradaInventario e) => new()
        {
            Id = e.Id,
            Articulo = e.Articulo?.Etiqueta ?? string.Empty,
            TipoMovimiento = FiltrosMovimientoDto.TipoEntrada,
            Cantidad = e.Cantidad,
            Fecha = e.Fecha,
            OrigenODestino = e.Origen
        };

        private static MovimientoInventarioDto AFilaDeMovimiento(SalidaInventario s) => new()
        {
            Id = s.Id,
            Articulo = s.Articulo?.Etiqueta ?? string.Empty,
            TipoMovimiento = s.TipoSalida,
            Cantidad = s.Cantidad,
            Fecha = s.Fecha,
            OrigenODestino = s.ComunidadDestinataria ?? s.TipoSalida
        };
    }
}