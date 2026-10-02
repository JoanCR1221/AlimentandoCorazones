using SIGAC.Application.DTOs;
using SIGAC.Application.DTOs.Bitacora;
using SIGAC.Application.DTOs.Gastos;
using SIGAC.Application.Interfaces;
using SIGAC.Domain;
using SIGAC.Domain.Entities;

namespace SIGAC.Tests.Application
{
    // Repositorios en memoria compartidos por GastosServiceTests y
    // TiposGastoServiceTests. Mismo criterio que los falsos de
    // ProyectosServiceTests: prueban la lógica del servicio, no EF Core.

    internal sealed class BitacoraGastosFalsa : IBitacoraService
    {
        public List<(string Accion, string Modulo, string? Detalle)> Registros { get; } = new();

        public Task RegistrarAsync(string accion, string modulo, string? detalle = null)
        {
            Registros.Add((accion, modulo, detalle));
            return Task.CompletedTask;
        }

        public Task RegistrarDeUsuarioAsync(string? usuarioId, string nombreUsuario, string? rol,
            string accion, string modulo, string? detalle = null) => Task.CompletedTask;

        public Task<ResultadoPaginado<BitacoraAccionDto>> ObtenerBitacoraAsync(FiltrosBitacoraDto filtros) =>
            throw new NotImplementedException();
    }

    internal sealed class RepositorioTiposGastoFalso : ITiposGastoRepository
    {
        public List<TipoGasto> Tipos { get; } = new();

        public TipoGasto Agregar(string nombre, bool activo = true, bool generaInventario = false, string? cuenta = null)
        {
            var tipo = new TipoGasto
            {
                Id = Tipos.Count + 1,
                Nombre = nombre,
                Activo = activo,
                GeneraInventario = generaInventario,
                CuentaContablePorDefecto = cuenta
            };
            Tipos.Add(tipo);
            return tipo;
        }

        // Copias, igual que el repositorio real (AsNoTracking): si el servicio
        // modificara la instancia devuelta sin llamar a ActualizarAsync, la prueba
        // lo notaría.
        public Task<IReadOnlyList<TipoGasto>> ObtenerTodosAsync(bool soloActivos) =>
            Task.FromResult<IReadOnlyList<TipoGasto>>(Tipos
                .Where(t => !soloActivos || t.Activo)
                .OrderBy(t => t.Nombre)
                .Select(Copiar)
                .ToList());

        public Task<TipoGasto?> ObtenerPorIdAsync(int id) =>
            Task.FromResult(Tipos.FirstOrDefault(t => t.Id == id) is { } t ? Copiar(t) : null);

        public Task AgregarAsync(TipoGasto tipo)
        {
            tipo.Id = Tipos.Count + 1;
            Tipos.Add(Copiar(tipo));
            return Task.CompletedTask;
        }

        public Task ActualizarAsync(TipoGasto tipo)
        {
            var existente = Tipos.First(t => t.Id == tipo.Id);
            existente.Nombre = tipo.Nombre;
            existente.GeneraInventario = tipo.GeneraInventario;
            existente.CuentaContablePorDefecto = tipo.CuentaContablePorDefecto;
            return Task.CompletedTask;
        }

        public Task CambiarEstadoAsync(int id, bool activo)
        {
            Tipos.First(t => t.Id == id).Activo = activo;
            return Task.CompletedTask;
        }

        private static TipoGasto Copiar(TipoGasto t) => new()
        {
            Id = t.Id,
            Nombre = t.Nombre,
            Activo = t.Activo,
            GeneraInventario = t.GeneraInventario,
            CuentaContablePorDefecto = t.CuentaContablePorDefecto
        };
    }

    internal sealed class RepositorioGastosFalso : IGastosRepository
    {
        private readonly RepositorioTiposGastoFalso _tipos;

        public RepositorioGastosFalso(RepositorioTiposGastoFalso tipos) => _tipos = tipos;

        public List<GastoOperativo> Gastos { get; } = new();
        public List<int> Anulados { get; } = new();
        public string? UltimaBusquedaProveedores { get; private set; }
        public int? UltimoMaximoProveedores { get; private set; }

        public Task AgregarAsync(GastoOperativo gasto)
        {
            gasto.Id = Gastos.Count + 1;
            Gastos.Add(gasto);
            return Task.CompletedTask;
        }

        public Task<GastoOperativo?> ObtenerPorIdAsync(int id) =>
            Task.FromResult(Gastos.FirstOrDefault(g => g.Id == id));

        public Task ActualizarAsync(GastoOperativo gasto) => Task.CompletedTask;

        public Task<IEnumerable<GastoOperativo>> ObtenerTodosAsync(FiltrosGastoDto filtros)
        {
            foreach (var g in Gastos)
                g.TipoGasto = _tipos.Tipos.First(t => t.Id == g.TipoGastoId);

            return Task.FromResult<IEnumerable<GastoOperativo>>(Gastos
                .Where(g => !filtros.TipoGastoId.HasValue || g.TipoGastoId == filtros.TipoGastoId)
                .Where(g => !filtros.GeneraInventario.HasValue || g.TipoGasto!.GeneraInventario == filtros.GeneraInventario)
                .ToList());
        }

        public Task<IReadOnlyList<string>> BuscarProveedoresAsync(string texto, int maximo)
        {
            UltimaBusquedaProveedores = texto;
            UltimoMaximoProveedores = maximo;

            return Task.FromResult<IReadOnlyList<string>>(Gastos
                .Select(g => g.Proveedor)
                .Where(p => p.Contains(texto, StringComparison.OrdinalIgnoreCase))
                .Distinct()
                .Take(maximo)
                .ToList());
        }

        public Task AnularConEntradasVinculadasAsync(int gastoId, string motivo)
        {
            var gasto = Gastos.First(g => g.Id == gastoId);
            gasto.Estado = EstadoGastoOperativo.Anulado;
            gasto.MotivoAnulacion = motivo;
            Anulados.Add(gastoId);
            return Task.CompletedTask;
        }
    }

    internal static class DatosGasto
    {
        public static GastoOperativoCrearDto Crear(int tipoGastoId) => new()
        {
            TipoGastoId = tipoGastoId,
            Proveedor = "Servicentro La Uruca",
            NumeroFactura = "FAC-001",
            Fecha = DateTime.Today,
            MontoSinIva = 10000m,
            Iva = 1300m,
            Descripcion = "Diésel",
            Responsable = "Ana Rojas"
        };

        public static GastoOperativo Entidad(int id, int tipoGastoId, decimal monto, decimal iva,
            string moneda = TiposMoneda.Colones, EstadoGastoOperativo estado = EstadoGastoOperativo.Activo) => new()
        {
            Id = id,
            TipoGastoId = tipoGastoId,
            Proveedor = $"Proveedor {id}",
            NumeroFactura = $"F-{id}",
            Fecha = DateTime.Today,
            MontoSinIva = monto,
            Iva = iva,
            Moneda = moneda,
            FormaPago = FormasPago.Contado,
            CuentaContable = ReglasGastoOperativo.CuentaContablePorDefecto,
            DescripcionCuenta = ReglasGastoOperativo.DescripcionCuentaPorDefecto,
            Descripcion = "Gasto de prueba",
            Responsable = "Ana Rojas",
            Estado = estado,
            MotivoAnulacion = estado == EstadoGastoOperativo.Anulado ? "Duplicado" : null
        };
    }
}
