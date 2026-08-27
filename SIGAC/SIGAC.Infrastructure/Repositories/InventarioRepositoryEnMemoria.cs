using System.Collections.Concurrent;
using SIGAC.Application.Interfaces;
using SIGAC.Domain.Entities;

namespace SIGAC.Infrastructure.Repositories
{
    // Implementación TEMPORAL en memoria, solo para desarrollo/pruebas.
    public class InventarioRepositoryEnMemoria : IInventarioRepository
    {
        private readonly ConcurrentDictionary<int, Articulo> _articulos = new();
        private readonly ConcurrentDictionary<int, EntradaInventario> _entradas = new();
        private readonly ConcurrentDictionary<int, SalidaInventario> _salidas = new();
        private readonly ConcurrentDictionary<int, SolicitudPrestamo> _solicitudes = new();

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

        public Task AgregarArticuloAsync(Articulo articulo)
        {
            articulo.Id = _siguienteArticuloId++;
            _articulos[articulo.Id] = articulo;
            return Task.CompletedTask;
        }

        public Task ActualizarArticuloAsync(Articulo articulo)
        {
            _articulos[articulo.Id] = articulo;
            return Task.CompletedTask;
        }

        public Task ActualizarStockAsync(int articuloId, int cantidad)
        {
            if (_articulos.TryGetValue(articuloId, out var articulo))
                articulo.StockActual += cantidad;
            return Task.CompletedTask;
        }

        public Task ReducirStockAsync(int articuloId, int cantidad)
        {
            if (_articulos.TryGetValue(articuloId, out var articulo))
                articulo.StockActual -= cantidad;
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

        public Task AgregarEntradaAsync(EntradaInventario entrada)
        {
            entrada.Id = _siguienteEntradaId++;
            entrada.Articulo = _articulos.GetValueOrDefault(entrada.ArticuloId);
            _entradas[entrada.Id] = entrada;
            return Task.CompletedTask;
        }

        public Task AgregarSalidaAsync(SalidaInventario salida)
        {
            salida.Id = _siguienteSalidaId++;
            salida.Articulo = _articulos.GetValueOrDefault(salida.ArticuloId);
            _salidas[salida.Id] = salida;
            return Task.CompletedTask;
        }

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

        public Task AgregarSolicitudPrestamoAsync(SolicitudPrestamo solicitud)
        {
            solicitud.Id = _siguienteSolicitudId++;
            solicitud.Articulo = _articulos.GetValueOrDefault(solicitud.ArticuloId);
            _solicitudes[solicitud.Id] = solicitud;
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
