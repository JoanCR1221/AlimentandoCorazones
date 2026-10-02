using SIGAC.Application.DTOs;
using SIGAC.Application.DTOs.Bitacora;
using SIGAC.Application.DTOs.Gastos;
using SIGAC.Application.DTOs.Reportes;
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

        public Task<IEnumerable<GastoOperativo>> ObtenerParaReporteAsync(int mes, int anio, string formaPago)
        {
            foreach (var g in Gastos)
                g.TipoGasto = _tipos.Tipos.First(t => t.Id == g.TipoGastoId);

            return Task.FromResult<IEnumerable<GastoOperativo>>(Gastos
                .Where(g => g.Estado == EstadoGastoOperativo.Activo
                    && g.FormaPago == formaPago
                    && g.Moneda == TiposMoneda.Colones
                    && g.Fecha.Month == mes
                    && g.Fecha.Year == anio)
                .ToList());
        }

        // Panorama gráfico de Gastos: mismo recorte que el repositorio real
        // (activos, en colones, dentro de la ventana), pero en memoria.
        public Task<IReadOnlyList<MontoPorTipoDto>> ObtenerMontoPorTipoAsync(int mesesHaciaAtras)
        {
            foreach (var g in Gastos)
                g.TipoGasto = _tipos.Tipos.First(t => t.Id == g.TipoGastoId);

            return Task.FromResult<IReadOnlyList<MontoPorTipoDto>>(GastosActivosEnColones(mesesHaciaAtras)
                .GroupBy(g => g.TipoGasto!.Nombre)
                .Select(g => new MontoPorTipoDto(g.Key, g.Sum(x => x.MontoSinIva + x.Iva)))
                .OrderByDescending(f => f.Monto)
                .ToList());
        }

        public Task<IReadOnlyList<MontoPorFormaPagoDto>> ObtenerMontoPorFormaPagoAsync(int mesesHaciaAtras) =>
            Task.FromResult<IReadOnlyList<MontoPorFormaPagoDto>>(GastosActivosEnColones(mesesHaciaAtras)
                .GroupBy(g => g.FormaPago)
                .Select(g => new MontoPorFormaPagoDto(g.Key, g.Sum(x => x.MontoSinIva + x.Iva)))
                .ToList());

        public Task<IReadOnlyList<MontoPorMesDto>> ObtenerMontoPorMesAsync(int mesesHaciaAtras) =>
            Task.FromResult<IReadOnlyList<MontoPorMesDto>>(GastosActivosEnColones(mesesHaciaAtras)
                .GroupBy(g => new { g.Fecha.Year, g.Fecha.Month })
                .Select(g => new MontoPorMesDto(g.Key.Year, g.Key.Month, g.Sum(x => x.MontoSinIva + x.Iva)))
                .OrderBy(f => f.Anio).ThenBy(f => f.Mes)
                .ToList());

        public Task<IReadOnlyList<ConteoPorMesDto>> ObtenerCantidadPorMesAsync(int mesesHaciaAtras) =>
            Task.FromResult<IReadOnlyList<ConteoPorMesDto>>(Gastos
                .Where(g => g.Estado == EstadoGastoOperativo.Activo && g.Fecha >= InicioVentana(mesesHaciaAtras))
                .GroupBy(g => new { g.Fecha.Year, g.Fecha.Month })
                .Select(g => new ConteoPorMesDto(g.Key.Year, g.Key.Month, g.Count()))
                .OrderBy(f => f.Anio).ThenBy(f => f.Mes)
                .ToList());

        public Task<IReadOnlyList<MontoPorProveedorDto>> ObtenerTopProveedoresAsync(int mesesHaciaAtras, int maximo) =>
            Task.FromResult<IReadOnlyList<MontoPorProveedorDto>>(GastosActivosEnColones(mesesHaciaAtras)
                .GroupBy(g => g.Proveedor)
                .Select(g => new MontoPorProveedorDto(g.Key, g.Sum(x => x.MontoSinIva + x.Iva)))
                .OrderByDescending(f => f.Monto)
                .Take(maximo)
                .ToList());

        public Task<(int Activos, int Anulados)> ObtenerConteoPorEstadoAsync(int mesesHaciaAtras)
        {
            var ventana = Gastos.Where(g => g.Fecha >= InicioVentana(mesesHaciaAtras)).ToList();

            return Task.FromResult((
                ventana.Count(g => g.Estado == EstadoGastoOperativo.Activo),
                ventana.Count(g => g.Estado == EstadoGastoOperativo.Anulado)));
        }

        private IEnumerable<GastoOperativo> GastosActivosEnColones(int mesesHaciaAtras)
        {
            var inicioVentana = InicioVentana(mesesHaciaAtras);
            return Gastos.Where(g =>
                g.Estado == EstadoGastoOperativo.Activo &&
                g.Moneda == TiposMoneda.Colones &&
                g.Fecha >= inicioVentana);
        }

        private static DateTime InicioVentana(int mesesHaciaAtras)
        {
            var inicioMesActual = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            return inicioMesActual.AddMonths(-(mesesHaciaAtras - 1));
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
