using SIGAC.Application.DTOs.Inventario;
using SIGAC.Application.Exceptions;
using SIGAC.Application.Interfaces;
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

                var articulo = await _repository.ObtenerArticuloPorNombreAsync(dto.NombreArticulo);

                // El artículo nuevo se arma acá pero NO se guarda por separado: se le
                // pasa al repositorio para que lo cree dentro de la misma transacción
                // que la entrada y el stock. Guardarlo antes, por su cuenta, dejaba un
                // artículo en el catálogo con stock 0 si la entrada fallaba después.
                Articulo? articuloNuevo = articulo is null
                    ? new Articulo
                    {
                        Nombre = dto.NombreArticulo,
                        Categoria = dto.Categoria,
                        UnidadMedida = dto.UnidadMedida,
                        StockActual = 0
                    }
                    : null;

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

                if (dto.Origen.Equals("Donacion", StringComparison.OrdinalIgnoreCase))
                    entrada.DonanteId = dto.DonanteId;
                else if (dto.Origen.Equals("Compra", StringComparison.OrdinalIgnoreCase))
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

                var salida = new SalidaInventario
                {
                    ArticuloId = dto.ArticuloId,
                    Cantidad = dto.Cantidad,
                    Fecha = dto.Fecha,
                    TipoSalida = "Donacion",
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

        public async Task<IEnumerable<ArticuloExistenciaDto>> ObtenerExistenciasAsync(FiltrosExistenciaDto filtros)
        {
            try
            {
                var articulos = await _repository.ObtenerExistenciasAsync(filtros.Nombre, filtros.Categoria);

                return articulos.Select(a => new ArticuloExistenciaDto
                {
                    Id = a.Id,
                    Nombre = a.Nombre,
                    Categoria = a.Categoria,
                    UnidadMedida = a.UnidadMedida,
                    StockActual = a.StockActual,
                    StockBajo = a.StockActual <= a.StockMinimo
                });
            }
            catch (Exception ex)
            {
                throw new Exception("Error al consultar existencias.", ex);
            }
        }

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
                    Categoria = articulo.Categoria,
                    UnidadMedida = articulo.UnidadMedida,
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

                if (string.IsNullOrWhiteSpace(dto.Nombre))
                    throw new ValidationException("El nombre es obligatorio.");

                articulo.Nombre = dto.Nombre;
                articulo.Categoria = dto.Categoria;
                articulo.UnidadMedida = dto.UnidadMedida;

                await _repository.ActualizarArticuloAsync(articulo);
            }
            catch (Exception ex) when (ex is not ValidationException and not NotFoundException)
            {
                throw new Exception("Error al editar el artículo.", ex);
            }
        }

        public async Task<HistorialMovimientosResultadoDto> ObtenerHistorialMovimientosAsync(FiltrosMovimientoDto filtros)
        {
            try
            {
                var entradas = filtros.TipoMovimiento is null or "Entrada"
                    ? await _repository.ObtenerEntradasAsync(filtros.ArticuloId, filtros.Desde, filtros.Hasta)
                    : Enumerable.Empty<EntradaInventario>();

                var salidas = filtros.TipoMovimiento is null or "Salida"
                    ? await _repository.ObtenerSalidasAsync(filtros.ArticuloId, filtros.Desde, filtros.Hasta)
                    : Enumerable.Empty<SalidaInventario>();

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
                    TipoMovimiento = "Salida",
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
                _ = await _repository.ObtenerArticuloPorIdAsync(dto.ArticuloId)
                    ?? throw new NotFoundException("El artículo no existe.");

                if (dto.Cantidad <= 0)
                    throw new ValidationException("La cantidad debe ser mayor a 0.");

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

                if (solicitud.Cantidad > articulo.StockActual)
                    throw new ValidationException("El stock disponible ya no alcanza para este préstamo.");

                var salida = new SalidaInventario
                {
                    ArticuloId = solicitud.ArticuloId,
                    Cantidad = solicitud.Cantidad,
                    Fecha = DateTime.Now,
                    TipoSalida = "Prestamo",
                    SolicitudPrestamoId = solicitud.Id
                };

                solicitud.Estado = EstadoSolicitudPrestamo.Aprobada;

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

                solicitud.Estado = EstadoSolicitudPrestamo.Rechazada;
                solicitud.MotivoRechazo = dto.MotivoRechazo;

                await _repository.ActualizarSolicitudAsync(solicitud);
            }
            catch (Exception ex) when (ex is not ValidationException and not NotFoundException)
            {
                throw new Exception("Error al rechazar el préstamo.", ex);
            }
        }

        public async Task<IEnumerable<SolicitudPrestamoListaDto>> ObtenerSolicitudesAsync()
        {
            try
            {
                var solicitudes = await _repository.ObtenerSolicitudesAsync();

                return solicitudes.Select(s => new SolicitudPrestamoListaDto
                {
                    Id = s.Id,
                    Articulo = s.Articulo?.Nombre ?? string.Empty,
                    Cantidad = s.Cantidad,
                    Fecha = s.Fecha,
                    Actividad = s.Actividad,
                    Solicitante = s.Solicitante,
                    Estado = s.Estado.ToString()
                });
            }
            catch (Exception ex)
            {
                throw new Exception("Error al consultar las solicitudes de préstamo.", ex);
            }
        }
    }
}