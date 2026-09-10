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

        public InventarioService(IInventarioRepository repository)
        {
            _repository = repository;
        }

        public async Task RegistrarEntradaAsync(EntradaInventarioCrearDto dto)
        {
            try
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

                var articulo = await _repository.ObtenerArticuloPorNombreAsync(nombreArticulo);

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
                        UnidadMedida = unidadMedida,
                        Ubicacion = ubicacion,
                        StockActual = 0
                    };
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

                // Una sola llamada: crear el artículo (si hace falta), insertar la
                // entrada y sumar el stock son todo-o-nada.
                await _repository.RegistrarEntradaConStockAsync(entrada, articuloNuevo);
            }
            catch (Exception ex) when (ex is not ValidationException and not DuplicateException)
            {
                throw new Exception("Error al registrar la entrada de inventario.", ex);
            }
        }

        public async Task RegistrarSalidaDonacionAsync(SalidaDonacionCrearDto dto)
        {
            try
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

                var salida = new SalidaInventario
                {
                    ArticuloId = dto.ArticuloId,
                    Cantidad = dto.Cantidad,
                    Fecha = dto.Fecha,
                    TipoSalida = TiposSalidaInventario.Donacion,
                    ComunidadDestinataria = dto.ComunidadDestinataria,
                    Observaciones = dto.Observaciones
                };

                // Una sola llamada: insertar la salida y descontar el stock son
                // todo-o-nada. Antes eran dos guardados independientes y una falla al
                // descontar dejaba la salida ya registrada, inflando el stock real.
                await _repository.RegistrarSalidaConStockAsync(salida);
            }
            catch (Exception ex) when (ex is not ValidationException and not NotFoundException)
            {
                throw new Exception("Error al registrar la salida de inventario.", ex);
            }
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
                if (await _repository.ExisteNombreAsync(datos.Nombre, id))
                    throw new DuplicateException($"Ya existe otro artículo con el nombre \"{datos.Nombre}\".");

                if (await _repository.ExisteCodigoAsync(datos.Codigo, id))
                    throw new DuplicateException($"Ya existe otro artículo con el código \"{datos.Codigo}\".");

                articulo.Nombre = datos.Nombre;
                articulo.Codigo = datos.Codigo;
                articulo.Categoria = datos.Categoria;
                articulo.UnidadMedida = datos.UnidadMedida;
                articulo.Ubicacion = datos.Ubicacion;
                articulo.StockMinimo = datos.StockMinimo;

                await _repository.ActualizarArticuloAsync(articulo);
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
                        "No se puede eliminar: el artículo tiene entradas, salidas o solicitudes de préstamo registradas. Podés editarlo, pero no borrarlo.");

                await _repository.EliminarArticuloAsync(id);
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
                var incluyeEntradas = filtros.TipoMovimiento is null or "Entrada";
                var incluyeSalidas = filtros.TipoMovimiento is null
                    or TiposSalidaInventario.Donacion
                    or TiposSalidaInventario.Prestamo;

                var entradas = incluyeEntradas
                    ? await _repository.ObtenerEntradasAsync(filtros.ArticuloId, filtros.Desde, filtros.Hasta)
                    : Enumerable.Empty<EntradaInventario>();

                var salidas = incluyeSalidas
                    ? await _repository.ObtenerSalidasAsync(filtros.ArticuloId, filtros.Desde, filtros.Hasta)
                    : Enumerable.Empty<SalidaInventario>();

                // ObtenerSalidasAsync no filtra por TipoSalida (solo por artículo y
                // fecha), así que un sub-tipo puntual (Donacion o Prestamo) se recorta
                // acá antes de mapear.
                if (filtros.TipoMovimiento is TiposSalidaInventario.Donacion or TiposSalidaInventario.Prestamo)
                    salidas = salidas.Where(s => s.TipoSalida == filtros.TipoMovimiento);

                var movimientos = new List<MovimientoInventarioDto>();

                movimientos.AddRange(entradas.Select(e => new MovimientoInventarioDto
                {
                    Id = e.Id,
                    Articulo = e.Articulo?.Nombre ?? string.Empty,
                    TipoMovimiento = "Entrada",
                    Cantidad = e.Cantidad,
                    Fecha = e.Fecha,
                    OrigenODestino = e.Origen
                }));

                movimientos.AddRange(salidas.Select(s => new MovimientoInventarioDto
                {
                    Id = s.Id,
                    Articulo = s.Articulo?.Nombre ?? string.Empty,
                    TipoMovimiento = s.TipoSalida,
                    Cantidad = s.Cantidad,
                    Fecha = s.Fecha,
                    OrigenODestino = s.ComunidadDestinataria ?? s.TipoSalida
                }));

                return new HistorialMovimientosResultadoDto
                {
                    Movimientos = movimientos.OrderByDescending(m => m.Fecha).ToList(),
                    TotalEntradas = entradas.Sum(e => e.Cantidad),
                    TotalSalidas = salidas.Sum(s => s.Cantidad)
                };
            }
            catch (Exception ex)
            {
                throw new Exception("Error al consultar el historial de movimientos.", ex);
            }
        }

        public async Task RegistrarSolicitudPrestamoAsync(SolicitudPrestamoCrearDto dto)
        {
            try
            {
                var articulo = await _repository.ObtenerArticuloPorIdAsync(dto.ArticuloId)
                    ?? throw new NotFoundException("El artículo no existe.");

                // Va primero, antes de mirar la cantidad: es un rechazo categórico, no
                // una cuestión de cuánto se pide. Si el artículo no se presta, avisar
                // "la cantidad debe ser mayor a 0" mandaría al usuario a corregir algo
                // que no es el problema.
                //
                // Solo se presta Equipo (mobiliario, equipo de cocina, herramientas).
                // Alimento, Ropa y Calzado se consumen o se entregan en forma
                // definitiva: no vuelven, así que no hay préstamo que registrar.
                //
                // SolicitudPrestamo.razor ya filtra el buscador por esta categoría,
                // pero eso es una comodidad de la pantalla, no la regla: sin esta
                // comprobación, cualquier otro camino al servicio puede crear la
                // solicitud igual.
                if (articulo.Categoria != CategoriasArticulo.Equipo)
                {
                    throw new ValidationException(
                        $"Solo se pueden prestar artículos de categoría {CategoriasArticulo.Equipo}.");
                }

                if (dto.Cantidad <= 0)
                    throw new ValidationException("La cantidad debe ser mayor a 0.");

                // Mismo chequeo que RegistrarSalidaDonacionAsync. Es una validación
                // del momento del pedido, no una reserva: el stock puede cambiar antes
                // de la aprobación, y AprobarPrestamoAsync lo vuelve a verificar (y el
                // descuento lo cierra en SQL dentro de la transacción). Lo que evita es
                // dejar entrar solicitudes que ya nacen imposibles de aprobar.
                if (dto.Cantidad > articulo.StockActual)
                    throw new ValidationException("La cantidad solicitada supera el stock disponible.");

                // El DTO ya lleva [Required] en Actividad y Solicitante, pero eso solo
                // corre en el EditForm del cliente; se repite acá para que un llamador
                // que no pase por ese formulario no pueda dejarlos vacíos.
                if (string.IsNullOrWhiteSpace(dto.Actividad))
                    throw new ValidationException("La actividad es obligatoria.");

                if (string.IsNullOrWhiteSpace(dto.Solicitante))
                    throw new ValidationException("El solicitante es obligatorio.");

                var solicitud = new SolicitudPrestamo
                {
                    ArticuloId = dto.ArticuloId,
                    Cantidad = dto.Cantidad,
                    Fecha = dto.Fecha,
                    Actividad = dto.Actividad,
                    Solicitante = dto.Solicitante,
                    Estado = EstadoSolicitudPrestamo.Pendiente
                };

                await _repository.AgregarSolicitudPrestamoAsync(solicitud);
            }
            catch (Exception ex) when (ex is not ValidationException and not NotFoundException)
            {
                throw new Exception("Error al registrar la solicitud de préstamo.", ex);
            }
        }

        public async Task AprobarPrestamoAsync(ResolucionPrestamoDto dto)
        {
            try
            {
                var solicitud = await _repository.ObtenerSolicitudPorIdAsync(dto.SolicitudId)
                    ?? throw new NotFoundException("La solicitud no existe.");

                if (solicitud.Estado != EstadoSolicitudPrestamo.Pendiente)
                    throw new ValidationException("La solicitud ya fue resuelta.");

                var articulo = await _repository.ObtenerArticuloPorIdAsync(solicitud.ArticuloId)
                    ?? throw new NotFoundException("El artículo no existe.");

                // Se revalida acá y no se confía en lo que se comprobó al crear la
                // solicitud, por el mismo motivo que se revalidan el estado y el stock:
                // el dato pudo cambiar en el medio. EditarArticuloAsync permite mover
                // un artículo de categoría, así que un Equipo solicitado la semana
                // pasada puede ser un Alimento hoy. La aprobación es el momento en que
                // el artículo sale físicamente, así que es acá donde tiene que valer.
                //
                // También cubre las solicitudes creadas antes de que existiera la
                // validación del alta: para esas, esta es la única barrera.
                //
                // Si una solicitud queda atrapada por este chequeo, el camino es
                // rechazarla con su motivo, no aprobarla.
                if (articulo.Categoria != CategoriasArticulo.Equipo)
                {
                    throw new ValidationException(
                        $"No se puede aprobar: el artículo ya no es de categoría {CategoriasArticulo.Equipo} " +
                        "y solo se prestan artículos de esa categoría.");
                }

                if (solicitud.Cantidad > articulo.StockActual)
                    throw new ValidationException("El stock disponible ya no alcanza para este préstamo.");

                var salida = new SalidaInventario
                {
                    ArticuloId = solicitud.ArticuloId,
                    Cantidad = solicitud.Cantidad,
                    Fecha = DateTime.Now,
                    TipoSalida = TiposSalidaInventario.Prestamo,
                    SolicitudPrestamoId = solicitud.Id
                };

                solicitud.Estado = EstadoSolicitudPrestamo.Aprobada;
                solicitud.FechaResolucion = DateTime.Now;

                // Una sola llamada: marcar la solicitud como aprobada, registrar la
                // salida y descontar el stock son todo-o-nada. Antes eran tres
                // guardados independientes en este orden: salida, descuento, estado.
                // Si el último fallaba, el préstamo quedaba entregado y descontado con
                // la solicitud todavía en Pendiente, lista para aprobarse otra vez.
                await _repository.AprobarPrestamoConStockAsync(solicitud, salida);
            }
            catch (Exception ex) when (ex is not ValidationException and not NotFoundException)
            {
                throw new Exception("Error al aprobar el préstamo.", ex);
            }
        }

        public async Task RechazarPrestamoAsync(ResolucionPrestamoDto dto)
        {
            try
            {
                var solicitud = await _repository.ObtenerSolicitudPorIdAsync(dto.SolicitudId)
                    ?? throw new NotFoundException("La solicitud no existe.");

                if (solicitud.Estado != EstadoSolicitudPrestamo.Pendiente)
                    throw new ValidationException("La solicitud ya fue resuelta.");

                // Antes no se comprobaba: solo lo impedía el botón deshabilitado del
                // diálogo de rechazo, no el servicio. Un rechazo sin motivo no dice
                // nada de por qué no se prestó el artículo.
                if (string.IsNullOrWhiteSpace(dto.MotivoRechazo))
                    throw new ValidationException("El motivo del rechazo es obligatorio.");

                solicitud.Estado = EstadoSolicitudPrestamo.Rechazada;
                solicitud.FechaResolucion = DateTime.Now;
                solicitud.MotivoRechazo = dto.MotivoRechazo;

                await _repository.ActualizarSolicitudAsync(solicitud);
            }
            catch (Exception ex) when (ex is not ValidationException and not NotFoundException)
            {
                throw new Exception("Error al rechazar el préstamo.", ex);
            }
        }

        // estado opcional: sin él devuelve todas, como antes. Con él, el filtrado se
        // resuelve en SQL apoyado en IX_SolicitudesPrestamo_Estado, en vez de traer
        // el histórico completo para descartarlo en memoria.
        public async Task<IEnumerable<SolicitudPrestamoListaDto>> ObtenerSolicitudesAsync(
            EstadoSolicitudPrestamo? estado = null)
        {
            try
            {
                var solicitudes = await _repository.ObtenerSolicitudesAsync(estado);

                return solicitudes.Select(s => new SolicitudPrestamoListaDto
                {
                    Id = s.Id,
                    Articulo = s.Articulo?.Nombre ?? string.Empty,
                    Cantidad = s.Cantidad,
                    Fecha = s.Fecha,
                    Actividad = s.Actividad,
                    Solicitante = s.Solicitante,
                    Estado = s.Estado.ToString(),
                    FechaResolucion = s.FechaResolucion
                });
            }
            catch (Exception ex)
            {
                throw new Exception("Error al consultar las solicitudes de préstamo.", ex);
            }
        }
    }
}