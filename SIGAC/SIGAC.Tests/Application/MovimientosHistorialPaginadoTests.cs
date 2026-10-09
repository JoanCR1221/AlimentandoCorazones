using SIGAC.Application.DTOs;
using SIGAC.Application.DTOs.Inventario;
using SIGAC.Application.DTOs.Reportes;
using SIGAC.Application.Interfaces;
using SIGAC.Application.Services;
using SIGAC.Domain;
using SIGAC.Domain.Entities;

namespace SIGAC.Tests.Application
{
    // El historial de movimientos de inventario pagina en la base: lo que hace el
    // servicio con lo que le devuelve el repositorio (la página mezclada de entradas y
    // salidas, el total de registros y los totales en unidades del período completo).
    // El UNION ALL con Skip/Take y las sumas en sí se verifican contra SQL Server (ver
    // la nota del commit que los agregó).
    public class MovimientosHistorialPaginadoTests
    {
        private readonly RepositorioInventarioFalso _repositorio = new();
        private readonly InventarioService _servicio;

        public MovimientosHistorialPaginadoTests()
        {
            // La bitácora no se usa al consultar el historial.
            _servicio = new InventarioService(_repositorio, null!);
        }

        private static readonly DateTime Base = new(2026, 1, 1);

        private void Entrada(int id, int dia, int cantidad = 10, int articuloId = 1, string origen = "Compra") =>
            _repositorio.Entradas.Add(new EntradaInventario
            {
                Id = id,
                ArticuloId = articuloId,
                Articulo = new Articulo { Id = articuloId, Nombre = $"Articulo {articuloId}" },
                Cantidad = cantidad,
                Fecha = Base.AddDays(dia),
                Origen = origen
            });

        private void Salida(int id, int dia, int cantidad = 4, int articuloId = 1,
            string tipo = TiposSalidaInventario.Donacion, string? comunidad = null) =>
            _repositorio.Salidas.Add(new SalidaInventario
            {
                Id = id,
                ArticuloId = articuloId,
                Articulo = new Articulo { Id = articuloId, Nombre = $"Articulo {articuloId}" },
                Cantidad = cantidad,
                Fecha = Base.AddDays(dia),
                TipoSalida = tipo,
                ComunidadDestinataria = comunidad
            });

        private static (string Tipo, int Id) Clave(MovimientoInventarioDto m) => (m.TipoMovimiento, m.Id);

        [Fact]
        public async Task La_pagina_mezcla_entradas_y_salidas_del_mas_reciente_al_mas_antiguo()
        {
            Entrada(1, dia: 1);
            Salida(1, dia: 2);
            Entrada(2, dia: 3);
            Salida(2, dia: 4, tipo: TiposSalidaInventario.Prestamo);

            var resultado = await _servicio.ObtenerPaginaHistorialMovimientosAsync(new FiltrosMovimientoDto());

            Assert.Equal(
                new[] { ("Prestamo", 2), ("Entrada", 2), ("Donacion", 1), ("Entrada", 1) },
                resultado.Movimientos.Select(Clave));
        }

        [Fact]
        public async Task Devuelve_solo_la_pagina_pedida_y_el_total_de_todo_el_periodo()
        {
            for (var i = 1; i <= 30; i++) Entrada(i, dia: i);
            for (var i = 1; i <= 15; i++) Salida(i, dia: 100 + i);

            var primera = await _servicio.ObtenerPaginaHistorialMovimientosAsync(new FiltrosMovimientoDto { Pagina = 0 });
            var ultima = await _servicio.ObtenerPaginaHistorialMovimientosAsync(new FiltrosMovimientoDto { Pagina = 2 });

            Assert.Equal(20, primera.Movimientos.Count);
            Assert.Equal(5, ultima.Movimientos.Count);
            Assert.Equal(45, primera.TotalRegistros);
            Assert.Equal(45, ultima.TotalRegistros);
        }

        [Fact]
        public async Task Recorrer_todas_las_paginas_da_lo_mismo_que_el_historial_completo_de_los_reportes()
        {
            // Mezcla con fechas repetidas y ids repetidos entre tipos: el caso que más
            // fácil repite o salta una fila entre páginas.
            for (var i = 1; i <= 40; i++) Entrada(i, dia: i % 9);
            for (var i = 1; i <= 33; i++) Salida(i, dia: i % 9, tipo: i % 2 == 0 ? TiposSalidaInventario.Donacion : TiposSalidaInventario.Prestamo);

            var completo = await _servicio.ObtenerHistorialMovimientosAsync(new FiltrosMovimientoDto());

            var paginados = new List<(string, int)>();

            for (var pagina = 0; pagina < 5; pagina++)
            {
                var resultado = await _servicio.ObtenerPaginaHistorialMovimientosAsync(
                    new FiltrosMovimientoDto { Pagina = pagina, TamanoPagina = 20 });
                paginados.AddRange(resultado.Movimientos.Select(Clave));
            }

            Assert.Equal(73, paginados.Count);
            Assert.Equal(completo.Movimientos.Select(Clave), paginados);
            Assert.Equal(73, completo.TotalRegistros);
        }

        [Fact]
        public async Task Con_la_misma_fecha_la_entrada_va_antes_que_la_salida_y_dentro_de_cada_una_por_id_descendente()
        {
            Salida(9, dia: 7);
            Entrada(1, dia: 7);
            Entrada(2, dia: 7);
            Salida(10, dia: 7, tipo: TiposSalidaInventario.Prestamo);

            var resultado = await _servicio.ObtenerPaginaHistorialMovimientosAsync(new FiltrosMovimientoDto());

            Assert.Equal(
                new[] { ("Entrada", 2), ("Entrada", 1), ("Prestamo", 10), ("Donacion", 9) },
                resultado.Movimientos.Select(Clave));
        }

        [Fact]
        public async Task Los_totales_son_unidades_de_todo_el_periodo_y_no_de_la_pagina()
        {
            // 25 entradas de 10 unidades: la página trae 20 filas, pero el total es de las 25.
            for (var i = 1; i <= 25; i++) Entrada(i, dia: i, cantidad: 10);
            for (var i = 1; i <= 3; i++) Salida(i, dia: 40 + i, cantidad: 7);

            var resultado = await _servicio.ObtenerPaginaHistorialMovimientosAsync(new FiltrosMovimientoDto());

            Assert.Equal(20, resultado.Movimientos.Count);
            Assert.Equal(28, resultado.TotalRegistros);
            Assert.Equal(250, resultado.TotalEntradas);
            Assert.Equal(21, resultado.TotalSalidas);
        }

        [Fact]
        public async Task Solo_entradas_no_trae_salidas_ni_su_total()
        {
            Entrada(1, dia: 1, cantidad: 10);
            Salida(1, dia: 2, cantidad: 4);

            var resultado = await _servicio.ObtenerPaginaHistorialMovimientosAsync(
                new FiltrosMovimientoDto { TipoMovimiento = "Entrada" });

            Assert.Equal(new[] { ("Entrada", 1) }, resultado.Movimientos.Select(Clave));
            Assert.Equal(10, resultado.TotalEntradas);
            Assert.Equal(0, resultado.TotalSalidas);
            Assert.Equal(1, resultado.TotalRegistros);
        }

        [Theory]
        [InlineData(TiposSalidaInventario.Donacion)]
        [InlineData(TiposSalidaInventario.Prestamo)]
        public async Task Un_subtipo_de_salida_trae_solo_las_salidas_de_ese_tipo(string tipo)
        {
            Entrada(1, dia: 1);
            Salida(1, dia: 2, tipo: TiposSalidaInventario.Donacion);
            Salida(2, dia: 3, tipo: TiposSalidaInventario.Prestamo);

            var resultado = await _servicio.ObtenerPaginaHistorialMovimientosAsync(
                new FiltrosMovimientoDto { TipoMovimiento = tipo });

            var fila = Assert.Single(resultado.Movimientos);
            Assert.Equal(tipo, fila.TipoMovimiento);
            Assert.Equal(1, resultado.TotalRegistros);
            Assert.Equal(0, resultado.TotalEntradas);
            Assert.Equal(4, resultado.TotalSalidas);
        }

        [Fact]
        public async Task Un_tipo_desconocido_no_devuelve_nada()
        {
            Entrada(1, dia: 1);
            Salida(1, dia: 2);

            var resultado = await _servicio.ObtenerPaginaHistorialMovimientosAsync(
                new FiltrosMovimientoDto { TipoMovimiento = "Traslado" });

            Assert.Empty(resultado.Movimientos);
            Assert.Equal(0, resultado.TotalRegistros);
            Assert.Equal(0, resultado.TotalEntradas);
            Assert.Equal(0, resultado.TotalSalidas);
        }

        [Fact]
        public async Task Los_filtros_de_articulo_y_fechas_se_aplican_antes_de_paginar()
        {
            for (var i = 1; i <= 30; i++) Entrada(i, dia: i, articuloId: i % 2 == 0 ? 1 : 2);
            for (var i = 1; i <= 10; i++) Salida(i, dia: i, articuloId: 1);

            var resultado = await _servicio.ObtenerPaginaHistorialMovimientosAsync(new FiltrosMovimientoDto
            {
                ArticuloId = 1,
                Desde = Base.AddDays(1),
                Hasta = Base.AddDays(10),
                Pagina = 0
            });

            // Del artículo 1 y entre los días 1 y 10: 5 entradas (los pares) + 10 salidas.
            Assert.Equal(15, resultado.TotalRegistros);
            Assert.Equal(15, resultado.Movimientos.Count);
            Assert.All(resultado.Movimientos, m => Assert.Equal("Articulo 1", m.Articulo));
        }

        [Fact]
        public async Task Una_entrada_muestra_su_origen_y_una_salida_su_comunidad_o_su_tipo()
        {
            Entrada(1, dia: 1, origen: "Donacion");
            Salida(1, dia: 2, comunidad: "Barrio Esperanza");
            Salida(2, dia: 3, tipo: TiposSalidaInventario.Prestamo);

            var filas = (await _servicio.ObtenerPaginaHistorialMovimientosAsync(new FiltrosMovimientoDto())).Movimientos;

            Assert.Equal("Donacion", filas[2].OrigenODestino);
            Assert.Equal("Barrio Esperanza", filas[1].OrigenODestino);
            Assert.Equal("Prestamo", filas[0].OrigenODestino);
            Assert.Equal("Articulo 1", filas[0].Articulo);
        }

        [Fact]
        public async Task El_tamano_de_pagina_no_puede_superar_el_maximo()
        {
            for (var i = 1; i <= 150; i++) Entrada(i, dia: i);

            var resultado = await _servicio.ObtenerPaginaHistorialMovimientosAsync(
                new FiltrosMovimientoDto { TamanoPagina = 100_000 });

            Assert.Equal(FiltrosMovimientoDto.TamanoPaginaMaximo, resultado.Movimientos.Count);
            Assert.Equal(150, resultado.TotalRegistros);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-5)]
        public async Task Un_tamano_de_pagina_invalido_usa_el_predeterminado(int tamano)
        {
            for (var i = 1; i <= 50; i++) Entrada(i, dia: i);

            var resultado = await _servicio.ObtenerPaginaHistorialMovimientosAsync(
                new FiltrosMovimientoDto { TamanoPagina = tamano });

            Assert.Equal(FiltrosMovimientoDto.TamanoPaginaPredeterminado, resultado.Movimientos.Count);
        }

        [Fact]
        public async Task Una_pagina_negativa_se_trata_como_la_primera()
        {
            for (var i = 1; i <= 30; i++) Entrada(i, dia: i);

            var resultado = await _servicio.ObtenerPaginaHistorialMovimientosAsync(
                new FiltrosMovimientoDto { Pagina = -3 });

            Assert.Equal(30, resultado.Movimientos[0].Id);
        }

        [Fact]
        public async Task Sin_movimientos_devuelve_vacio()
        {
            var resultado = await _servicio.ObtenerPaginaHistorialMovimientosAsync(new FiltrosMovimientoDto());

            Assert.Empty(resultado.Movimientos);
            Assert.Equal(0, resultado.TotalRegistros);
            Assert.Equal(0, resultado.TotalEntradas);
            Assert.Equal(0, resultado.TotalSalidas);
        }

        [Fact]
        public async Task El_historial_completo_de_los_reportes_sigue_ignorando_la_paginacion()
        {
            for (var i = 1; i <= 45; i++) Entrada(i, dia: i);

            var resultado = await _servicio.ObtenerHistorialMovimientosAsync(
                new FiltrosMovimientoDto { Pagina = 1, TamanoPagina = 10 });

            Assert.Equal(45, resultado.Movimientos.Count);
            Assert.Equal(45, resultado.TotalRegistros);
        }

        [Fact]
        public async Task Un_fallo_del_repositorio_se_informa_con_el_mensaje_del_historial()
        {
            _repositorio.Falla = true;

            var ex = await Assert.ThrowsAsync<Exception>(() =>
                _servicio.ObtenerPaginaHistorialMovimientosAsync(new FiltrosMovimientoDto()));

            Assert.Equal("Error al consultar el historial de movimientos.", ex.Message);
        }

        // Imita lo que hace InventarioRepositoryEfCore: mismos filtros para las dos
        // tablas (con el sub-tipo de salida resuelto), unión de las claves ordenada por
        // fecha descendente, tipo (entrada antes que salida) e Id descendente, con
        // Skip/Take sobre esa lista. Las consultas completas (las de los reportes)
        // devuelven cada tabla ordenada y sin paginar. La exclusión de las entradas
        // anuladas vive en SQL y no se reproduce acá.
        private sealed class RepositorioInventarioFalso : IInventarioRepository
        {
            public List<EntradaInventario> Entradas { get; } = new();
            public List<SalidaInventario> Salidas { get; } = new();
            public bool Falla { get; set; }

            private IEnumerable<EntradaInventario> EntradasFiltradas(int? articuloId, DateTime? desde, DateTime? hasta)
            {
                if (Falla)
                    throw new InvalidOperationException("Falla simulada de la base de datos.");

                return Entradas
                    .Where(e => articuloId is null || e.ArticuloId == articuloId)
                    .Where(e => desde is null || e.Fecha >= desde.Value.Date)
                    .Where(e => hasta is null || e.Fecha < hasta.Value.Date.AddDays(1));
            }

            private IEnumerable<SalidaInventario> SalidasFiltradas(int? articuloId, string? tipoSalida, DateTime? desde, DateTime? hasta)
            {
                if (Falla)
                    throw new InvalidOperationException("Falla simulada de la base de datos.");

                return Salidas
                    .Where(s => articuloId is null || s.ArticuloId == articuloId)
                    .Where(s => tipoSalida is null || s.TipoSalida == tipoSalida)
                    .Where(s => desde is null || s.Fecha >= desde.Value.Date)
                    .Where(s => hasta is null || s.Fecha < hasta.Value.Date.AddDays(1));
            }

            public Task<IEnumerable<EntradaInventario>> ObtenerEntradasAsync(int? articuloId, DateTime? desde, DateTime? hasta) =>
                Task.FromResult<IEnumerable<EntradaInventario>>(EntradasFiltradas(articuloId, desde, hasta)
                    .OrderByDescending(e => e.Fecha).ThenByDescending(e => e.Id).ToList());

            public Task<IEnumerable<SalidaInventario>> ObtenerSalidasAsync(int? articuloId, DateTime? desde, DateTime? hasta) =>
                Task.FromResult<IEnumerable<SalidaInventario>>(SalidasFiltradas(articuloId, null, desde, hasta)
                    .OrderByDescending(s => s.Fecha).ThenByDescending(s => s.Id).ToList());

            public Task<ResultadoPaginado<ItemMovimiento>> ObtenerPaginaMovimientosAsync(FiltrosMovimientoDto filtros)
            {
                var items = new List<(int Tipo, int Id, DateTime Fecha, ItemMovimiento Item)>();

                if (filtros.IncluyeEntradas)
                    items.AddRange(EntradasFiltradas(filtros.ArticuloId, filtros.Desde, filtros.Hasta)
                        .Select(e => (0, e.Id, e.Fecha, new ItemMovimiento(e, null))));

                if (filtros.IncluyeSalidas)
                    items.AddRange(SalidasFiltradas(filtros.ArticuloId, filtros.TipoSalida, filtros.Desde, filtros.Hasta)
                        .Select(s => (1, s.Id, s.Fecha, new ItemMovimiento(null, s))));

                var tamano = filtros.TamanoPaginaEfectivo;

                var pagina = items
                    .OrderByDescending(i => i.Fecha).ThenBy(i => i.Tipo).ThenByDescending(i => i.Id)
                    .Skip(filtros.PaginaEfectiva * tamano)
                    .Take(tamano)
                    .Select(i => i.Item)
                    .ToList();

                return Task.FromResult(new ResultadoPaginado<ItemMovimiento>(pagina, items.Count));
            }

            public Task<TotalesMovimientos> ObtenerTotalesMovimientosAsync(FiltrosMovimientoDto filtros)
            {
                var entradas = filtros.IncluyeEntradas
                    ? EntradasFiltradas(filtros.ArticuloId, filtros.Desde, filtros.Hasta).Sum(e => e.Cantidad)
                    : 0;

                var salidas = filtros.IncluyeSalidas
                    ? SalidasFiltradas(filtros.ArticuloId, filtros.TipoSalida, filtros.Desde, filtros.Hasta).Sum(s => s.Cantidad)
                    : 0;

                return Task.FromResult(new TotalesMovimientos(entradas, salidas));
            }

            public Task<IReadOnlyList<Articulo>> ObtenerArticulosPorNombreAsync(string nombre) => throw new NotImplementedException();
            public Task<Articulo?> ObtenerArticuloPorIdAsync(int id) => throw new NotImplementedException();
            public Task ActualizarArticuloAsync(Articulo articulo) => throw new NotImplementedException();
            public Task<ResultadoPaginado<Articulo>> ObtenerExistenciasAsync(FiltrosExistenciaDto filtros) => throw new NotImplementedException();
            public Task<bool> ExisteCodigoAsync(string? codigo, int? idExcluir = null) => throw new NotImplementedException();
            public Task<int> ContarStockBajoAsync() => throw new NotImplementedException();
            public Task<bool> TieneMovimientosAsync(int articuloId) => throw new NotImplementedException();
            public Task EliminarArticuloAsync(int articuloId) => throw new NotImplementedException();
            public Task RegistrarEntradaConStockAsync(EntradaInventario entrada, Articulo? articuloNuevo) => throw new NotImplementedException();
            public Task RegistrarSalidaConStockAsync(SalidaInventario salida) => throw new NotImplementedException();
            public Task AprobarPrestamoConStockAsync(SolicitudPrestamo solicitud, SalidaInventario salida) => throw new NotImplementedException();
            public Task<IReadOnlyList<UnidadesPorMesYTipoDto>> ObtenerEntradasPorMesYOrigenAsync(int mesesHaciaAtras) => throw new NotImplementedException();
            public Task<IReadOnlyList<UnidadesPorMesYTipoDto>> ObtenerSalidasPorMesYTipoAsync(int mesesHaciaAtras) => throw new NotImplementedException();
            public Task<IReadOnlyList<MovimientosPorArticuloDto>> ObtenerArticulosConMasMovimientosAsync(int mesesHaciaAtras, int maximo) => throw new NotImplementedException();
            public Task AgregarSolicitudPrestamoAsync(SolicitudPrestamo solicitud) => throw new NotImplementedException();
            public Task<SolicitudPrestamo?> ObtenerSolicitudPorIdAsync(int id) => throw new NotImplementedException();
            public Task ActualizarSolicitudAsync(SolicitudPrestamo solicitud) => throw new NotImplementedException();
            public Task<IEnumerable<SolicitudPrestamo>> ObtenerSolicitudesAsync(EstadoSolicitudPrestamo? estado = null) => throw new NotImplementedException();
        }
    }
}
