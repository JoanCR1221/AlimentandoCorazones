using System.Collections.Concurrent;
using SIGAC.Application.Exceptions;
using SIGAC.Application.Interfaces;
using SIGAC.Domain.Entities;

namespace SIGAC.Infrastructure.Repositories
{
    // Implementación TEMPORAL en memoria, solo para desarrollo/pruebas.
    // Ya no está registrada en Program.cs: la reemplazó InventarioRepositoryEfCore.
    public class InventarioRepositoryEnMemoria : IInventarioRepository
    {
        private readonly ConcurrentDictionary<int, Articulo> _articulos = new();
        private readonly ConcurrentDictionary<int, EntradaInventario> _entradas = new();
        private readonly ConcurrentDictionary<int, SalidaInventario> _salidas = new();
        private readonly ConcurrentDictionary<int, SolicitudPrestamo> _solicitudes = new();

        // Sustituto de la transacción de la base: las operaciones compuestas se
        // ejecutan enteras bajo este lock, así ningún otro hilo ve el estado a medio
        // aplicar. No hay rollback real, pero como todos los pasos son en memoria y
        // no fallan por I/O, alcanza para que esta implementación se comporte como la
        // de EF Core de cara al servicio.
        private readonly object _candado = new();

        private int _siguienteArticuloId = 1;
        private int _siguienteEntradaId = 1;
        private int _siguienteSalidaId = 1;
        private int _siguienteSolicitudId = 1;

        public Task<Articulo?> ObtenerArticuloPorNombreAsync(string nombre)
        {
            var articulo = _articulos.Values.FirstOrDefault(a =>
                string.Equals(a.Nombre, nombre, StringComparison.OrdinalIgnoreCase));
            return Task.FromResult(articulo);
        }

        public Task<Articulo?> ObtenerArticuloPorIdAsync(int id)
        {
            _articulos.TryGetValue(id, out var articulo);
            return Task.FromResult(articulo);
        }

        public Task ActualizarArticuloAsync(Articulo articulo)
        {
            _articulos[articulo.Id] = articulo;
            return Task.CompletedTask;
        }

        public Task<IEnumerable<Articulo>> ObtenerExistenciasAsync(string? nombre, string? categoria)
        {
            var query = _articulos.Values.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(nombre))
                query = query.Where(a => a.Nombre.Contains(nombre, StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrWhiteSpace(categoria))
                query = query.Where(a => a.Categoria == categoria);

            return Task.FromResult(query);
        }

        // ------------------------------------------------------------------
        // Movimientos que mueven stock (operaciones compuestas)
        // ------------------------------------------------------------------

        public Task RegistrarEntradaConStockAsync(EntradaInventario entrada, Articulo? articuloNuevo)
        {
            lock (_candado)
            {
                if (articuloNuevo is not null)
                {
                    articuloNuevo.Id = _siguienteArticuloId++;
                    _articulos[articuloNuevo.Id] = articuloNuevo;
                    entrada.ArticuloId = articuloNuevo.Id;
                }

                if (!_articulos.TryGetValue(entrada.ArticuloId, out var articulo))
                    throw new NotFoundException("El artículo no existe.");

                entrada.Id = _siguienteEntradaId++;
                entrada.Articulo = articulo;
                _entradas[entrada.Id] = entrada;

                articulo.StockActual += entrada.Cantidad;
            }

            return Task.CompletedTask;
        }

        public Task RegistrarSalidaConStockAsync(SalidaInventario salida)
        {
            lock (_candado)
            {
                RegistrarSalidaYDescontar(salida);
            }

            return Task.CompletedTask;
        }

        public Task AprobarPrestamoConStockAsync(SolicitudPrestamo solicitud, SalidaInventario salida)
        {
            lock (_candado)
            {
                if (!_solicitudes.TryGetValue(solicitud.Id, out var existente))
                    throw new NotFoundException("La solicitud no existe.");

                // Mismo re-chequeo dentro de la "transacción" que hace la versión de
                // EF Core: el estado pudo cambiar desde que lo validó el servicio.
                if (existente.Estado != EstadoSolicitudPrestamo.Pendiente)
                    throw new ValidationException("La solicitud ya fue resuelta.");

                RegistrarSalidaYDescontar(salida);

                existente.Estado = solicitud.Estado;
                existente.MotivoRechazo = solicitud.MotivoRechazo;
            }

            return Task.CompletedTask;
        }

        // Se llama siempre con _candado tomado.
        private void RegistrarSalidaYDescontar(SalidaInventario salida)
        {
            if (!_articulos.TryGetValue(salida.ArticuloId, out var articulo))
                throw new NotFoundException("El artículo no existe.");

            if (articulo.StockActual < salida.Cantidad)
            {
                throw new ValidationException(
                    "El stock disponible cambió mientras se registraba el movimiento y ya no alcanza. " +
                    "Volvé a intentarlo.");
            }

            salida.Id = _siguienteSalidaId++;
            salida.Articulo = articulo;
            _salidas[salida.Id] = salida;

            articulo.StockActual -= salida.Cantidad;
        }

        // ------------------------------------------------------------------
        // Consultas de movimientos
        // ------------------------------------------------------------------

        public Task<IEnumerable<EntradaInventario>> ObtenerEntradasAsync(int? articuloId, DateTime? desde, DateTime? hasta)
        {
            var query = _entradas.Values.AsEnumerable();

            if (articuloId.HasValue)
                query = query.Where(e => e.ArticuloId == articuloId.Value);

            if (desde.HasValue)
                query = query.Where(e => e.Fecha.Date >= desde.Value.Date);

            if (hasta.HasValue)
                query = query.Where(e => e.Fecha.Date <= hasta.Value.Date);

            return Task.FromResult(query);
        }

        public Task<IEnumerable<SalidaInventario>> ObtenerSalidasAsync(int? articuloId, DateTime? desde, DateTime? hasta)
        {
            var query = _salidas.Values.AsEnumerable();

            if (articuloId.HasValue)
                query = query.Where(s => s.ArticuloId == articuloId.Value);

            if (desde.HasValue)
                query = query.Where(s => s.Fecha.Date >= desde.Value.Date);

            if (hasta.HasValue)
                query = query.Where(s => s.Fecha.Date <= hasta.Value.Date);

            return Task.FromResult(query);
        }

        // ------------------------------------------------------------------
        // Préstamos
        // ------------------------------------------------------------------

        public Task AgregarSolicitudPrestamoAsync(SolicitudPrestamo solicitud)
        {
            lock (_candado)
            {
                solicitud.Id = _siguienteSolicitudId++;
                solicitud.Articulo = _articulos.GetValueOrDefault(solicitud.ArticuloId);
                _solicitudes[solicitud.Id] = solicitud;
            }

            return Task.CompletedTask;
        }

        public Task<SolicitudPrestamo?> ObtenerSolicitudPorIdAsync(int id)
        {
            _solicitudes.TryGetValue(id, out var solicitud);
            return Task.FromResult(solicitud);
        }

        public Task ActualizarSolicitudAsync(SolicitudPrestamo solicitud)
        {
            _solicitudes[solicitud.Id] = solicitud;
            return Task.CompletedTask;
        }

        public Task<IEnumerable<SolicitudPrestamo>> ObtenerSolicitudesAsync()
        {
            return Task.FromResult(_solicitudes.Values.AsEnumerable());
        }
    }
}
