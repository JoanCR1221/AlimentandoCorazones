using SIGAC.Application.DTOs;
using SIGAC.Application.DTOs.Donaciones;
using SIGAC.Application.DTOs.Reportes;
using SIGAC.Application.Interfaces;
using SIGAC.Application.Services;
using SIGAC.Domain;
using SIGAC.Domain.Entities;

namespace SIGAC.Tests.Application
{
    // El historial de donaciones pagina en la base: lo que hace el servicio con lo que
    // le devuelve el repositorio (la página mezclada de dinero y especie, el total de
    // registros y los totales por moneda del período completo). El UNION ALL con
    // Skip/Take y los GROUP BY en sí se verifican contra SQL Server (ver la nota del
    // commit que los agregó).
    public class DonacionesHistorialPaginadoTests
    {
        private readonly RepositorioDonacionesFalso _repositorio = new();
        private readonly DonacionesService _servicio;

        public DonacionesHistorialPaginadoTests()
        {
            // Donantes, inventario, beneficiarios y bitácora no se usan al consultar el
            // historial.
            _servicio = new DonacionesService(_repositorio, null!, null!, null!, null!);
        }

        private static readonly DateTime Base = new(2026, 1, 1);

        private void Dinero(int id, int dia, decimal monto = 1000m, string moneda = TiposMoneda.Colones, int donanteId = 1, string? observaciones = null) =>
            _repositorio.Dinero.Add(new DonacionDinero
            {
                Id = id,
                DonanteId = donanteId,
                Donante = new Donante { Id = donanteId, Nombre = $"Donante {donanteId}" },
                Monto = monto,
                Moneda = moneda,
                Fecha = Base.AddDays(dia),
                Observaciones = observaciones
            });

        private void Especie(int id, int dia, string articulo = "Arroz", int cantidad = 3, int donanteId = 1) =>
            _repositorio.Especie.Add(new DonacionEspecie
            {
                Id = id,
                DonanteId = donanteId,
                Donante = new Donante { Id = donanteId, Nombre = $"Donante {donanteId}" },
                Fecha = Base.AddDays(dia),
                Detalles = { new DetalleDonacionEspecie { NombreArticulo = articulo, Cantidad = cantidad, UnidadMedida = "kg" } }
            });

        private static (string Tipo, int Id) Clave(HistorialDonacionDto d) => (d.TipoDonacion, d.Id);

        [Fact]
        public async Task La_pagina_mezcla_dinero_y_especie_de_la_mas_reciente_a_la_mas_antigua()
        {
            Dinero(1, dia: 1);
            Especie(1, dia: 2);
            Dinero(2, dia: 3);
            Especie(2, dia: 4);

            var resultado = await _servicio.ObtenerPaginaHistorialDonacionesAsync(new FiltrosHistorialDonacionDto());

            Assert.Equal(
                new[] { ("Especie", 2), ("Dinero", 2), ("Especie", 1), ("Dinero", 1) },
                resultado.Donaciones.Select(Clave));
        }

        [Fact]
        public async Task Devuelve_solo_la_pagina_pedida_y_el_total_de_todo_el_periodo()
        {
            for (var i = 1; i <= 30; i++) Dinero(i, dia: i);
            for (var i = 1; i <= 15; i++) Especie(i, dia: 100 + i);

            var primera = await _servicio.ObtenerPaginaHistorialDonacionesAsync(new FiltrosHistorialDonacionDto { Pagina = 0 });
            var ultima = await _servicio.ObtenerPaginaHistorialDonacionesAsync(new FiltrosHistorialDonacionDto { Pagina = 2 });

            Assert.Equal(20, primera.Donaciones.Count);
            Assert.Equal(5, ultima.Donaciones.Count);
            Assert.Equal(45, primera.TotalRegistros);
            Assert.Equal(45, ultima.TotalRegistros);
        }

        [Fact]
        public async Task Recorrer_todas_las_paginas_da_lo_mismo_que_el_historial_completo_de_los_reportes()
        {
            // Mezcla con fechas repetidas y ids repetidos entre tipos: el caso que más
            // fácil repite o salta una fila entre páginas.
            for (var i = 1; i <= 40; i++) Dinero(i, dia: i % 9);
            for (var i = 1; i <= 33; i++) Especie(i, dia: i % 9);

            var completo = await _servicio.ObtenerHistorialDonacionesAsync(new FiltrosHistorialDonacionDto());

            var paginadas = new List<(string, int)>();

            for (var pagina = 0; pagina < 5; pagina++)
            {
                var resultado = await _servicio.ObtenerPaginaHistorialDonacionesAsync(
                    new FiltrosHistorialDonacionDto { Pagina = pagina, TamanoPagina = 20 });
                paginadas.AddRange(resultado.Donaciones.Select(Clave));
            }

            Assert.Equal(73, paginadas.Count);
            Assert.Equal(completo.Donaciones.Select(Clave), paginadas);
            Assert.Equal(73, completo.TotalRegistros);
        }

        [Fact]
        public async Task Con_la_misma_fecha_y_el_mismo_id_el_dinero_va_antes_que_la_especie()
        {
            Especie(5, dia: 7);
            Dinero(5, dia: 7);

            var resultado = await _servicio.ObtenerPaginaHistorialDonacionesAsync(new FiltrosHistorialDonacionDto());

            Assert.Equal(new[] { ("Dinero", 5), ("Especie", 5) }, resultado.Donaciones.Select(Clave));
        }

        [Fact]
        public async Task Los_totales_por_moneda_cubren_el_periodo_completo_y_no_la_pagina()
        {
            // 25 donaciones de 100 colones: la página trae 20, pero el total es de las 25.
            for (var i = 1; i <= 25; i++) Dinero(i, dia: i, monto: 100m);
            Dinero(26, dia: 30, monto: 50m, moneda: TiposMoneda.Dolares);

            var resultado = await _servicio.ObtenerPaginaHistorialDonacionesAsync(new FiltrosHistorialDonacionDto());

            Assert.Equal(20, resultado.Donaciones.Count);
            Assert.Equal(26, resultado.TotalRegistros);
            Assert.Equal(2500m, resultado.TotalesPorMoneda.Single(t => t.Moneda == TiposMoneda.Colones).Total);
            Assert.Equal(50m, resultado.TotalesPorMoneda.Single(t => t.Moneda == TiposMoneda.Dolares).Total);
        }

        [Fact]
        public async Task Los_totales_salen_en_el_orden_de_las_monedas_del_sistema()
        {
            Dinero(1, dia: 1, moneda: TiposMoneda.Euros);
            Dinero(2, dia: 2, moneda: TiposMoneda.Dolares);
            Dinero(3, dia: 3, moneda: TiposMoneda.Colones);

            var resultado = await _servicio.ObtenerPaginaHistorialDonacionesAsync(new FiltrosHistorialDonacionDto());

            Assert.Equal(TiposMoneda.Todos, resultado.TotalesPorMoneda.Select(t => t.Moneda));
        }

        [Fact]
        public async Task Solo_especie_no_trae_dinero_ni_totales()
        {
            Dinero(1, dia: 1);
            Especie(1, dia: 2);

            var resultado = await _servicio.ObtenerPaginaHistorialDonacionesAsync(
                new FiltrosHistorialDonacionDto { TipoDonacion = "Especie" });

            Assert.Equal(new[] { ("Especie", 1) }, resultado.Donaciones.Select(Clave));
            Assert.Equal(1, resultado.TotalRegistros);
            Assert.Empty(resultado.TotalesPorMoneda);
        }

        [Fact]
        public async Task Solo_dinero_no_trae_especie()
        {
            Dinero(1, dia: 1);
            Especie(1, dia: 2);

            var resultado = await _servicio.ObtenerPaginaHistorialDonacionesAsync(
                new FiltrosHistorialDonacionDto { TipoDonacion = "Dinero" });

            Assert.Equal(new[] { ("Dinero", 1) }, resultado.Donaciones.Select(Clave));
            Assert.Equal(1, resultado.TotalRegistros);
        }

        [Fact]
        public async Task Un_tipo_desconocido_no_devuelve_nada()
        {
            Dinero(1, dia: 1);
            Especie(1, dia: 2);

            var resultado = await _servicio.ObtenerPaginaHistorialDonacionesAsync(
                new FiltrosHistorialDonacionDto { TipoDonacion = "Cheque" });

            Assert.Empty(resultado.Donaciones);
            Assert.Equal(0, resultado.TotalRegistros);
            Assert.Empty(resultado.TotalesPorMoneda);
        }

        [Fact]
        public async Task Los_filtros_de_donante_y_fechas_se_aplican_antes_de_paginar()
        {
            for (var i = 1; i <= 30; i++) Dinero(i, dia: i, donanteId: i % 2 == 0 ? 1 : 2);
            for (var i = 1; i <= 10; i++) Especie(i, dia: i, donanteId: 1);

            var resultado = await _servicio.ObtenerPaginaHistorialDonacionesAsync(new FiltrosHistorialDonacionDto
            {
                DonanteId = 1,
                FechaDesde = Base.AddDays(1),
                FechaHasta = Base.AddDays(10),
                Pagina = 0
            });

            // Del donante 1 y entre los días 1 y 10: 5 de dinero (los pares) + 10 de especie.
            Assert.Equal(15, resultado.TotalRegistros);
            Assert.Equal(15, resultado.Donaciones.Count);
            Assert.All(resultado.Donaciones, d => Assert.Equal("Donante 1", d.NombreDonante));
        }

        [Fact]
        public async Task Una_fila_de_dinero_muestra_monto_moneda_y_observaciones()
        {
            Dinero(1, dia: 1, monto: 2500.5m, moneda: TiposMoneda.Dolares, observaciones: "Campaña de invierno");

            var fila = Assert.Single((await _servicio.ObtenerPaginaHistorialDonacionesAsync(new FiltrosHistorialDonacionDto())).Donaciones);

            Assert.Equal("Dinero", fila.TipoDonacion);
            Assert.Equal(2500.5m, fila.Monto);
            Assert.Equal(TiposMoneda.Dolares, fila.Moneda);
            Assert.Equal("Campaña de invierno", fila.Descripcion);
            Assert.Equal("Donante 1", fila.NombreDonante);
        }

        [Fact]
        public async Task Una_fila_de_especie_describe_lo_donado_y_no_tiene_monto()
        {
            Especie(1, dia: 1, articulo: "Frijoles", cantidad: 4);

            var fila = Assert.Single((await _servicio.ObtenerPaginaHistorialDonacionesAsync(new FiltrosHistorialDonacionDto())).Donaciones);

            Assert.Equal("Especie", fila.TipoDonacion);
            Assert.Null(fila.Monto);
            Assert.Null(fila.Moneda);
            Assert.Equal("4 kg de Frijoles", fila.Descripcion);
        }

        [Fact]
        public async Task Las_lineas_de_una_donacion_en_especie_se_describen_en_el_orden_en_que_se_registraron()
        {
            // Los Detalles llegan de la base en el orden que le dé su plan a SQL: la
            // descripción tiene que salir igual venga como venga la colección.
            _repositorio.Especie.Add(new DonacionEspecie
            {
                Id = 1,
                DonanteId = 1,
                Donante = new Donante { Id = 1, Nombre = "Donante 1" },
                Fecha = Base,
                Detalles =
                {
                    new DetalleDonacionEspecie { Id = 12, NombreArticulo = "Camisetas", Cantidad = 3, UnidadMedida = "unidades" },
                    new DetalleDonacionEspecie { Id = 11, NombreArticulo = "Arroz", Cantidad = 2, UnidadMedida = "kg" }
                }
            });

            var fila = Assert.Single((await _servicio.ObtenerPaginaHistorialDonacionesAsync(new FiltrosHistorialDonacionDto())).Donaciones);

            Assert.Equal("2 kg de Arroz, 3 unidades de Camisetas", fila.Descripcion);
        }

        [Fact]
        public async Task El_tamano_de_pagina_no_puede_superar_el_maximo()
        {
            for (var i = 1; i <= 150; i++) Dinero(i, dia: i);

            var resultado = await _servicio.ObtenerPaginaHistorialDonacionesAsync(
                new FiltrosHistorialDonacionDto { TamanoPagina = 100_000 });

            Assert.Equal(FiltrosHistorialDonacionDto.TamanoPaginaMaximo, resultado.Donaciones.Count);
            Assert.Equal(150, resultado.TotalRegistros);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-5)]
        public async Task Un_tamano_de_pagina_invalido_usa_el_predeterminado(int tamano)
        {
            for (var i = 1; i <= 50; i++) Dinero(i, dia: i);

            var resultado = await _servicio.ObtenerPaginaHistorialDonacionesAsync(
                new FiltrosHistorialDonacionDto { TamanoPagina = tamano });

            Assert.Equal(FiltrosHistorialDonacionDto.TamanoPaginaPredeterminado, resultado.Donaciones.Count);
        }

        [Fact]
        public async Task Una_pagina_negativa_se_trata_como_la_primera()
        {
            for (var i = 1; i <= 30; i++) Dinero(i, dia: i);

            var resultado = await _servicio.ObtenerPaginaHistorialDonacionesAsync(
                new FiltrosHistorialDonacionDto { Pagina = -3 });

            Assert.Equal(30, resultado.Donaciones[0].Id);
        }

        [Fact]
        public async Task Sin_donaciones_devuelve_vacio()
        {
            var resultado = await _servicio.ObtenerPaginaHistorialDonacionesAsync(new FiltrosHistorialDonacionDto());

            Assert.Empty(resultado.Donaciones);
            Assert.Equal(0, resultado.TotalRegistros);
            Assert.Empty(resultado.TotalesPorMoneda);
        }

        [Fact]
        public async Task El_historial_completo_de_los_reportes_sigue_ignorando_la_paginacion()
        {
            for (var i = 1; i <= 45; i++) Dinero(i, dia: i);

            var resultado = await _servicio.ObtenerHistorialDonacionesAsync(
                new FiltrosHistorialDonacionDto { Pagina = 1, TamanoPagina = 10 });

            Assert.Equal(45, resultado.Donaciones.Count);
            Assert.Equal(45, resultado.TotalRegistros);
        }

        [Fact]
        public async Task Un_fallo_del_repositorio_se_informa_con_el_mensaje_del_historial()
        {
            _repositorio.Falla = true;

            var ex = await Assert.ThrowsAsync<Exception>(() =>
                _servicio.ObtenerPaginaHistorialDonacionesAsync(new FiltrosHistorialDonacionDto()));

            Assert.Equal("Error al consultar el historial de donaciones.", ex.Message);
        }

        // Imita lo que hace DonacionesRepositoryEfCore: mismos filtros para las dos
        // tablas, unión de las claves ordenada por fecha e Id descendentes y, a igual
        // fecha e Id, el dinero antes que la especie, con Skip/Take sobre esa lista.
        // Las consultas completas (las de los reportes) devuelven todo sin paginar.
        private sealed class RepositorioDonacionesFalso : IDonacionesRepository
        {
            public List<DonacionDinero> Dinero { get; } = new();
            public List<DonacionEspecie> Especie { get; } = new();
            public bool Falla { get; set; }

            private IEnumerable<DonacionDinero> DineroFiltrado(FiltrosHistorialDonacionDto f)
            {
                if (Falla)
                    throw new InvalidOperationException("Falla simulada de la base de datos.");

                return Dinero
                    .Where(d => f.DonanteId is null || d.DonanteId == f.DonanteId)
                    .Where(d => f.FechaDesde is null || d.Fecha >= f.FechaDesde.Value.Date)
                    .Where(d => f.FechaHasta is null || d.Fecha < f.FechaHasta.Value.Date.AddDays(1));
            }

            private IEnumerable<DonacionEspecie> EspecieFiltrada(FiltrosHistorialDonacionDto f)
            {
                if (Falla)
                    throw new InvalidOperationException("Falla simulada de la base de datos.");

                return Especie
                    .Where(d => f.DonanteId is null || d.DonanteId == f.DonanteId)
                    .Where(d => f.FechaDesde is null || d.Fecha >= f.FechaDesde.Value.Date)
                    .Where(d => f.FechaHasta is null || d.Fecha < f.FechaHasta.Value.Date.AddDays(1));
            }

            public Task<IEnumerable<DonacionDinero>> ObtenerDonacionesDineroAsync(FiltrosHistorialDonacionDto filtros) =>
                Task.FromResult<IEnumerable<DonacionDinero>>(DineroFiltrado(filtros).ToList());

            public Task<IEnumerable<DonacionEspecie>> ObtenerDonacionesEspecieAsync(FiltrosHistorialDonacionDto filtros) =>
                Task.FromResult<IEnumerable<DonacionEspecie>>(EspecieFiltrada(filtros).ToList());

            public Task<ResultadoPaginado<ItemHistorialDonacion>> ObtenerPaginaHistorialAsync(FiltrosHistorialDonacionDto filtros)
            {
                var items = new List<(int Tipo, int Id, DateTime Fecha, ItemHistorialDonacion Item)>();

                if (filtros.IncluyeDinero)
                    items.AddRange(DineroFiltrado(filtros).Select(d => (0, d.Id, d.Fecha, new ItemHistorialDonacion(d, null))));

                if (filtros.IncluyeEspecie)
                    items.AddRange(EspecieFiltrada(filtros).Select(d => (1, d.Id, d.Fecha, new ItemHistorialDonacion(null, d))));

                var tamano = filtros.TamanoPaginaEfectivo;

                var pagina = items
                    .OrderByDescending(i => i.Fecha).ThenByDescending(i => i.Id).ThenBy(i => i.Tipo)
                    .Skip(filtros.PaginaEfectiva * tamano)
                    .Take(tamano)
                    .Select(i => i.Item)
                    .ToList();

                return Task.FromResult(new ResultadoPaginado<ItemHistorialDonacion>(pagina, items.Count));
            }

            public Task<IReadOnlyList<MontoPorMonedaDto>> ObtenerTotalesDineroPorMonedaAsync(FiltrosHistorialDonacionDto filtros)
            {
                // En el orden en que aparecen, no el de las monedas del sistema, a
                // propósito: el servicio es quien lo impone.
                IReadOnlyList<MontoPorMonedaDto> totales = !filtros.IncluyeDinero
                    ? Array.Empty<MontoPorMonedaDto>()
                    : DineroFiltrado(filtros)
                        .GroupBy(d => d.Moneda)
                        .Select(g => new MontoPorMonedaDto(g.Key, g.Sum(d => d.Monto)))
                        .ToList();

                return Task.FromResult(totales);
            }

            public Task AgregarDonacionDineroAsync(DonacionDinero donacion) => throw new NotImplementedException();
            public Task AgregarDonacionEspecieAsync(DonacionEspecie donacion) => throw new NotImplementedException();
            public Task AgregarDonacionEntregadaAsync(DonacionEntregada donacion) => throw new NotImplementedException();
            public Task<IEnumerable<DonacionEntregada>> ObtenerEntregasAsync(FiltrosHistorialEntregaDto filtros) => throw new NotImplementedException();
            public Task<IReadOnlyList<ConteoDonacionMensualDto>> ObtenerCantidadPorMesAsync(int mesesHaciaAtras) => throw new NotImplementedException();
            public Task<IReadOnlyList<MontoPorMesDto>> ObtenerDineroEnColonesPorMesAsync(int mesesHaciaAtras) => throw new NotImplementedException();
            public Task<IReadOnlyList<DonacionesPorDonanteDto>> ObtenerTopDonantesAsync(int mesesHaciaAtras, int maximo) => throw new NotImplementedException();
        }
    }
}
