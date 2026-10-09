using SIGAC.Application.DTOs.Gastos;
using SIGAC.Application.Services;
using SIGAC.Domain;
using SIGAC.Domain.Entities;

namespace SIGAC.Tests.Application
{
    // El listado de gastos pagina en la base: lo que hace el servicio con lo que le
    // devuelve el repositorio (la página, el total de registros y el total acumulado
    // por moneda de todo el conjunto filtrado). El Skip/Take y el GROUP BY en sí se
    // verifican contra SQL Server (ver la nota del commit que los agregó).
    public class GastosListadoPaginadoTests
    {
        private readonly RepositorioTiposGastoFalso _tipos = new();
        private readonly RepositorioGastosFalso _gastos;
        private readonly GastosService _servicio;
        private readonly TipoGasto _combustible;
        private readonly TipoGasto _materiales;

        public GastosListadoPaginadoTests()
        {
            _gastos = new RepositorioGastosFalso(_tipos);
            _servicio = new GastosService(_gastos, _tipos, new BitacoraGastosFalsa());

            _combustible = _tipos.Agregar("Combustible");
            _materiales = _tipos.Agregar("Materiales y Suministros", generaInventario: true);
        }

        private static readonly DateTime Base = new(2026, 1, 1);

        private GastoOperativo Agregar(int id, int dia, decimal monto = 1000m, decimal iva = 130m,
            string moneda = TiposMoneda.Colones, EstadoGastoOperativo estado = EstadoGastoOperativo.Activo,
            TipoGasto? tipo = null, string? proveedor = null, string? factura = null)
        {
            var gasto = DatosGasto.Entidad(id, (tipo ?? _combustible).Id, monto, iva, moneda, estado);
            gasto.Fecha = Base.AddDays(dia);

            if (proveedor is not null)
                gasto.Proveedor = proveedor;

            if (factura is not null)
                gasto.NumeroFactura = factura;

            _gastos.Gastos.Add(gasto);
            return gasto;
        }

        [Fact]
        public async Task Devuelve_solo_la_pagina_pedida_y_el_total_de_todo_el_conjunto()
        {
            for (var i = 1; i <= 45; i++) Agregar(i, dia: i);

            var primera = await _servicio.ObtenerPaginaGastosAsync(new FiltrosGastoDto { Pagina = 0 });
            var ultima = await _servicio.ObtenerPaginaGastosAsync(new FiltrosGastoDto { Pagina = 2 });

            Assert.Equal(20, primera.Gastos.Count);
            Assert.Equal(5, ultima.Gastos.Count);
            Assert.Equal(45, primera.TotalRegistros);
            Assert.Equal(45, ultima.TotalRegistros);
        }

        [Fact]
        public async Task Va_del_mas_reciente_al_mas_antiguo_sin_repetir_entre_paginas()
        {
            for (var i = 1; i <= 45; i++) Agregar(i, dia: i);

            var ids = new List<int>();

            for (var pagina = 0; pagina < 3; pagina++)
            {
                var resultado = await _servicio.ObtenerPaginaGastosAsync(new FiltrosGastoDto { Pagina = pagina });
                ids.AddRange(resultado.Gastos.Select(g => g.Id));
            }

            Assert.Equal(Enumerable.Range(1, 45).Reverse(), ids);
        }

        [Fact]
        public async Task Desempata_por_id_los_gastos_de_la_misma_fecha()
        {
            Agregar(1, dia: 10);
            Agregar(2, dia: 10);
            Agregar(3, dia: 10);

            var resultado = await _servicio.ObtenerPaginaGastosAsync(new FiltrosGastoDto());

            Assert.Equal(new[] { 3, 2, 1 }, resultado.Gastos.Select(g => g.Id));
        }

        [Fact]
        public async Task El_total_acumulado_cubre_todo_el_conjunto_y_no_la_pagina()
        {
            // 25 gastos de 1 000 + 130 de IVA: la página trae 20, pero el total es de los 25.
            for (var i = 1; i <= 25; i++) Agregar(i, dia: i, monto: 1000m, iva: 130m);

            var resultado = await _servicio.ObtenerPaginaGastosAsync(new FiltrosGastoDto());

            Assert.Equal(20, resultado.Gastos.Count);
            Assert.Equal(28_250m, Assert.Single(resultado.TotalesPorMoneda).Total);
        }

        [Fact]
        public async Task Un_gasto_anulado_se_lista_pero_no_suma_al_total()
        {
            Agregar(1, dia: 1, monto: 1000m, iva: 130m);
            Agregar(2, dia: 2, monto: 5000m, iva: 650m, estado: EstadoGastoOperativo.Anulado);

            var resultado = await _servicio.ObtenerPaginaGastosAsync(new FiltrosGastoDto());

            Assert.Equal(2, resultado.TotalRegistros);
            Assert.Equal(1130m, Assert.Single(resultado.TotalesPorMoneda).Total);
            Assert.Contains(resultado.Gastos, g => g.Estado == nameof(EstadoGastoOperativo.Anulado));
        }

        [Fact]
        public async Task Los_totales_salen_en_el_orden_de_las_monedas_del_sistema()
        {
            Agregar(1, dia: 1, moneda: TiposMoneda.Euros);
            Agregar(2, dia: 2, moneda: TiposMoneda.Dolares);
            Agregar(3, dia: 3, moneda: TiposMoneda.Colones);

            var resultado = await _servicio.ObtenerPaginaGastosAsync(new FiltrosGastoDto());

            Assert.Equal(TiposMoneda.Todos, resultado.TotalesPorMoneda.Select(t => t.Moneda));
        }

        [Fact]
        public async Task Sin_gastos_activos_no_hay_totales()
        {
            Agregar(1, dia: 1, estado: EstadoGastoOperativo.Anulado);

            var resultado = await _servicio.ObtenerPaginaGastosAsync(new FiltrosGastoDto());

            Assert.Equal(1, resultado.TotalRegistros);
            Assert.Empty(resultado.TotalesPorMoneda);
        }

        [Fact]
        public async Task El_filtro_de_texto_busca_en_proveedor_y_factura_antes_de_paginar()
        {
            for (var i = 1; i <= 30; i++) Agregar(i, dia: i, proveedor: "Servicentro Norte", factura: $"A-{i}");
            for (var i = 31; i <= 40; i++) Agregar(i, dia: i, proveedor: "Ferretería Sur", factura: $"B-{i}");

            var porProveedor = await _servicio.ObtenerPaginaGastosAsync(new FiltrosGastoDto { Texto = "servicentro", Pagina = 1 });
            var porFactura = await _servicio.ObtenerPaginaGastosAsync(new FiltrosGastoDto { Texto = "B-3" });

            // 30 del proveedor: la segunda página tiene 10, sin mezclar con la ferretería.
            Assert.Equal(30, porProveedor.TotalRegistros);
            Assert.Equal(10, porProveedor.Gastos.Count);
            Assert.All(porProveedor.Gastos, g => Assert.Equal("Servicentro Norte", g.Proveedor));

            // Facturas B-31 a B-39 (y B-3 en sí no existe): las que empiezan con "B-3".
            Assert.Equal(9, porFactura.TotalRegistros);
        }

        [Fact]
        public async Task Los_filtros_de_tipo_y_de_fechas_se_aplican_antes_de_paginar()
        {
            for (var i = 1; i <= 30; i++) Agregar(i, dia: i, tipo: i % 2 == 0 ? _combustible : _materiales);

            var resultado = await _servicio.ObtenerPaginaGastosAsync(new FiltrosGastoDto
            {
                TipoGastoId = _combustible.Id,
                FechaDesde = Base.AddDays(1),
                FechaHasta = Base.AddDays(10)
            });

            // Combustible son los pares; entre los días 1 y 10 (el último completo): 2, 4, 6, 8 y 10.
            Assert.Equal(5, resultado.TotalRegistros);
            Assert.Equal(new[] { 10, 8, 6, 4, 2 }, resultado.Gastos.Select(g => g.Id));
        }

        [Fact]
        public async Task El_filtro_de_genera_inventario_se_aplica_antes_de_paginar()
        {
            for (var i = 1; i <= 30; i++) Agregar(i, dia: i, tipo: i <= 25 ? _combustible : _materiales);

            var resultado = await _servicio.ObtenerPaginaGastosAsync(new FiltrosGastoDto { GeneraInventario = true });

            Assert.Equal(5, resultado.TotalRegistros);
            Assert.All(resultado.Gastos, g => Assert.Equal("Materiales y Suministros", g.TipoGasto));
        }

        [Fact]
        public async Task Una_fila_muestra_el_tipo_el_estado_y_el_total_pagado()
        {
            Agregar(1, dia: 1, monto: 10_000m, iva: 1_300m, moneda: TiposMoneda.Dolares, tipo: _materiales);

            var fila = Assert.Single((await _servicio.ObtenerPaginaGastosAsync(new FiltrosGastoDto())).Gastos);

            Assert.Equal("Materiales y Suministros", fila.TipoGasto);
            Assert.Equal(nameof(EstadoGastoOperativo.Activo), fila.Estado);
            Assert.Equal(11_300m, fila.Total);
            Assert.Equal(TiposMoneda.Dolares, fila.Moneda);
        }

        [Fact]
        public async Task El_tamano_de_pagina_no_puede_superar_el_maximo()
        {
            for (var i = 1; i <= 150; i++) Agregar(i, dia: i);

            var resultado = await _servicio.ObtenerPaginaGastosAsync(new FiltrosGastoDto { TamanoPagina = 100_000 });

            Assert.Equal(FiltrosGastoDto.TamanoPaginaMaximo, resultado.Gastos.Count);
            Assert.Equal(150, resultado.TotalRegistros);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-5)]
        public async Task Un_tamano_de_pagina_invalido_usa_el_predeterminado(int tamano)
        {
            for (var i = 1; i <= 50; i++) Agregar(i, dia: i);

            var resultado = await _servicio.ObtenerPaginaGastosAsync(new FiltrosGastoDto { TamanoPagina = tamano });

            Assert.Equal(FiltrosGastoDto.TamanoPaginaPredeterminado, resultado.Gastos.Count);
        }

        [Fact]
        public async Task Una_pagina_negativa_se_trata_como_la_primera()
        {
            for (var i = 1; i <= 30; i++) Agregar(i, dia: i);

            var resultado = await _servicio.ObtenerPaginaGastosAsync(new FiltrosGastoDto { Pagina = -3 });

            Assert.Equal(30, resultado.Gastos[0].Id);
        }

        [Fact]
        public async Task Sin_gastos_devuelve_vacio()
        {
            var resultado = await _servicio.ObtenerPaginaGastosAsync(new FiltrosGastoDto());

            Assert.Empty(resultado.Gastos);
            Assert.Equal(0, resultado.TotalRegistros);
            Assert.Empty(resultado.TotalesPorMoneda);
        }

        [Fact]
        public async Task La_consulta_completa_del_selector_de_inventario_sigue_ignorando_la_paginacion()
        {
            for (var i = 1; i <= 45; i++) Agregar(i, dia: i);

            var resultado = await _servicio.ObtenerGastosAsync(new FiltrosGastoDto { Pagina = 1, TamanoPagina = 10 });

            Assert.Equal(45, resultado.Gastos.Count);
            Assert.Equal(45, resultado.TotalRegistros);
        }

        [Fact]
        public async Task Un_fallo_del_repositorio_se_informa_con_el_mensaje_del_listado()
        {
            _gastos.Falla = true;

            var ex = await Assert.ThrowsAsync<Exception>(() =>
                _servicio.ObtenerPaginaGastosAsync(new FiltrosGastoDto()));

            Assert.Equal("Error al consultar los gastos operativos.", ex.Message);
        }
    }
}
