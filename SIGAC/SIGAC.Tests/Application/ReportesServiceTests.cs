using SIGAC.Application.DTOs.Donaciones;
using SIGAC.Application.DTOs.Reportes;
using SIGAC.Application.Interfaces;
using SIGAC.Application.Services;
using SIGAC.Domain;
using SIGAC.Domain.Entities;

namespace SIGAC.Tests.Application
{
    // El reporte de gastos, el de donaciones y la agrupación del de beneficiarios:
    // los dos paneles gráficos y el resto del de beneficiarios están cubiertos por
    // verificación manual en el navegador (ver las notas de los commits que los
    // agregaron).
    public class ReportesServiceTests
    {
        private readonly RepositorioTiposGastoFalso _tipos = new();
        private readonly RepositorioGastosFalso _gastos;
        private readonly RepositorioAsistenciaFalso _asistencias = new();
        private readonly RepositorioDonacionesFalso _donaciones = new();
        private readonly ReportesService _servicio;

        private readonly TipoGasto _alquiler;
        private readonly TipoGasto _combustible;

        public ReportesServiceTests()
        {
            _gastos = new RepositorioGastosFalso(_tipos);

            // El reporte de donaciones solo usa el historial del DonacionesService
            // real (así las pruebas cubren el filtro por tipo y el total por moneda
            // de verdad), y ese método solo lee el repositorio: lo demás no se toca.
            var donaciones = new DonacionesService(_donaciones, null!, null!, null!, null!);

            _servicio = new ReportesService(_asistencias, new RepositorioBeneficiariosFalso(), _gastos, donaciones);

            _alquiler = _tipos.Agregar("Alquiler de Equipo");
            _combustible = _tipos.Agregar("Combustible");
        }

        private GastoOperativo AgregarGasto(TipoGasto tipo, string proveedor, int dia, decimal monto, decimal iva,
            string formaPago = FormasPago.Contado, string moneda = TiposMoneda.Colones,
            EstadoGastoOperativo estado = EstadoGastoOperativo.Activo)
        {
            var gasto = DatosGasto.Entidad(_gastos.Gastos.Count + 1, tipo.Id, monto, iva, moneda, estado);
            gasto.Proveedor = proveedor;
            gasto.Fecha = new DateTime(2026, 9, dia);
            gasto.FormaPago = formaPago;
            _gastos.Gastos.Add(gasto);
            return gasto;
        }

        // Una instancia nueva de Beneficiario por asistencia, igual que hace EF con
        // AsNoTracking: dos asistencias de la misma persona traen dos objetos
        // distintos con el mismo Id. Con una sola asistencia por persona (los datos
        // de las primeras pruebas) el error de agrupar por objeto no se notaba.
        private void AgregarAsistencia(int beneficiarioId, string primerNombre, string tiempoComida, int dia)
        {
            _asistencias.Asistencias.Add(new AsistenciaComedor
            {
                Id = _asistencias.Asistencias.Count + 1,
                BeneficiarioId = beneficiarioId,
                Beneficiario = new Beneficiario
                {
                    Id = beneficiarioId,
                    PrimerNombre = primerNombre,
                    PrimerApellido = "Prueba",
                    FechaNacimiento = new DateTime(1990, 1, 1)
                },
                Fecha = new DateTime(2026, 9, dia),
                TiempoComida = tiempoComida
            });
        }

        private void AgregarDonacionDinero(string donante, decimal monto, string moneda, DateTime fecha, string? observaciones = null)
        {
            _donaciones.Dinero.Add(new DonacionDinero
            {
                Id = _donaciones.Dinero.Count + 1,
                Donante = new Donante { Nombre = donante },
                Monto = monto,
                Moneda = moneda,
                Fecha = fecha,
                Observaciones = observaciones
            });
        }

        private void AgregarDonacionEspecie(string donante, DateTime fecha, string articulo, int cantidad)
        {
            _donaciones.Especie.Add(new DonacionEspecie
            {
                Id = _donaciones.Especie.Count + 1,
                Donante = new Donante { Nombre = donante },
                Fecha = fecha,
                Detalles =
                {
                    new DetalleDonacionEspecie { NombreArticulo = articulo, Cantidad = cantidad, UnidadMedida = "kg" }
                }
            });
        }

        private static FiltrosReporteDonacionesDto TodasLasDonaciones() => new();

        [Fact]
        public async Task El_reporte_de_donaciones_une_dinero_y_especie_de_la_mas_reciente_a_la_mas_antigua()
        {
            AgregarDonacionDinero("Ana Mora", 25000m, TiposMoneda.Colones, new DateTime(2026, 9, 3), "Para el comedor");
            AgregarDonacionEspecie("Super Valle", new DateTime(2026, 9, 10), "Arroz", 3);

            var resultado = await _servicio.GenerarReporteDonacionesAsync(TodasLasDonaciones());

            Assert.Equal(2, resultado.Filas.Count);

            var especie = resultado.Filas[0];
            Assert.Equal(new DateTime(2026, 9, 10), especie.Fecha);
            Assert.Equal("Especie", especie.TipoDonacion);
            Assert.Equal("Super Valle", especie.Donante);
            Assert.Equal("3 kg de Arroz", especie.Descripcion);

            // La especie no se valoriza: sin monto ni moneda, no un 0.
            Assert.Null(especie.Monto);
            Assert.Null(especie.Moneda);

            var dinero = resultado.Filas[1];
            Assert.Equal("Dinero", dinero.TipoDonacion);
            Assert.Equal("Ana Mora", dinero.Donante);
            Assert.Equal(25000m, dinero.Monto);
            Assert.Equal(TiposMoneda.Colones, dinero.Moneda);
            Assert.Equal("Para el comedor", dinero.Descripcion);
        }

        [Fact]
        public async Task El_reporte_de_donaciones_cuenta_las_de_cada_clase()
        {
            AgregarDonacionDinero("Ana Mora", 1000m, TiposMoneda.Colones, new DateTime(2026, 9, 1));
            AgregarDonacionDinero("Beto Solís", 2000m, TiposMoneda.Colones, new DateTime(2026, 9, 2));
            AgregarDonacionEspecie("Super Valle", new DateTime(2026, 9, 3), "Arroz", 3);

            var resultado = await _servicio.GenerarReporteDonacionesAsync(TodasLasDonaciones());

            Assert.Equal(2, resultado.CantidadDinero);
            Assert.Equal(1, resultado.CantidadEspecie);
            Assert.Equal(3, resultado.Filas.Count);
        }

        [Fact]
        public async Task El_reporte_de_donaciones_totaliza_el_dinero_por_moneda_sin_mezclarlas()
        {
            AgregarDonacionDinero("Ana Mora", 1000m, TiposMoneda.Colones, new DateTime(2026, 9, 1));
            AgregarDonacionDinero("Beto Solís", 500m, TiposMoneda.Colones, new DateTime(2026, 9, 2));
            AgregarDonacionDinero("Carla Rojas", 20m, TiposMoneda.Dolares, new DateTime(2026, 9, 3));

            var resultado = await _servicio.GenerarReporteDonacionesAsync(TodasLasDonaciones());

            Assert.Equal(2, resultado.TotalesPorMoneda.Count);
            Assert.Equal(1500m, Assert.Single(resultado.TotalesPorMoneda, t => t.Moneda == TiposMoneda.Colones).Total);
            Assert.Equal(20m, Assert.Single(resultado.TotalesPorMoneda, t => t.Moneda == TiposMoneda.Dolares).Total);
        }

        [Fact]
        public async Task El_reporte_de_donaciones_lista_los_totales_en_el_orden_del_catalogo_de_monedas()
        {
            // De la más reciente a la más antigua aparecen euros, dólares y colones:
            // el total tiene que salir al revés, igual que TiposMoneda.Todos.
            AgregarDonacionDinero("Ana Mora", 1000m, TiposMoneda.Colones, new DateTime(2026, 9, 1));
            AgregarDonacionDinero("Carla Rojas", 20m, TiposMoneda.Dolares, new DateTime(2026, 9, 2));
            AgregarDonacionDinero("Dora Vega", 15m, TiposMoneda.Euros, new DateTime(2026, 9, 3));

            var resultado = await _servicio.GenerarReporteDonacionesAsync(TodasLasDonaciones());

            Assert.Equal(TiposMoneda.Todos, resultado.TotalesPorMoneda.Select(t => t.Moneda));
        }

        [Fact]
        public async Task Las_donaciones_en_especie_no_suman_dinero()
        {
            AgregarDonacionEspecie("Super Valle", new DateTime(2026, 9, 3), "Arroz", 3);
            AgregarDonacionEspecie("Super Valle", new DateTime(2026, 9, 4), "Frijoles", 5);

            var resultado = await _servicio.GenerarReporteDonacionesAsync(TodasLasDonaciones());

            Assert.Equal(2, resultado.CantidadEspecie);
            Assert.Equal(0, resultado.CantidadDinero);
            Assert.Empty(resultado.TotalesPorMoneda);
        }

        [Theory]
        [InlineData("Dinero", 1, 0)]
        [InlineData("Especie", 0, 1)]
        [InlineData(null, 1, 1)]
        [InlineData("", 1, 1)]
        public async Task El_reporte_de_donaciones_filtra_por_tipo(string? tipo, int esperadasDinero, int esperadasEspecie)
        {
            AgregarDonacionDinero("Ana Mora", 1000m, TiposMoneda.Colones, new DateTime(2026, 9, 1));
            AgregarDonacionEspecie("Super Valle", new DateTime(2026, 9, 3), "Arroz", 3);

            var resultado = await _servicio.GenerarReporteDonacionesAsync(new FiltrosReporteDonacionesDto { TipoDonacion = tipo });

            Assert.Equal(esperadasDinero, resultado.CantidadDinero);
            Assert.Equal(esperadasEspecie, resultado.CantidadEspecie);
        }

        [Fact]
        public async Task El_reporte_de_donaciones_filtra_por_fechas_e_incluye_el_ultimo_dia()
        {
            AgregarDonacionDinero("Antes", 1m, TiposMoneda.Colones, new DateTime(2026, 8, 31, 23, 0, 0));
            AgregarDonacionDinero("Primer día", 2m, TiposMoneda.Colones, new DateTime(2026, 9, 1, 8, 0, 0));
            AgregarDonacionDinero("Último día", 4m, TiposMoneda.Colones, new DateTime(2026, 9, 30, 14, 30, 0));
            AgregarDonacionDinero("Después", 8m, TiposMoneda.Colones, new DateTime(2026, 10, 1, 0, 0, 0));

            var resultado = await _servicio.GenerarReporteDonacionesAsync(new FiltrosReporteDonacionesDto
            {
                FechaDesde = new DateTime(2026, 9, 1),
                FechaHasta = new DateTime(2026, 9, 30)
            });

            Assert.Equal(new[] { "Último día", "Primer día" }, resultado.Filas.Select(f => f.Donante));
            Assert.Equal(6m, Assert.Single(resultado.TotalesPorMoneda).Total);
        }

        [Fact]
        public async Task El_reporte_de_donaciones_sin_resultados_viene_vacio()
        {
            var resultado = await _servicio.GenerarReporteDonacionesAsync(TodasLasDonaciones());

            Assert.Empty(resultado.Filas);
            Assert.Empty(resultado.TotalesPorMoneda);
            Assert.Equal(0, resultado.CantidadDinero);
            Assert.Equal(0, resultado.CantidadEspecie);
        }

        [Fact]
        public async Task El_reporte_de_donaciones_envuelve_los_errores_del_historial()
        {
            _donaciones.Falla = true;

            var error = await Assert.ThrowsAsync<Exception>(() => _servicio.GenerarReporteDonacionesAsync(TodasLasDonaciones()));

            Assert.Equal("Error al generar el reporte de donaciones.", error.Message);
        }

        [Fact]
        public async Task El_reporte_de_beneficiarios_da_una_fila_por_persona_aunque_asista_varias_veces()
        {
            AgregarAsistencia(1, "Ana", TiemposComida.Desayuno, 1);
            AgregarAsistencia(1, "Ana", TiemposComida.Almuerzo, 1);
            AgregarAsistencia(1, "Ana", TiemposComida.Merienda, 2);
            AgregarAsistencia(2, "Beto", TiemposComida.Desayuno, 1);

            var resultado = await _servicio.GenerarReporteBeneficiariosAsync(new FiltrosReporteBeneficiariosDto());

            Assert.Equal(2, resultado.Filas.Count);

            var ana = Assert.Single(resultado.Filas, f => f.BeneficiarioId == 1);
            Assert.Equal(1, ana.Desayunos);
            Assert.Equal(1, ana.Almuerzos);
            Assert.Equal(1, ana.Meriendas);
            Assert.Equal(3, ana.TotalAsistencias);

            Assert.Equal(1, Assert.Single(resultado.Filas, f => f.BeneficiarioId == 2).TotalAsistencias);
            Assert.Equal(4, resultado.TotalGeneral);
        }

        [Fact]
        public async Task El_reporte_de_beneficiarios_ordena_las_filas_por_nombre()
        {
            AgregarAsistencia(2, "Beto", TiemposComida.Desayuno, 1);
            AgregarAsistencia(1, "Ana", TiemposComida.Desayuno, 1);

            var resultado = await _servicio.GenerarReporteBeneficiariosAsync(new FiltrosReporteBeneficiariosDto());

            Assert.Equal(new[] { 1, 2 }, resultado.Filas.Select(f => f.BeneficiarioId));
        }

        [Fact]
        public async Task Agrupa_por_tipo_de_gasto_y_descripcion_de_cuenta()
        {
            AgregarGasto(_alquiler, "Renta Equipos Salas S.A", 5, 25000m, 3250m);
            AgregarGasto(_alquiler, "Renta Equipos Salas S.A", 17, 29370m, 3818m);
            AgregarGasto(_combustible, "Transgas Liberia S.A", 10, 32000m, 0m);

            var filtros = new FiltrosReporteGastosDto { Mes = 9, Anio = 2026, FormaPago = FormasPago.Contado };
            var reporte = await _servicio.GenerarReporteGastosAsync(filtros);

            Assert.Equal(2, reporte.Grupos.Count);

            var grupoAlquiler = Assert.Single(reporte.Grupos, g => g.TipoGasto == "Alquiler de Equipo");
            Assert.Equal(2, grupoAlquiler.Filas.Count);
            Assert.Equal(54370m, grupoAlquiler.SubtotalMonto);
            Assert.Equal(7068m, grupoAlquiler.SubtotalIva);

            var grupoCombustible = Assert.Single(reporte.Grupos, g => g.TipoGasto == "Combustible");
            Assert.Equal(32000m, grupoCombustible.SubtotalMonto);
            Assert.Equal(0m, grupoCombustible.SubtotalIva);
        }

        [Fact]
        public async Task Ordena_las_filas_de_cada_grupo_por_dia()
        {
            AgregarGasto(_alquiler, "Proveedor B", 27, 1000m, 0m);
            AgregarGasto(_alquiler, "Proveedor A", 1, 2000m, 0m);
            AgregarGasto(_alquiler, "Proveedor C", 11, 3000m, 0m);

            var filtros = new FiltrosReporteGastosDto { Mes = 9, Anio = 2026, FormaPago = FormasPago.Contado };
            var reporte = await _servicio.GenerarReporteGastosAsync(filtros);

            var grupo = Assert.Single(reporte.Grupos);
            Assert.Equal(new[] { 1, 11, 27 }, grupo.Filas.Select(f => f.Dia));
        }

        [Fact]
        public async Task Calcula_el_gran_total_sumando_los_subtotales_de_cada_grupo()
        {
            AgregarGasto(_alquiler, "Renta Equipos Salas S.A", 5, 25000m, 3250m);
            AgregarGasto(_combustible, "Transgas Liberia S.A", 10, 32000m, 1000m);

            var filtros = new FiltrosReporteGastosDto { Mes = 9, Anio = 2026, FormaPago = FormasPago.Contado };
            var reporte = await _servicio.GenerarReporteGastosAsync(filtros);

            Assert.Equal(57000m, reporte.GranTotalMonto);
            Assert.Equal(4250m, reporte.GranTotalIva);
        }

        [Fact]
        public async Task Excluye_los_gastos_anulados()
        {
            AgregarGasto(_alquiler, "Renta Equipos Salas S.A", 5, 25000m, 3250m);
            AgregarGasto(_alquiler, "Renta Equipos Salas S.A", 10, 99999m, 0m, estado: EstadoGastoOperativo.Anulado);

            var filtros = new FiltrosReporteGastosDto { Mes = 9, Anio = 2026, FormaPago = FormasPago.Contado };
            var reporte = await _servicio.GenerarReporteGastosAsync(filtros);

            var grupo = Assert.Single(reporte.Grupos);
            Assert.Equal(25000m, Assert.Single(grupo.Filas).Monto);
        }

        [Fact]
        public async Task Excluye_gastos_de_otro_mes_otra_forma_de_pago_u_otra_moneda()
        {
            AgregarGasto(_alquiler, "Dentro del filtro", 5, 25000m, 0m);
            AgregarGasto(_alquiler, "Otro mes", 5, 1m, 0m).Fecha = new DateTime(2026, 8, 5);
            AgregarGasto(_alquiler, "Otra forma de pago", 5, 1m, 0m, formaPago: FormasPago.Credito);
            AgregarGasto(_alquiler, "Otra moneda", 5, 1m, 0m, moneda: TiposMoneda.Dolares);

            var filtros = new FiltrosReporteGastosDto { Mes = 9, Anio = 2026, FormaPago = FormasPago.Contado };
            var reporte = await _servicio.GenerarReporteGastosAsync(filtros);

            var grupo = Assert.Single(reporte.Grupos);
            var fila = Assert.Single(grupo.Filas);
            Assert.Equal("Dentro del filtro", fila.Proveedor);
        }

        // Fakes mínimos: ReportesService los exige en el constructor, pero el
        // reporte de gastos no los usa. Si algún método de acá llega a
        // invocarse, que la prueba falle fuerte en vez de devolver datos falsos.
        private sealed class RepositorioAsistenciaFalso : IAsistenciaRepository
        {
            public Task AgregarAsync(AsistenciaComedor asistencia) => throw new NotImplementedException();
            public Task<bool> ExisteAsistenciaAsync(int beneficiarioId, DateTime fecha, string tiempoComida) => throw new NotImplementedException();
            public Task<IEnumerable<AsistenciaComedor>> ObtenerAsistenciasDiariasAsync(DateTime fecha) => throw new NotImplementedException();
            public Task<IEnumerable<AsistenciaComedor>> ObtenerHistorialAsync(SIGAC.Application.DTOs.Asistencia.FiltrosAsistenciaDto filtros) => throw new NotImplementedException();
            public List<AsistenciaComedor> Asistencias { get; } = new();

            public Task<IEnumerable<AsistenciaComedor>> ObtenerParaReporteBeneficiariosAsync(FiltrosReporteBeneficiariosDto filtros) =>
                Task.FromResult<IEnumerable<AsistenciaComedor>>(Asistencias);
            public Task<IReadOnlyList<ConteoComidaMensualDto>> ObtenerComidasPorTiempoYMesAsync(int mesesHaciaAtras) => throw new NotImplementedException();
            public Task<IReadOnlyList<ConteoPorMesDto>> ObtenerPersonasAtendidasPorMesAsync(int mesesHaciaAtras) => throw new NotImplementedException();
        }

        // Solo las dos consultas del historial, con el mismo criterio de fechas que
        // DonacionesRepositoryEfCore (el último día entra completo). El resto no lo
        // usa el reporte.
        private sealed class RepositorioDonacionesFalso : IDonacionesRepository
        {
            public List<DonacionDinero> Dinero { get; } = new();
            public List<DonacionEspecie> Especie { get; } = new();
            public bool Falla { get; set; }

            public Task<IEnumerable<DonacionDinero>> ObtenerDonacionesDineroAsync(FiltrosHistorialDonacionDto filtros) =>
                Task.FromResult(Consultar(Dinero, d => d.Fecha, filtros));

            public Task<IEnumerable<DonacionEspecie>> ObtenerDonacionesEspecieAsync(FiltrosHistorialDonacionDto filtros) =>
                Task.FromResult(Consultar(Especie, d => d.Fecha, filtros));

            private IEnumerable<T> Consultar<T>(List<T> donaciones, Func<T, DateTime> fecha, FiltrosHistorialDonacionDto filtros)
            {
                if (Falla)
                    throw new InvalidOperationException("Falla simulada de la base de datos.");

                return donaciones
                    .Where(d => filtros.FechaDesde is null || fecha(d) >= filtros.FechaDesde.Value.Date)
                    .Where(d => filtros.FechaHasta is null || fecha(d) < filtros.FechaHasta.Value.Date.AddDays(1))
                    .ToList();
            }

            public Task AgregarDonacionDineroAsync(DonacionDinero donacion) => throw new NotImplementedException();
            public Task AgregarDonacionEspecieAsync(DonacionEspecie donacion) => throw new NotImplementedException();
            public Task AgregarDonacionEntregadaAsync(DonacionEntregada donacion) => throw new NotImplementedException();
            public Task<IEnumerable<DonacionEntregada>> ObtenerEntregasAsync(FiltrosHistorialEntregaDto filtros) => throw new NotImplementedException();
        }

        private sealed class RepositorioBeneficiariosFalso : SIGAC.Application.Interfaces.IBeneficiariosRepository
        {
            public Task AgregarAsync(Beneficiario beneficiario) => throw new NotImplementedException();
            public Task ActualizarAsync(Beneficiario beneficiario) => throw new NotImplementedException();
            public Task<Beneficiario?> ObtenerPorIdAsync(int id) => throw new NotImplementedException();
            public Task<SIGAC.Application.DTOs.Beneficiarios.BeneficiarioCoincidente?> BuscarPorNombresYFechaAsync(string primerNombre, string segundoNombre, string primerApellido, string segundoApellido, DateTime fechaNacimiento, int? idExcluir = null) => throw new NotImplementedException();
            public Task<SIGAC.Application.DTOs.ResultadoPaginado<Beneficiario>> ObtenerPaginaAsync(SIGAC.Application.DTOs.Beneficiarios.FiltrosBeneficiarioDto filtros) => throw new NotImplementedException();
            public Task<SIGAC.Application.DTOs.Beneficiarios.BeneficiarioCoincidente?> BuscarPorNumIdentidadAsync(string? numIdentidad, int? idExcluir = null) => throw new NotImplementedException();
            public Task CambiarEstadoAsync(int id, bool estado) => throw new NotImplementedException();
            public Task<SIGAC.Application.DTOs.ResumenRegistrosDto> ObtenerResumenAsync() => throw new NotImplementedException();
            public Task<IReadOnlyList<ConteoPorCategoriaDto>> ObtenerConteoPorCategoriaAsync() => throw new NotImplementedException();
            public Task<IReadOnlyList<ConteoPorMesDto>> ObtenerAltasPorMesAsync(int mesesHaciaAtras) => throw new NotImplementedException();
        }
    }
}
