using SIGAC.Application.DTOs;
using SIGAC.Application.DTOs.Donaciones;
using SIGAC.Application.DTOs.Inventario;
using SIGAC.Application.DTOs.Reportes;
using SIGAC.Application.Exceptions;
using SIGAC.Application.Interfaces;
using SIGAC.Application.Services;
using SIGAC.Domain;
using SIGAC.Domain.Entities;

namespace SIGAC.Tests.Application
{
    // Registro de donaciones (dinero, especie y entregas): que cada operación llegue al
    // repositorio como UNA sola escritura compuesta (donante nuevo + donación +
    // movimiento de stock) y que el servicio rechace antes de escribir lo que no
    // corresponde. La transacción en sí es de EF Core y no se prueba acá.
    public class DonacionesRegistroTests
    {
        private readonly RepositorioInventarioRegistroFalso _inventario = new();
        private readonly RepositorioDonacionesRegistroFalso _donaciones = new();
        private readonly RepositorioDonantesRegistroFalso _donantes = new();
        private readonly BitacoraGastosFalsa _bitacora = new();
        private readonly DonacionesService _servicio;

        public DonacionesRegistroTests()
        {
            // Donante activo #1: el que usan las donaciones que no crean uno nuevo.
            _donantes.Existentes.Add(new Donante { Id = 1, Nombre = "Super Valle", TipoPersona = TiposPersonaDonante.Todos[0], Estado = true });

            var servicioInventario = new InventarioService(_inventario, _bitacora);

            _servicio = new DonacionesService(
                _donaciones, _donantes, servicioInventario, beneficiariosRepository: null!, _bitacora);
        }

        private static NuevoDonanteDto DonanteNuevo(string nombre = "Ana Mora") => new()
        {
            Nombre = nombre,
            TipoPersona = TiposPersonaDonante.Todos[0]
        };

        private static DetalleArticuloDto Linea(
            string nombre, int cantidad = 3, string categoria = CategoriasArticulo.Alimento,
            string? estado = null, string? unidad = null) => new()
        {
            NombreArticulo = nombre,
            Cantidad = cantidad,
            Categoria = categoria,
            Estado = estado,
            UnidadMedida = unidad ?? UnidadesMedidaArticulo.ObtenerUnidadesValidas(categoria).First()
        };

        private static DonacionEspecieCrearDto DonacionEspecie(NuevoDonanteDto? nuevo, params DetalleArticuloDto[] lineas) => new()
        {
            NuevoDonante = nuevo,
            DonanteId = nuevo is null ? 1 : null,
            Fecha = DateTime.Today,
            Articulos = lineas.ToList()
        };

        // ---------------------------------------------------------------
        // Dinero
        // ---------------------------------------------------------------

        [Fact]
        public async Task Dinero_con_donante_nuevo_lo_manda_al_repositorio_junto_con_la_donacion()
        {
            await _servicio.RegistrarDonacionDineroAsync(new DonacionDineroCrearDto
            {
                NuevoDonante = DonanteNuevo(),
                Monto = 1000,
                Moneda = TiposMoneda.Colones,
                Fecha = DateTime.Today
            });

            var llamada = Assert.Single(_donaciones.Dinero);
            Assert.NotNull(llamada.NuevoDonante);
            Assert.Equal("Ana Mora", llamada.NuevoDonante!.Nombre);

            // El donante ya no se crea por su cuenta antes que la donación: era lo que
            // dejaba un donante huérfano cuando la donación fallaba.
            Assert.Empty(_donantes.Agregados);
        }

        [Fact]
        public async Task Dinero_con_donante_nuevo_deja_el_alta_del_donante_en_la_bitacora()
        {
            await _servicio.RegistrarDonacionDineroAsync(new DonacionDineroCrearDto
            {
                NuevoDonante = DonanteNuevo("Ana Mora"),
                Monto = 1000,
                Moneda = TiposMoneda.Colones,
                Fecha = DateTime.Today
            });

            Assert.Contains(_bitacora.Registros, r => r.Modulo == ModulosSistema.Donaciones && r.Detalle!.Contains("Donante #") && r.Detalle.Contains("Ana Mora"));
            Assert.Contains(_bitacora.Registros, r => r.Detalle!.StartsWith("Donación en dinero"));
        }

        [Fact]
        public async Task Dinero_con_donante_nuevo_invalido_no_escribe_nada()
        {
            await Assert.ThrowsAsync<ValidationException>(() => _servicio.RegistrarDonacionDineroAsync(new DonacionDineroCrearDto
            {
                NuevoDonante = DonanteNuevo(nombre: "   "),
                Monto = 1000,
                Moneda = TiposMoneda.Colones,
                Fecha = DateTime.Today
            }));

            Assert.Empty(_donaciones.Dinero);
            Assert.Empty(_donantes.Agregados);
        }

        [Fact]
        public async Task Dinero_si_el_repositorio_falla_no_deja_nada_en_la_bitacora()
        {
            _donaciones.Falla = true;

            await Assert.ThrowsAsync<Exception>(() => _servicio.RegistrarDonacionDineroAsync(new DonacionDineroCrearDto
            {
                NuevoDonante = DonanteNuevo(),
                Monto = 1000,
                Moneda = TiposMoneda.Colones,
                Fecha = DateTime.Today
            }));

            Assert.Empty(_bitacora.Registros);
        }

        // ---------------------------------------------------------------
        // Especie
        // ---------------------------------------------------------------

        [Fact]
        public async Task Especie_manda_donante_nuevo_donacion_y_entradas_en_una_sola_escritura()
        {
            await _servicio.RegistrarDonacionEspecieAsync(
                DonacionEspecie(DonanteNuevo(), Linea("Arroz"), Linea("Frijoles")));

            var llamada = Assert.Single(_donaciones.Especie);
            Assert.NotNull(llamada.NuevoDonante);
            Assert.Equal(2, llamada.Donacion.Detalles.Count);
            Assert.Equal(2, llamada.Entradas.Count);
            Assert.All(llamada.Entradas, e => Assert.Equal(OrigenesEntradaInventario.Donacion, e.Origen));
            Assert.Empty(_donantes.Agregados);

            // Inventario no escribe por su cuenta: el stock lo suma la escritura compuesta.
            Assert.Empty(_inventario.EntradasRegistradas);
        }

        [Fact]
        public async Task Especie_con_articulo_existente_guarda_la_categoria_y_unidad_del_inventario()
        {
            // El artículo existe como Ropa, en "Unidad"; la línea llega con otra categoría y unidad.
            var unidadRopa = UnidadesMedidaArticulo.ObtenerUnidadesValidas(CategoriasArticulo.Ropa).First();
            _inventario.Articulos.Add(new Articulo
            {
                Id = 10, Nombre = "Camisa", Categoria = CategoriasArticulo.Ropa, UnidadMedida = unidadRopa, StockActual = 4
            });

            await _servicio.RegistrarDonacionEspecieAsync(DonacionEspecie(
                null, Linea("Camisa", categoria: CategoriasArticulo.Alimento)));

            var llamada = Assert.Single(_donaciones.Especie);
            var detalle = Assert.Single(llamada.Donacion.Detalles);
            var entrada = Assert.Single(llamada.Entradas);

            Assert.Equal(CategoriasArticulo.Ropa, detalle.Categoria);
            Assert.Equal(unidadRopa, detalle.UnidadMedida);
            Assert.Equal(10, entrada.ArticuloId);
        }

        [Fact]
        public async Task Especie_con_el_mismo_articulo_nuevo_en_dos_lineas_crea_un_solo_articulo()
        {
            await _servicio.RegistrarDonacionEspecieAsync(DonacionEspecie(
                DonanteNuevo(), Linea("Arroz", cantidad: 2), Linea("Arroz", cantidad: 5)));

            var llamada = Assert.Single(_donaciones.Especie);

            Assert.Equal(2, llamada.Entradas.Count);
            Assert.Same(llamada.Entradas[0].Articulo, llamada.Entradas[1].Articulo);
        }

        [Fact]
        public async Task Especie_con_una_linea_invalida_no_escribe_nada()
        {
            await Assert.ThrowsAsync<ValidationException>(() => _servicio.RegistrarDonacionEspecieAsync(
                DonacionEspecie(DonanteNuevo(), Linea("Arroz"), Linea("Frijoles", cantidad: 0))));

            Assert.Empty(_donaciones.Especie);
            Assert.Empty(_donantes.Agregados);
        }

        [Fact]
        public async Task Especie_si_el_repositorio_falla_no_deja_nada_en_la_bitacora()
        {
            _donaciones.Falla = true;

            await Assert.ThrowsAsync<Exception>(() => _servicio.RegistrarDonacionEspecieAsync(
                DonacionEspecie(DonanteNuevo(), Linea("Arroz"))));

            Assert.Empty(_bitacora.Registros);
        }

        // ---------------------------------------------------------------
        // Entregas
        // ---------------------------------------------------------------

        private DonacionEntregadaCrearDto Entrega(int cantidad = 2, DateTime? fecha = null) => new()
        {
            ArticuloId = 10,
            Cantidad = cantidad,
            Fecha = fecha ?? DateTime.Today,
            TipoDestinatario = TiposDestinatarioDonacion.Comunidad,
            ComunidadDestinataria = "Barrio Norte"
        };

        private void ArticuloConStock(int stock) => _inventario.Articulos.Add(new Articulo
        {
            Id = 10,
            Nombre = "Arroz",
            Categoria = CategoriasArticulo.Alimento,
            UnidadMedida = UnidadesMedidaArticulo.ObtenerUnidadesValidas(CategoriasArticulo.Alimento).First(),
            StockActual = stock
        });

        [Fact]
        public async Task Entrega_manda_la_salida_y_la_entrega_en_una_sola_escritura()
        {
            ArticuloConStock(5);

            await _servicio.RegistrarDonacionEntregadaAsync(Entrega(cantidad: 3));

            var llamada = Assert.Single(_donaciones.Entregas);
            Assert.Equal(3, llamada.Entrega.Cantidad);
            Assert.Equal(3, llamada.Salida.Cantidad);
            Assert.Equal(TiposSalidaInventario.Donacion, llamada.Salida.TipoSalida);

            // Inventario no descuenta por su cuenta antes de guardar la entrega.
            Assert.Empty(_inventario.SalidasRegistradas);
        }

        [Fact]
        public async Task Entrega_si_el_repositorio_falla_no_queda_nada_registrado_en_inventario()
        {
            ArticuloConStock(5);
            _donaciones.Falla = true;

            await Assert.ThrowsAsync<Exception>(() => _servicio.RegistrarDonacionEntregadaAsync(Entrega()));

            Assert.Empty(_inventario.SalidasRegistradas);
            Assert.Equal(5, _inventario.Articulos.Single().StockActual);
            Assert.Empty(_bitacora.Registros);
        }

        [Fact]
        public async Task Entrega_con_stock_insuficiente_se_rechaza_sin_escribir()
        {
            ArticuloConStock(1);

            await Assert.ThrowsAsync<ValidationException>(() => _servicio.RegistrarDonacionEntregadaAsync(Entrega(cantidad: 2)));

            Assert.Empty(_donaciones.Entregas);
        }

        [Fact]
        public async Task Entrega_sin_fecha_se_rechaza()
        {
            ArticuloConStock(5);

            var ex = await Assert.ThrowsAsync<ValidationException>(() =>
                _servicio.RegistrarDonacionEntregadaAsync(Entrega(fecha: default(DateTime))));

            Assert.Contains("obligatoria", ex.Message);
            Assert.Empty(_donaciones.Entregas);
        }

        [Fact]
        public async Task Entrega_con_fecha_futura_se_rechaza()
        {
            ArticuloConStock(5);

            var ex = await Assert.ThrowsAsync<ValidationException>(() =>
                _servicio.RegistrarDonacionEntregadaAsync(Entrega(fecha: DateTime.Today.AddDays(1))));

            Assert.Contains("futura", ex.Message);
            Assert.Empty(_donaciones.Entregas);
        }

        [Fact]
        public async Task Salida_directa_de_inventario_sin_fecha_se_rechaza()
        {
            ArticuloConStock(5);
            var inventario = new InventarioService(_inventario, _bitacora);

            await Assert.ThrowsAsync<ValidationException>(() => inventario.RegistrarSalidaDonacionAsync(new SalidaDonacionCrearDto
            {
                ArticuloId = 10,
                Cantidad = 1,
                ComunidadDestinataria = "Barrio Norte"
            }));

            Assert.Empty(_inventario.SalidasRegistradas);
        }

        // ---------------------------------------------------------------
        // Falsos
        // ---------------------------------------------------------------

        private sealed class RepositorioDonacionesRegistroFalso : IDonacionesRepository
        {
            public bool Falla { get; set; }

            public List<(DonacionDinero Donacion, Donante? NuevoDonante)> Dinero { get; } = new();
            public List<(DonacionEspecie Donacion, Donante? NuevoDonante, IReadOnlyList<EntradaInventario> Entradas)> Especie { get; } = new();
            public List<(DonacionEntregada Entrega, SalidaInventario Salida)> Entregas { get; } = new();

            private void ComprobarFalla()
            {
                if (Falla)
                    throw new InvalidOperationException("Falla simulada de la base de datos.");
            }

            public Task AgregarDonacionDineroAsync(DonacionDinero donacion, Donante? nuevoDonante)
            {
                ComprobarFalla();
                donacion.Id = Dinero.Count + 1;
                Dinero.Add((donacion, nuevoDonante));
                if (nuevoDonante is not null)
                    nuevoDonante.Id = 100 + Dinero.Count;
                return Task.CompletedTask;
            }

            public Task AgregarDonacionEspecieAsync(
                DonacionEspecie donacion, Donante? nuevoDonante, IReadOnlyList<EntradaInventario> entradas)
            {
                ComprobarFalla();
                donacion.Id = Especie.Count + 1;
                Especie.Add((donacion, nuevoDonante, entradas));
                if (nuevoDonante is not null)
                    nuevoDonante.Id = 100 + Especie.Count;
                return Task.CompletedTask;
            }

            public Task AgregarDonacionEntregadaAsync(DonacionEntregada donacion, SalidaInventario salida)
            {
                ComprobarFalla();
                donacion.Id = Entregas.Count + 1;
                Entregas.Add((donacion, salida));
                return Task.CompletedTask;
            }

            public Task<IEnumerable<DonacionDinero>> ObtenerDonacionesDineroAsync(FiltrosHistorialDonacionDto filtros) => throw new NotImplementedException();
            public Task<IEnumerable<DonacionEspecie>> ObtenerDonacionesEspecieAsync(FiltrosHistorialDonacionDto filtros) => throw new NotImplementedException();
            public Task<ResultadoPaginado<ItemHistorialDonacion>> ObtenerPaginaHistorialAsync(FiltrosHistorialDonacionDto filtros) => throw new NotImplementedException();
            public Task<IReadOnlyList<MontoPorMonedaDto>> ObtenerTotalesDineroPorMonedaAsync(FiltrosHistorialDonacionDto filtros) => throw new NotImplementedException();
            public Task<IEnumerable<DonacionEntregada>> ObtenerEntregasAsync(FiltrosHistorialEntregaDto filtros) => throw new NotImplementedException();
            public Task<IReadOnlyList<ConteoDonacionMensualDto>> ObtenerCantidadPorMesAsync(int mesesHaciaAtras) => throw new NotImplementedException();
            public Task<IReadOnlyList<MontoPorMesDto>> ObtenerDineroEnColonesPorMesAsync(int mesesHaciaAtras) => throw new NotImplementedException();
            public Task<IReadOnlyList<DonacionesPorDonanteDto>> ObtenerTopDonantesAsync(int mesesHaciaAtras, int maximo) => throw new NotImplementedException();
        }

        private sealed class RepositorioDonantesRegistroFalso : IDonantesRepository
        {
            public List<Donante> Agregados { get; } = new();
            public List<Donante> Existentes { get; } = new();

            public Task AgregarAsync(Donante donante)
            {
                Agregados.Add(donante);
                return Task.CompletedTask;
            }

            public Task<Donante?> ObtenerPorIdAsync(int id) =>
                Task.FromResult(Existentes.FirstOrDefault(d => d.Id == id));

            public Task ActualizarAsync(Donante donante) => throw new NotImplementedException();
            public Task<ResultadoPaginado<Donante>> ObtenerTodosAsync(FiltrosDonanteDto filtros) => throw new NotImplementedException();
            public Task<bool> ExisteNombreAsync(string nombre, int? idExcluir = null) => throw new NotImplementedException();
            public Task CambiarEstadoAsync(int id, bool estado) => throw new NotImplementedException();
            public Task<ResumenRegistrosDto> ObtenerResumenAsync() => throw new NotImplementedException();
        }

        // Solo lo que usan los servicios bajo prueba. Las entradas y salidas "registradas"
        // son las que el servicio de Inventario escribiría POR SU CUENTA: en una donación
        // tienen que quedar vacías, porque la escritura la hace Donaciones en su transacción.
        private sealed class RepositorioInventarioRegistroFalso : IInventarioRepository
        {
            public List<Articulo> Articulos { get; } = new();
            public List<EntradaInventario> EntradasRegistradas { get; } = new();
            public List<SalidaInventario> SalidasRegistradas { get; } = new();

            public Task<IReadOnlyList<Articulo>> ObtenerArticulosPorNombreAsync(string nombre) =>
                Task.FromResult<IReadOnlyList<Articulo>>(Articulos
                    .Where(a => string.Equals(a.Nombre, nombre, StringComparison.OrdinalIgnoreCase))
                    .ToList());

            public Task<Articulo?> ObtenerArticuloPorIdAsync(int id) =>
                Task.FromResult(Articulos.FirstOrDefault(a => a.Id == id));

            public Task<bool> ExisteCodigoAsync(string? codigo, int? idExcluir = null) =>
                Task.FromResult(codigo is not null && Articulos.Any(a => a.Codigo == codigo && a.Id != idExcluir));

            public Task RegistrarEntradaConStockAsync(EntradaInventario entrada, Articulo? articuloNuevo)
            {
                EntradasRegistradas.Add(entrada);
                return Task.CompletedTask;
            }

            public Task RegistrarSalidaConStockAsync(SalidaInventario salida)
            {
                SalidasRegistradas.Add(salida);
                return Task.CompletedTask;
            }

            public Task ActualizarArticuloAsync(Articulo articulo) => throw new NotImplementedException();
            public Task<ResultadoPaginado<Articulo>> ObtenerExistenciasAsync(FiltrosExistenciaDto filtros) => throw new NotImplementedException();
            public Task<int> ContarStockBajoAsync() => throw new NotImplementedException();
            public Task<bool> TieneMovimientosAsync(int articuloId) => throw new NotImplementedException();
            public Task EliminarArticuloAsync(int articuloId) => throw new NotImplementedException();
            public Task<IEnumerable<EntradaInventario>> ObtenerEntradasAsync(int? articuloId, DateTime? desde, DateTime? hasta) => throw new NotImplementedException();
            public Task<IEnumerable<SalidaInventario>> ObtenerSalidasAsync(int? articuloId, DateTime? desde, DateTime? hasta) => throw new NotImplementedException();
            public Task<ResultadoPaginado<ItemMovimiento>> ObtenerPaginaMovimientosAsync(FiltrosMovimientoDto filtros) => throw new NotImplementedException();
            public Task<TotalesMovimientos> ObtenerTotalesMovimientosAsync(FiltrosMovimientoDto filtros) => throw new NotImplementedException();
            public Task<IReadOnlyList<UnidadesPorMesYTipoDto>> ObtenerEntradasPorMesYOrigenAsync(int mesesHaciaAtras) => throw new NotImplementedException();
            public Task<IReadOnlyList<UnidadesPorMesYTipoDto>> ObtenerSalidasPorMesYTipoAsync(int mesesHaciaAtras) => throw new NotImplementedException();
            public Task<IReadOnlyList<MovimientosPorArticuloDto>> ObtenerArticulosConMasMovimientosAsync(int mesesHaciaAtras, int maximo) => throw new NotImplementedException();
        }
    }
}
