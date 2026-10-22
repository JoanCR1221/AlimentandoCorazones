using SIGAC.Application.DTOs;
using SIGAC.Application.DTOs.Inventario;
using SIGAC.Application.DTOs.Reportes;
using SIGAC.Application.Exceptions;
using SIGAC.Application.Interfaces;
using SIGAC.Application.Services;
using SIGAC.Domain;
using SIGAC.Domain.Entities;

namespace SIGAC.Tests.Application
{
    // Registrar entradas y salidas y editar artículos. El todo-o-nada de la escritura
    // (movimiento y stock en una transacción) es del repositorio y se verifica contra SQL
    // Server; acá se prueba lo que decide el servicio: qué artículo recibe la entrada,
    // qué se rechaza antes de escribir y qué queda en la bitácora.
    public class InventarioServiceTests
    {
        private readonly RepositorioInventarioFalso _repositorio = new();
        private readonly BitacoraGastosFalsa _bitacora = new();
        private readonly InventarioService _servicio;

        private static readonly string UnidadAlimento = UnidadesMedidaArticulo.ObtenerUnidadesValidas(CategoriasArticulo.Alimento).First();

        public InventarioServiceTests()
        {
            _servicio = new InventarioService(_repositorio, _bitacora);
        }

        private Articulo ArticuloExistente(int id, string nombre, int stock = 10, string categoria = CategoriasArticulo.Alimento,
            string? estado = null, string? codigo = null)
        {
            var articulo = new Articulo
            {
                Id = id,
                Nombre = nombre,
                Codigo = codigo,
                Categoria = categoria,
                Estado = estado,
                UnidadMedida = UnidadesMedidaArticulo.ObtenerUnidadesValidas(categoria).First(),
                StockActual = stock
            };
            _repositorio.Articulos.Add(articulo);
            return articulo;
        }

        private static EntradaInventarioCrearDto Entrada(string nombre = "Arroz", int cantidad = 5) => new()
        {
            NombreArticulo = nombre,
            Categoria = CategoriasArticulo.Alimento,
            UnidadMedida = UnidadAlimento,
            Cantidad = cantidad,
            Fecha = DateTime.Today,
            Origen = OrigenesEntradaInventario.Compra
        };

        // ---------------------------------------------------------------
        // Entradas
        // ---------------------------------------------------------------

        [Fact]
        public async Task Una_entrada_de_un_articulo_nuevo_lo_crea_con_los_datos_validados()
        {
            var dto = Entrada(nombre: "  Arroz   blanco ");
            dto.Codigo = " P001 ";

            await _servicio.RegistrarEntradaAsync(dto);

            var (entrada, nuevo) = Assert.Single(_repositorio.Entradas);
            Assert.NotNull(nuevo);
            Assert.Equal("Arroz blanco", nuevo!.Nombre);
            Assert.Equal("P001", nuevo.Codigo);
            Assert.Equal(CategoriasArticulo.Alimento, nuevo.Categoria);
            Assert.Equal(5, nuevo.StockActual); // nace en 0 y la entrada compuesta le suma lo recibido
            Assert.Equal(5, entrada.Cantidad);
            Assert.Contains(_bitacora.Registros, r => r.Detalle!.Contains("artículo nuevo"));
        }

        [Fact]
        public async Task Una_entrada_de_un_articulo_existente_suma_a_ese_articulo_sin_crear_otro()
        {
            var existente = ArticuloExistente(7, "Arroz", stock: 10);

            // La categoría que viene en el formulario se ignora: manda la del artículo.
            var dto = Entrada(cantidad: 4);
            dto.Categoria = CategoriasArticulo.Ropa;
            await _servicio.RegistrarEntradaAsync(dto);

            var (entrada, nuevo) = Assert.Single(_repositorio.Entradas);
            Assert.Null(nuevo);
            Assert.Equal(7, entrada.ArticuloId);
            Assert.Equal(14, existente.StockActual);
            Assert.Single(_repositorio.Articulos);
        }

        [Fact]
        public async Task La_busqueda_por_nombre_ignora_mayusculas_y_espacios_repetidos()
        {
            ArticuloExistente(7, "Arroz blanco", stock: 1);

            await _servicio.RegistrarEntradaAsync(Entrada(nombre: " arroz   BLANCO "));

            Assert.Equal(7, Assert.Single(_repositorio.Entradas).Entrada.ArticuloId);
        }

        [Fact]
        public async Task Un_equipo_se_distingue_por_estado_y_un_estado_nuevo_crea_otro_articulo()
        {
            ArticuloExistente(3, "Silla", stock: 2, categoria: CategoriasArticulo.Equipo, estado: EstadosArticulo.Nuevo);

            var dto = Entrada(nombre: "Silla");
            dto.Categoria = CategoriasArticulo.Equipo;
            dto.UnidadMedida = UnidadesMedidaArticulo.ObtenerUnidadesValidas(CategoriasArticulo.Equipo).First();
            dto.Estado = EstadosArticulo.Danado;

            await _servicio.RegistrarEntradaAsync(dto);

            var (_, nuevo) = Assert.Single(_repositorio.Entradas);
            Assert.NotNull(nuevo);
            Assert.Equal(EstadosArticulo.Danado, nuevo!.Estado);
        }

        [Fact]
        public async Task Un_nombre_que_ya_existe_en_otra_categoria_se_rechaza()
        {
            ArticuloExistente(3, "Silla", categoria: CategoriasArticulo.Equipo, estado: EstadosArticulo.Nuevo);

            var dto = Entrada(nombre: "Silla");
            dto.Estado = null;

            // Es Equipo: sin estado no se sabe a qué artículo suma.
            await Assert.ThrowsAsync<ValidationException>(() => _servicio.RegistrarEntradaAsync(dto));
            Assert.Empty(_repositorio.Entradas);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-3)]
        public async Task Una_cantidad_que_no_es_positiva_se_rechaza(int cantidad)
        {
            await Assert.ThrowsAsync<ValidationException>(() => _servicio.RegistrarEntradaAsync(Entrada(cantidad: cantidad)));
            Assert.Empty(_repositorio.Entradas);
        }

        [Fact]
        public async Task Un_origen_fuera_del_catalogo_se_rechaza()
        {
            var dto = Entrada();
            dto.Origen = "Regalo";

            await Assert.ThrowsAsync<ValidationException>(() => _servicio.RegistrarEntradaAsync(dto));
        }

        [Fact]
        public async Task Una_fecha_futura_se_rechaza()
        {
            var dto = Entrada();
            dto.Fecha = DateTime.Today.AddDays(1);

            var ex = await Assert.ThrowsAsync<ValidationException>(() => _servicio.RegistrarEntradaAsync(dto));
            Assert.Contains("futura", ex.Message);
        }

        [Fact]
        public async Task Una_unidad_que_no_corresponde_a_la_categoria_se_rechaza()
        {
            var dto = Entrada();
            dto.Categoria = CategoriasArticulo.Ropa;
            dto.UnidadMedida = UnidadesMedidaArticulo.ObtenerUnidadesValidas(CategoriasArticulo.Alimento)
                .First(u => !UnidadesMedidaArticulo.EsValidaParaCategoria(CategoriasArticulo.Ropa, u));

            await Assert.ThrowsAsync<ValidationException>(() => _servicio.RegistrarEntradaAsync(dto));
        }

        [Fact]
        public async Task Un_codigo_repetido_en_un_articulo_nuevo_se_rechaza()
        {
            ArticuloExistente(1, "Frijoles", codigo: "P001");

            var dto = Entrada(nombre: "Arroz");
            dto.Codigo = "P001";

            await Assert.ThrowsAsync<DuplicateException>(() => _servicio.RegistrarEntradaAsync(dto));
            Assert.Empty(_repositorio.Entradas);
        }

        [Fact]
        public async Task Si_el_repositorio_falla_el_error_se_envuelve_y_no_queda_bitacora()
        {
            _repositorio.Falla = true;

            await Assert.ThrowsAsync<Exception>(() => _servicio.RegistrarEntradaAsync(Entrada()));
            Assert.Empty(_bitacora.Registros);
        }

        // ---------------------------------------------------------------
        // Salidas por donación
        // ---------------------------------------------------------------

        private static SalidaDonacionCrearDto Salida(int articuloId = 1, int cantidad = 3, string? comunidad = "Barrio Norte") => new()
        {
            ArticuloId = articuloId,
            Cantidad = cantidad,
            Fecha = DateTime.Today,
            ComunidadDestinataria = comunidad!
        };

        [Fact]
        public async Task Una_salida_con_stock_suficiente_se_registra_y_descuenta()
        {
            var articulo = ArticuloExistente(1, "Arroz", stock: 10);

            await _servicio.RegistrarSalidaDonacionAsync(Salida(cantidad: 4));

            var salida = Assert.Single(_repositorio.Salidas);
            Assert.Equal(TiposSalidaInventario.Donacion, salida.TipoSalida);
            Assert.Equal(6, articulo.StockActual);
            Assert.Contains(_bitacora.Registros, r => r.Accion == AccionesBitacora.Entregar);
        }

        [Fact]
        public async Task Se_puede_sacar_todo_el_stock()
        {
            var articulo = ArticuloExistente(1, "Arroz", stock: 5);

            await _servicio.RegistrarSalidaDonacionAsync(Salida(cantidad: 5));

            Assert.Equal(0, articulo.StockActual);
        }

        [Fact]
        public async Task Una_salida_mayor_al_stock_se_rechaza_sin_tocar_nada()
        {
            var articulo = ArticuloExistente(1, "Arroz", stock: 2);

            var ex = await Assert.ThrowsAsync<ValidationException>(() => _servicio.RegistrarSalidaDonacionAsync(Salida(cantidad: 3)));

            Assert.Contains("stock", ex.Message);
            Assert.Empty(_repositorio.Salidas);
            Assert.Equal(2, articulo.StockActual);
            Assert.Empty(_bitacora.Registros);
        }

        [Fact]
        public async Task Una_salida_de_un_articulo_inexistente_se_rechaza()
        {
            await Assert.ThrowsAsync<NotFoundException>(() => _servicio.RegistrarSalidaDonacionAsync(Salida(articuloId: 99)));
        }

        [Fact]
        public async Task Una_salida_sin_comunidad_se_rechaza()
        {
            ArticuloExistente(1, "Arroz", stock: 10);

            await Assert.ThrowsAsync<ValidationException>(() => _servicio.RegistrarSalidaDonacionAsync(Salida(comunidad: "  ")));
            Assert.Empty(_repositorio.Salidas);
        }

        [Fact]
        public async Task Una_salida_con_cantidad_cero_se_rechaza()
        {
            ArticuloExistente(1, "Arroz", stock: 10);

            await Assert.ThrowsAsync<ValidationException>(() => _servicio.RegistrarSalidaDonacionAsync(Salida(cantidad: 0)));
        }

        [Fact]
        public async Task Si_el_stock_cambia_justo_antes_de_escribir_el_repositorio_lo_rechaza_y_el_servicio_deja_pasar_el_motivo()
        {
            var articulo = ArticuloExistente(1, "Arroz", stock: 10);
            _repositorio.StockConsumidoAntesDeEscribir = 9;

            var ex = await Assert.ThrowsAsync<ValidationException>(() => _servicio.RegistrarSalidaDonacionAsync(Salida(cantidad: 4)));

            Assert.Contains("cambió", ex.Message);
            Assert.Empty(_repositorio.Salidas);
            Assert.Equal(1, articulo.StockActual);
        }

        // ---------------------------------------------------------------
        // Editar artículo
        // ---------------------------------------------------------------

        private static ArticuloEditarDto Edicion(string nombre = "Arroz", string? codigo = null) => new()
        {
            Nombre = nombre,
            Codigo = codigo,
            Categoria = CategoriasArticulo.Alimento,
            UnidadMedida = UnidadAlimento,
            StockMinimo = 8
        };

        [Fact]
        public async Task Editar_actualiza_el_articulo_con_los_valores_normalizados()
        {
            var articulo = ArticuloExistente(1, "Arroz", stock: 10);

            await _servicio.EditarArticuloAsync(1, Edicion(nombre: "  Arroz   integral ", codigo: " A1 "));

            Assert.Equal("Arroz integral", articulo.Nombre);
            Assert.Equal("A1", articulo.Codigo);
            Assert.Equal(8, articulo.StockMinimo);
            Assert.Equal(10, articulo.StockActual);
            Assert.Contains(_bitacora.Registros, r => r.Accion == AccionesBitacora.Editar);
        }

        [Fact]
        public async Task Editar_un_articulo_inexistente_se_rechaza()
        {
            await Assert.ThrowsAsync<NotFoundException>(() => _servicio.EditarArticuloAsync(99, Edicion()));
        }

        [Fact]
        public async Task Renombrar_a_un_nombre_que_ya_usa_otro_articulo_se_rechaza()
        {
            ArticuloExistente(1, "Arroz");
            ArticuloExistente(2, "Frijoles");

            await Assert.ThrowsAsync<DuplicateException>(() => _servicio.EditarArticuloAsync(2, Edicion(nombre: "arroz")));
        }

        [Fact]
        public async Task Guardar_un_articulo_con_su_propio_nombre_y_codigo_no_choca_consigo_mismo()
        {
            ArticuloExistente(1, "Arroz", codigo: "A1");

            await _servicio.EditarArticuloAsync(1, Edicion(nombre: "Arroz", codigo: "A1"));
        }

        [Fact]
        public async Task Usar_el_codigo_de_otro_articulo_se_rechaza()
        {
            ArticuloExistente(1, "Arroz", codigo: "A1");
            ArticuloExistente(2, "Frijoles");

            await Assert.ThrowsAsync<DuplicateException>(() => _servicio.EditarArticuloAsync(2, Edicion(nombre: "Frijoles", codigo: "A1")));
        }

        [Fact]
        public async Task Un_stock_minimo_negativo_se_rechaza()
        {
            ArticuloExistente(1, "Arroz");
            var dto = Edicion();
            dto.StockMinimo = -1;

            await Assert.ThrowsAsync<ValidationException>(() => _servicio.EditarArticuloAsync(1, dto));
        }

        // ---------------------------------------------------------------
        // Falso
        // ---------------------------------------------------------------

        // Imita lo que hace InventarioRepositoryEfCore: la entrada crea el artículo nuevo y
        // suma el stock; la salida descuenta solo si alcanza.
        private sealed class RepositorioInventarioFalso : IInventarioRepository
        {
            public List<Articulo> Articulos { get; } = new();
            public List<(EntradaInventario Entrada, Articulo? Nuevo)> Entradas { get; } = new();
            public List<SalidaInventario> Salidas { get; } = new();
            public bool Falla { get; set; }

            // Simula que otro usuario consumió stock entre la validación del servicio y la
            // escritura: se aplica justo al registrar la salida.
            public int StockConsumidoAntesDeEscribir { get; set; }

            public Task<IReadOnlyList<Articulo>> ObtenerArticulosPorNombreAsync(string nombre) =>
                Task.FromResult<IReadOnlyList<Articulo>>(Articulos
                    .Where(a => string.Equals(a.Nombre, nombre, StringComparison.OrdinalIgnoreCase))
                    .ToList());

            public Task<Articulo?> ObtenerArticuloPorIdAsync(int id) =>
                Task.FromResult(Articulos.FirstOrDefault(a => a.Id == id));

            public Task<bool> ExisteCodigoAsync(string? codigo, int? idExcluir = null) =>
                Task.FromResult(codigo is not null && Articulos.Any(a => a.Codigo == codigo && a.Id != idExcluir));

            public Task ActualizarArticuloAsync(Articulo articulo) => Task.CompletedTask;

            public Task RegistrarEntradaConStockAsync(EntradaInventario entrada, Articulo? articuloNuevo)
            {
                if (Falla)
                    throw new InvalidOperationException("Falla simulada de la base de datos.");

                if (articuloNuevo is not null)
                {
                    articuloNuevo.Id = Articulos.Count + 100;
                    Articulos.Add(articuloNuevo);
                    entrada.ArticuloId = articuloNuevo.Id;
                }

                Articulos.First(a => a.Id == entrada.ArticuloId).StockActual += entrada.Cantidad;
                Entradas.Add((entrada, articuloNuevo));
                return Task.CompletedTask;
            }

            public Task RegistrarSalidaConStockAsync(SalidaInventario salida)
            {
                var articulo = Articulos.First(a => a.Id == salida.ArticuloId);
                articulo.StockActual -= StockConsumidoAntesDeEscribir;

                if (articulo.StockActual < salida.Cantidad)
                {
                    throw new ValidationException(
                        "El stock disponible cambió mientras se registraba el movimiento y ya no alcanza. Vuelva a intentarlo.");
                }

                articulo.StockActual -= salida.Cantidad;
                Salidas.Add(salida);
                return Task.CompletedTask;
            }

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
