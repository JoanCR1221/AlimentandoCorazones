using SIGAC.Application.DTOs;
using SIGAC.Application.DTOs.Alquileres;
using SIGAC.Application.DTOs.Reportes;
using SIGAC.Application.Interfaces;
using SIGAC.Application.Services;
using SIGAC.Domain;
using SIGAC.Domain.Entities;

namespace SIGAC.Tests.Application
{
    // El calendario de alquileres pagina en la base: lo que hace el servicio con lo que
    // le devuelve el repositorio (la página, el total de registros y el ingreso por
    // moneda de todo el conjunto filtrado). El Skip/Take y el GROUP BY en sí se
    // verifican contra SQL Server (ver la nota del commit que los agregó).
    public class AlquileresHistorialPaginadoTests
    {
        private readonly RepositorioAlquileresFalso _repositorio = new();
        private readonly AlquileresService _servicio;

        public AlquileresHistorialPaginadoTests()
        {
            // Arrendatarios, sectores y bitácora no se usan al consultar el calendario.
            _servicio = new AlquileresService(_repositorio, null!, null!, null!);
        }

        private static readonly DateTime Base = new(2026, 1, 1);

        private AlquilerEspacio Agregar(int id, int dia, int horaInicio = 8, decimal monto = 10_000m,
            string moneda = TiposMoneda.Colones, EstadoAlquiler estado = EstadoAlquiler.Reservado,
            int arrendatarioId = 1, params int[] sectores)
        {
            var alquiler = new AlquilerEspacio
            {
                Id = id,
                ArrendatarioId = arrendatarioId,
                Arrendatario = new Arrendatario { Id = arrendatarioId, Nombre = $"Arrendatario {arrendatarioId}" },
                Fecha = Base.AddDays(dia),
                HoraInicio = TimeSpan.FromHours(horaInicio),
                HoraFin = TimeSpan.FromHours(horaInicio + 2),
                CantidadPersonas = 20,
                Monto = monto,
                Moneda = moneda,
                Estado = estado,
                MotivoCancelacion = estado == EstadoAlquiler.Cancelado ? "Se suspendió" : null,
                Espacios = (sectores.Length == 0 ? new[] { 1 } : sectores)
                    .Select(s => new EspacioFisico { Id = s, Nombre = $"Sector {s}" })
                    .ToList()
            };

            _repositorio.Alquileres.Add(alquiler);
            return alquiler;
        }

        [Fact]
        public async Task Devuelve_solo_la_pagina_pedida_y_el_total_de_todo_el_conjunto()
        {
            for (var i = 1; i <= 45; i++) Agregar(i, dia: i);

            var primera = await _servicio.ObtenerPaginaHistorialAlquileresAsync(new FiltrosHistorialAlquilerDto { Pagina = 0 });
            var ultima = await _servicio.ObtenerPaginaHistorialAlquileresAsync(new FiltrosHistorialAlquilerDto { Pagina = 2 });

            Assert.Equal(20, primera.Alquileres.Count);
            Assert.Equal(5, ultima.Alquileres.Count);
            Assert.Equal(45, primera.TotalRegistros);
            Assert.Equal(45, ultima.TotalRegistros);
        }

        [Fact]
        public async Task Va_por_fecha_hora_de_inicio_e_id_sin_repetir_entre_paginas()
        {
            // Fechas y horas repetidas a propósito: el caso que más fácil repite o salta
            // una fila entre páginas.
            for (var i = 1; i <= 60; i++) Agregar(i, dia: i % 7, horaInicio: 8 + i % 3);

            var completo = await _servicio.ObtenerHistorialAlquileresAsync(new FiltrosHistorialAlquilerDto());

            var ids = new List<int>();

            for (var pagina = 0; pagina < 3; pagina++)
            {
                var resultado = await _servicio.ObtenerPaginaHistorialAlquileresAsync(new FiltrosHistorialAlquilerDto { Pagina = pagina });
                ids.AddRange(resultado.Alquileres.Select(a => a.Id));
            }

            Assert.Equal(60, ids.Count);
            Assert.Equal(completo.Alquileres.Select(a => a.Id), ids);
            Assert.Equal(60, ids.Distinct().Count());
        }

        [Fact]
        public async Task Con_la_misma_fecha_y_hora_desempata_por_id_ascendente()
        {
            Agregar(3, dia: 5, horaInicio: 9);
            Agregar(1, dia: 5, horaInicio: 9);
            Agregar(2, dia: 5, horaInicio: 8);

            var resultado = await _servicio.ObtenerPaginaHistorialAlquileresAsync(new FiltrosHistorialAlquilerDto());

            Assert.Equal(new[] { 2, 1, 3 }, resultado.Alquileres.Select(a => a.Id));
        }

        [Fact]
        public async Task El_ingreso_cubre_todo_el_conjunto_y_no_la_pagina()
        {
            // 25 alquileres de 10 000: la página trae 20, pero el ingreso es de los 25.
            for (var i = 1; i <= 25; i++) Agregar(i, dia: i, monto: 10_000m);

            var resultado = await _servicio.ObtenerPaginaHistorialAlquileresAsync(new FiltrosHistorialAlquilerDto());

            Assert.Equal(20, resultado.Alquileres.Count);
            Assert.Equal(250_000m, Assert.Single(resultado.TotalesPorMoneda).Total);
        }

        [Fact]
        public async Task Un_alquiler_cancelado_se_lista_pero_no_genera_ingreso()
        {
            Agregar(1, dia: 1, monto: 10_000m);
            Agregar(2, dia: 2, monto: 50_000m, estado: EstadoAlquiler.Cancelado);

            var resultado = await _servicio.ObtenerPaginaHistorialAlquileresAsync(new FiltrosHistorialAlquilerDto());

            Assert.Equal(2, resultado.TotalRegistros);
            Assert.Equal(10_000m, Assert.Single(resultado.TotalesPorMoneda).Total);
        }

        [Fact]
        public async Task Filtrando_solo_cancelados_no_hay_ingreso()
        {
            Agregar(1, dia: 1);
            Agregar(2, dia: 2, estado: EstadoAlquiler.Cancelado);

            var resultado = await _servicio.ObtenerPaginaHistorialAlquileresAsync(
                new FiltrosHistorialAlquilerDto { Estado = EstadoAlquiler.Cancelado });

            Assert.Equal(new[] { 2 }, resultado.Alquileres.Select(a => a.Id));
            Assert.Equal(1, resultado.TotalRegistros);
            Assert.Empty(resultado.TotalesPorMoneda);
        }

        [Fact]
        public async Task Los_ingresos_salen_en_el_orden_de_las_monedas_del_sistema()
        {
            Agregar(1, dia: 1, moneda: TiposMoneda.Euros);
            Agregar(2, dia: 2, moneda: TiposMoneda.Dolares);
            Agregar(3, dia: 3, moneda: TiposMoneda.Colones);

            var resultado = await _servicio.ObtenerPaginaHistorialAlquileresAsync(new FiltrosHistorialAlquilerDto());

            Assert.Equal(TiposMoneda.Todos, resultado.TotalesPorMoneda.Select(t => t.Moneda));
        }

        [Fact]
        public async Task Los_filtros_de_sector_y_fechas_se_aplican_antes_de_paginar()
        {
            for (var i = 1; i <= 30; i++) Agregar(i, dia: i, arrendatarioId: 1, sectores: i % 2 == 0 ? new[] { 1 } : new[] { 2, 3 });

            var resultado = await _servicio.ObtenerPaginaHistorialAlquileresAsync(new FiltrosHistorialAlquilerDto
            {
                EspacioId = 3,
                FechaDesde = Base.AddDays(1),
                FechaHasta = Base.AddDays(10)
            });

            // El sector 3 solo lo usan los impares; entre los días 1 y 10 (el último incluido): 1, 3, 5, 7 y 9.
            Assert.Equal(5, resultado.TotalRegistros);
            Assert.Equal(new[] { 1, 3, 5, 7, 9 }, resultado.Alquileres.Select(a => a.Id));
        }

        [Fact]
        public async Task Una_fila_muestra_arrendatario_sectores_ordenados_y_estado()
        {
            Agregar(1, dia: 1, estado: EstadoAlquiler.Cancelado, sectores: new[] { 3, 1, 2 });

            var fila = Assert.Single((await _servicio.ObtenerPaginaHistorialAlquileresAsync(new FiltrosHistorialAlquilerDto())).Alquileres);

            Assert.Equal("Arrendatario 1", fila.Arrendatario);
            Assert.Equal(new[] { "Sector 1", "Sector 2", "Sector 3" }, fila.Espacios);
            Assert.Equal(EstadoAlquiler.Cancelado, fila.Estado);
            Assert.Equal("Se suspendió", fila.MotivoCancelacion);
        }

        [Fact]
        public async Task Sin_arrendatario_cargado_la_fila_muestra_su_numero()
        {
            var alquiler = Agregar(1, dia: 1, arrendatarioId: 42);
            alquiler.Arrendatario = null;

            var fila = Assert.Single((await _servicio.ObtenerPaginaHistorialAlquileresAsync(new FiltrosHistorialAlquilerDto())).Alquileres);

            Assert.Equal("Arrendatario #42", fila.Arrendatario);
        }

        [Fact]
        public async Task El_tamano_de_pagina_no_puede_superar_el_maximo()
        {
            for (var i = 1; i <= 150; i++) Agregar(i, dia: i);

            var resultado = await _servicio.ObtenerPaginaHistorialAlquileresAsync(
                new FiltrosHistorialAlquilerDto { TamanoPagina = 100_000 });

            Assert.Equal(FiltrosHistorialAlquilerDto.TamanoPaginaMaximo, resultado.Alquileres.Count);
            Assert.Equal(150, resultado.TotalRegistros);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-5)]
        public async Task Un_tamano_de_pagina_invalido_usa_el_predeterminado(int tamano)
        {
            for (var i = 1; i <= 50; i++) Agregar(i, dia: i);

            var resultado = await _servicio.ObtenerPaginaHistorialAlquileresAsync(
                new FiltrosHistorialAlquilerDto { TamanoPagina = tamano });

            Assert.Equal(FiltrosHistorialAlquilerDto.TamanoPaginaPredeterminado, resultado.Alquileres.Count);
        }

        [Fact]
        public async Task Una_pagina_negativa_se_trata_como_la_primera()
        {
            for (var i = 1; i <= 30; i++) Agregar(i, dia: i);

            var resultado = await _servicio.ObtenerPaginaHistorialAlquileresAsync(
                new FiltrosHistorialAlquilerDto { Pagina = -3 });

            Assert.Equal(1, resultado.Alquileres[0].Id);
        }

        [Fact]
        public async Task Sin_alquileres_devuelve_vacio()
        {
            var resultado = await _servicio.ObtenerPaginaHistorialAlquileresAsync(new FiltrosHistorialAlquilerDto());

            Assert.Empty(resultado.Alquileres);
            Assert.Equal(0, resultado.TotalRegistros);
            Assert.Empty(resultado.TotalesPorMoneda);
        }

        [Fact]
        public async Task El_historial_completo_del_reporte_y_de_la_ocupacion_sigue_ignorando_la_paginacion()
        {
            for (var i = 1; i <= 45; i++) Agregar(i, dia: i);

            var resultado = await _servicio.ObtenerHistorialAlquileresAsync(
                new FiltrosHistorialAlquilerDto { Pagina = 1, TamanoPagina = 10 });

            Assert.Equal(45, resultado.Alquileres.Count);
            Assert.Equal(45, resultado.TotalRegistros);
        }

        [Fact]
        public async Task Un_fallo_del_repositorio_se_informa_con_el_mensaje_del_calendario()
        {
            _repositorio.Falla = true;

            var ex = await Assert.ThrowsAsync<Exception>(() =>
                _servicio.ObtenerPaginaHistorialAlquileresAsync(new FiltrosHistorialAlquilerDto()));

            Assert.Equal("Error al consultar los alquileres.", ex.Message);
        }

        // Imita lo que hace AlquileresRepositoryEfCore: mismos filtros para el
        // historial completo y el paginado (fechas inclusivas porque Fecha es una
        // columna date, sector entre otros y estado) y el mismo orden total (fecha,
        // hora de inicio e Id), con Skip/Take sobre esa lista. El resto de la interfaz
        // no la usa el calendario: si llega a invocarse, que la prueba falle fuerte en
        // vez de devolver datos falsos.
        private sealed class RepositorioAlquileresFalso : IAlquileresRepository
        {
            public List<AlquilerEspacio> Alquileres { get; } = new();
            public bool Falla { get; set; }

            private IEnumerable<AlquilerEspacio> Filtrar(FiltrosHistorialAlquilerDto filtros)
            {
                if (Falla)
                    throw new InvalidOperationException("Falla simulada de la base de datos.");

                return Alquileres
                    .Where(a => filtros.FechaDesde is null || a.Fecha >= filtros.FechaDesde.Value.Date)
                    .Where(a => filtros.FechaHasta is null || a.Fecha <= filtros.FechaHasta.Value.Date)
                    .Where(a => filtros.EspacioId is null || a.Espacios.Any(e => e.Id == filtros.EspacioId))
                    .Where(a => filtros.Estado is null || a.Estado == filtros.Estado);
            }

            private static IEnumerable<AlquilerEspacio> Ordenar(IEnumerable<AlquilerEspacio> alquileres) =>
                alquileres.OrderBy(a => a.Fecha).ThenBy(a => a.HoraInicio).ThenBy(a => a.Id);

            public Task<IReadOnlyList<AlquilerEspacio>> ObtenerHistorialAlquileresAsync(FiltrosHistorialAlquilerDto filtros) =>
                Task.FromResult<IReadOnlyList<AlquilerEspacio>>(Ordenar(Filtrar(filtros)).ToList());

            public Task<ResultadoPaginado<AlquilerEspacio>> ObtenerPaginaHistorialAlquileresAsync(FiltrosHistorialAlquilerDto filtros)
            {
                var filtrados = Filtrar(filtros).ToList();
                var tamano = filtros.TamanoPaginaEfectivo;

                var pagina = Ordenar(filtrados)
                    .Skip(filtros.PaginaEfectiva * tamano)
                    .Take(tamano)
                    .ToList();

                return Task.FromResult(new ResultadoPaginado<AlquilerEspacio>(pagina, filtrados.Count));
            }

            public Task<IReadOnlyList<MontoPorMonedaDto>> ObtenerTotalesPorMonedaAsync(FiltrosHistorialAlquilerDto filtros)
            {
                // En el orden en que aparecen, no el de las monedas del sistema, a
                // propósito: el servicio es quien lo impone.
                IReadOnlyList<MontoPorMonedaDto> totales = Filtrar(filtros)
                    .Where(a => a.Estado == EstadoAlquiler.Reservado)
                    .GroupBy(a => a.Moneda)
                    .Select(g => new MontoPorMonedaDto(g.Key, g.Sum(a => a.Monto)))
                    .ToList();

                return Task.FromResult(totales);
            }

            public Task AgregarAlquilerAsync(AlquilerEspacio alquiler) => throw new NotImplementedException();
            public Task<IReadOnlyList<AlquilerEspacio>> ObtenerChoquesAsync(
                DateTime fecha, TimeSpan horaInicio, TimeSpan horaFin, IReadOnlyCollection<int> espacioIds) => throw new NotImplementedException();
            public Task<AlquilerEspacio?> ObtenerPorIdAsync(int id) => throw new NotImplementedException();
            public Task<bool> CancelarAsync(int id, string motivoCancelacion) => throw new NotImplementedException();
            public Task<IReadOnlyList<AlquilerPanoramaDto>> ObtenerParaPanoramaAsync(int mesesHaciaAtras) => throw new NotImplementedException();
        }
    }
}
