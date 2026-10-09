using SIGAC.Application.DTOs.Asistencia;
using SIGAC.Application.DTOs.Reportes;
using SIGAC.Application.Interfaces;
using SIGAC.Application.Services;
using SIGAC.Domain;
using SIGAC.Domain.Entities;

namespace SIGAC.Tests.Application
{
    // El historial de asistencia pagina en la base: lo que hace el servicio con lo
    // que le devuelve el repositorio (la página, el total de registros y los totales
    // por tiempo de comida del período completo). El Skip/Take y el GROUP BY en sí
    // se verifican contra SQL Server (ver la nota del commit que los agregó).
    public class AsistenciaServiceTests
    {
        private readonly RepositorioAsistenciaFalso _asistencias = new();
        private readonly AsistenciaService _servicio;

        public AsistenciaServiceTests()
        {
            // Beneficiarios y bitácora no se usan al consultar el historial.
            _servicio = new AsistenciaService(_asistencias, null!, null!);
        }

        // Id creciente con la fecha: el más reciente tiene el Id más alto.
        private void Agregar(int cantidad, string tiempoComida = TiemposComida.Almuerzo, int beneficiarioId = 1, int primerDia = 0)
        {
            for (var i = 0; i < cantidad; i++)
            {
                var id = _asistencias.Asistencias.Count + 1;

                _asistencias.Asistencias.Add(new AsistenciaComedor
                {
                    Id = id,
                    BeneficiarioId = beneficiarioId,
                    Beneficiario = new Beneficiario { Id = beneficiarioId, PrimerNombre = $"Persona{beneficiarioId}", PrimerApellido = "Prueba" },
                    Fecha = new DateTime(2026, 1, 1).AddDays(primerDia + i),
                    TiempoComida = tiempoComida
                });
            }
        }

        [Fact]
        public async Task Historial_devuelve_solo_la_pagina_pedida_y_el_total_de_todo_el_periodo()
        {
            Agregar(45);

            var primera = await _servicio.ObtenerHistorialAsistenciaAsync(new FiltrosAsistenciaDto { Pagina = 0 });
            var ultima = await _servicio.ObtenerHistorialAsistenciaAsync(new FiltrosAsistenciaDto { Pagina = 2 });

            Assert.Equal(20, primera.Registros.Count);
            Assert.Equal(5, ultima.Registros.Count);
            Assert.Equal(45, primera.TotalRegistros);
            Assert.Equal(45, ultima.TotalRegistros);
        }

        [Fact]
        public async Task Historial_va_del_mas_reciente_al_mas_antiguo_sin_repetir_entre_paginas()
        {
            Agregar(45);

            var ids = new List<int>();

            for (var pagina = 0; pagina < 3; pagina++)
            {
                var resultado = await _servicio.ObtenerHistorialAsistenciaAsync(new FiltrosAsistenciaDto { Pagina = pagina });
                ids.AddRange(resultado.Registros.Select(r => r.Id));
            }

            Assert.Equal(Enumerable.Range(1, 45).Reverse(), ids);
        }

        [Fact]
        public async Task Historial_desempata_por_id_las_asistencias_del_mismo_dia()
        {
            // Tres tiempos de comida el mismo día: sin un orden total, el Skip/Take
            // podría repetir o saltarse alguna entre una página y otra.
            foreach (var tiempo in TiemposComida.Todos)
                Agregar(1, tiempo, primerDia: 10);

            var resultado = await _servicio.ObtenerHistorialAsistenciaAsync(new FiltrosAsistenciaDto());

            Assert.Equal(new[] { 3, 2, 1 }, resultado.Registros.Select(r => r.Id));
        }

        [Fact]
        public async Task Los_totales_por_tiempo_comida_cubren_el_periodo_completo_y_no_la_pagina()
        {
            Agregar(30, TiemposComida.Desayuno);
            Agregar(10, TiemposComida.Almuerzo);
            Agregar(5, TiemposComida.Merienda);

            var resultado = await _servicio.ObtenerHistorialAsistenciaAsync(new FiltrosAsistenciaDto { Pagina = 0 });

            // La página trae 20 filas, pero los totales son los de las 45.
            Assert.Equal(20, resultado.Registros.Count);
            Assert.Equal(30, resultado.TotalesPorTiempoComida[TiemposComida.Desayuno]);
            Assert.Equal(10, resultado.TotalesPorTiempoComida[TiemposComida.Almuerzo]);
            Assert.Equal(5, resultado.TotalesPorTiempoComida[TiemposComida.Merienda]);
            Assert.Equal(45, resultado.TotalRegistros);
        }

        [Fact]
        public async Task Los_totales_salen_en_el_orden_del_dia_sin_importar_cual_aparece_primero()
        {
            Agregar(2, TiemposComida.Merienda);
            Agregar(2, TiemposComida.Desayuno);
            Agregar(2, TiemposComida.Almuerzo);

            var resultado = await _servicio.ObtenerHistorialAsistenciaAsync(new FiltrosAsistenciaDto());

            Assert.Equal(TiemposComida.Todos, resultado.TotalesPorTiempoComida.Keys);
        }

        [Fact]
        public async Task Un_tiempo_de_comida_desconocido_va_al_final_en_vez_de_perderse()
        {
            Agregar(1, "Cena");
            Agregar(1, TiemposComida.Desayuno);

            var resultado = await _servicio.ObtenerHistorialAsistenciaAsync(new FiltrosAsistenciaDto());

            Assert.Equal(new[] { TiemposComida.Desayuno, "Cena" }, resultado.TotalesPorTiempoComida.Keys);
            Assert.Equal(2, resultado.TotalRegistros);
        }

        [Fact]
        public async Task Los_filtros_se_aplican_antes_de_paginar()
        {
            Agregar(30, TiemposComida.Desayuno, beneficiarioId: 1);
            Agregar(30, TiemposComida.Almuerzo, beneficiarioId: 2);

            var resultado = await _servicio.ObtenerHistorialAsistenciaAsync(new FiltrosAsistenciaDto
            {
                TiempoComida = TiemposComida.Almuerzo,
                Pagina = 1
            });

            // 30 almuerzos: la segunda página tiene 10, no una mezcla con desayunos.
            Assert.Equal(10, resultado.Registros.Count);
            Assert.All(resultado.Registros, r => Assert.Equal(TiemposComida.Almuerzo, r.TiempoComida));
            Assert.Equal(30, resultado.TotalRegistros);
            Assert.Equal(new[] { TiemposComida.Almuerzo }, resultado.TotalesPorTiempoComida.Keys);
        }

        [Fact]
        public async Task El_nombre_del_beneficiario_sale_de_la_navegacion_cargada()
        {
            Agregar(1, beneficiarioId: 7);

            var resultado = await _servicio.ObtenerHistorialAsistenciaAsync(new FiltrosAsistenciaDto());

            Assert.Equal("Persona7 Prueba", Assert.Single(resultado.Registros).NombreBeneficiario);
        }

        [Fact]
        public async Task Sin_registros_devuelve_vacio_y_no_pide_la_pagina()
        {
            var resultado = await _servicio.ObtenerHistorialAsistenciaAsync(new FiltrosAsistenciaDto());

            Assert.Empty(resultado.Registros);
            Assert.Equal(0, resultado.TotalRegistros);
            Assert.Empty(resultado.TotalesPorTiempoComida);
            Assert.Equal(0, _asistencias.PaginasPedidas);
        }

        [Fact]
        public async Task El_tamano_de_pagina_no_puede_superar_el_maximo()
        {
            Agregar(150);

            var resultado = await _servicio.ObtenerHistorialAsistenciaAsync(new FiltrosAsistenciaDto { TamanoPagina = 100_000 });

            Assert.Equal(FiltrosAsistenciaDto.TamanoPaginaMaximo, resultado.Registros.Count);
            Assert.Equal(150, resultado.TotalRegistros);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-5)]
        public async Task Un_tamano_de_pagina_invalido_usa_el_predeterminado(int tamano)
        {
            Agregar(50);

            var resultado = await _servicio.ObtenerHistorialAsistenciaAsync(new FiltrosAsistenciaDto { TamanoPagina = tamano });

            Assert.Equal(FiltrosAsistenciaDto.TamanoPaginaPredeterminado, resultado.Registros.Count);
        }

        [Fact]
        public async Task Una_pagina_negativa_se_trata_como_la_primera()
        {
            Agregar(30);

            var resultado = await _servicio.ObtenerHistorialAsistenciaAsync(new FiltrosAsistenciaDto { Pagina = -3 });

            Assert.Equal(30, resultado.Registros[0].Id);
        }

        [Fact]
        public async Task Una_pagina_mas_alla_del_final_devuelve_vacio_pero_con_el_total()
        {
            Agregar(30);

            var resultado = await _servicio.ObtenerHistorialAsistenciaAsync(new FiltrosAsistenciaDto { Pagina = 9 });

            Assert.Empty(resultado.Registros);
            Assert.Equal(30, resultado.TotalRegistros);
        }

        [Fact]
        public async Task Un_fallo_del_repositorio_se_informa_con_el_mensaje_del_historial()
        {
            _asistencias.Falla = true;

            var ex = await Assert.ThrowsAsync<Exception>(() =>
                _servicio.ObtenerHistorialAsistenciaAsync(new FiltrosAsistenciaDto()));

            Assert.Equal("Error al consultar el historial de asistencia.", ex.Message);
        }

        // Imita lo que hace AsistenciaRepositoryEfCore: mismos filtros para la página
        // y para los totales, orden por fecha y Id descendentes y Skip/Take con los
        // valores saneados. El resto de la interfaz no la usa el historial: si llega
        // a invocarse, que la prueba falle fuerte en vez de devolver datos falsos.
        private sealed class RepositorioAsistenciaFalso : IAsistenciaRepository
        {
            public List<AsistenciaComedor> Asistencias { get; } = new();
            public bool Falla { get; set; }
            public int PaginasPedidas { get; private set; }

            private IEnumerable<AsistenciaComedor> Filtrar(FiltrosAsistenciaDto filtros)
            {
                if (Falla)
                    throw new InvalidOperationException("Falla simulada.");

                var consulta = Asistencias.AsEnumerable();

                if (filtros.BeneficiarioId.HasValue)
                    consulta = consulta.Where(a => a.BeneficiarioId == filtros.BeneficiarioId.Value);

                if (!string.IsNullOrWhiteSpace(filtros.TiempoComida))
                    consulta = consulta.Where(a => a.TiempoComida == filtros.TiempoComida);

                if (filtros.FechaDesde.HasValue)
                    consulta = consulta.Where(a => a.Fecha >= filtros.FechaDesde.Value.Date);

                if (filtros.FechaHasta.HasValue)
                    consulta = consulta.Where(a => a.Fecha <= filtros.FechaHasta.Value.Date);

                return consulta;
            }

            public Task<IReadOnlyList<AsistenciaComedor>> ObtenerPaginaHistorialAsync(FiltrosAsistenciaDto filtros)
            {
                PaginasPedidas++;

                var pagina = Filtrar(filtros)
                    .OrderByDescending(a => a.Fecha)
                    .ThenByDescending(a => a.Id)
                    .Skip(filtros.PaginaEfectiva * filtros.TamanoPaginaEfectivo)
                    .Take(filtros.TamanoPaginaEfectivo)
                    .ToList();

                return Task.FromResult<IReadOnlyList<AsistenciaComedor>>(pagina);
            }

            public Task<IReadOnlyDictionary<string, int>> ObtenerTotalesPorTiempoComidaAsync(FiltrosAsistenciaDto filtros)
            {
                // Sin el orden del día a propósito: el servicio es quien lo impone.
                var totales = Filtrar(filtros)
                    .GroupBy(a => a.TiempoComida)
                    .ToDictionary(g => g.Key, g => g.Count());

                return Task.FromResult<IReadOnlyDictionary<string, int>>(totales);
            }

            public Task AgregarAsync(AsistenciaComedor asistencia) => throw new NotImplementedException();
            public Task<bool> ExisteAsistenciaAsync(int beneficiarioId, DateTime fecha, string tiempoComida) => throw new NotImplementedException();
            public Task<IEnumerable<AsistenciaComedor>> ObtenerAsistenciasDiariasAsync(DateTime fecha) => throw new NotImplementedException();
            public Task<IEnumerable<AsistenciaComedor>> ObtenerParaReporteBeneficiariosAsync(FiltrosReporteBeneficiariosDto filtros) => throw new NotImplementedException();
            public Task<IReadOnlyList<ConteoComidaMensualDto>> ObtenerComidasPorTiempoYMesAsync(int mesesHaciaAtras) => throw new NotImplementedException();
            public Task<IReadOnlyList<ConteoPorMesDto>> ObtenerPersonasAtendidasPorMesAsync(int mesesHaciaAtras) => throw new NotImplementedException();
        }
    }
}
