using SIGAC.Application.DTOs;
using SIGAC.Application.DTOs.Bitacora;
using SIGAC.Application.DTOs.Inventario;
using SIGAC.Application.Exceptions;
using SIGAC.Application.Interfaces;
using SIGAC.Application.Services;
using SIGAC.Application.Validators;
using SIGAC.Domain;
using SIGAC.Domain.Entities;

namespace SIGAC.Tests.Application
{
    public class ValidarEstadoTests
    {
        [Theory]
        [InlineData("Nuevo", "Nuevo")]
        [InlineData("En buen estado", "En buen estado")]
        [InlineData("Dañado", "Dañado")]
        [InlineData("  nuevo ", "Nuevo")]
        [InlineData("EN  BUEN   ESTADO", "En buen estado")]
        public void Equipo_devuelve_el_valor_canonico_de_la_lista(string entrada, string esperado)
        {
            Assert.Equal(esperado, ArticuloValidator.ValidarEstado(CategoriasArticulo.Equipo, entrada));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Equipo_sin_estado_se_rechaza(string? entrada)
        {
            Assert.Throws<ValidationException>(() =>
                ArticuloValidator.ValidarEstado(CategoriasArticulo.Equipo, entrada));
        }

        [Fact]
        public void Equipo_con_estado_fuera_de_la_lista_se_rechaza()
        {
            var ex = Assert.Throws<ValidationException>(() =>
                ArticuloValidator.ValidarEstado(CategoriasArticulo.Equipo, "Oxidado"));

            Assert.Contains("Nuevo", ex.Message);
        }

        [Theory]
        [InlineData(CategoriasArticulo.Alimento)]
        [InlineData(CategoriasArticulo.Ropa)]
        [InlineData(CategoriasArticulo.Calzado)]
        public void Fuera_de_Equipo_el_estado_se_descarta(string categoria)
        {
            // Aunque llegue un valor (residuo del formulario) o uno inválido, no se
            // rechaza ni se guarda: lo que exige el CHECK de la base es NULL.
            Assert.Null(ArticuloValidator.ValidarEstado(categoria, "Nuevo"));
            Assert.Null(ArticuloValidator.ValidarEstado(categoria, "Oxidado"));
            Assert.Null(ArticuloValidator.ValidarEstado(categoria, null));
        }

        [Fact]
        public void La_etiqueta_incluye_el_estado_solo_cuando_lo_hay()
        {
            Assert.Equal("Silla (Nuevo)", Articulo.EtiquetaDe("Silla", "Nuevo"));
            Assert.Equal("Arroz", Articulo.EtiquetaDe("Arroz", null));
            Assert.Equal("Arroz", Articulo.EtiquetaDe("Arroz", ""));
        }
    }

    // La clave del catálogo es (Nombre, Estado): estas pruebas fijan cómo decide el
    // servicio a qué fila va una entrada, que es la regla que antes era solo "por
    // nombre".
    public class RegistrarEntradaConEstadoTests
    {
        private readonly RepositorioFalso _repo = new();
        private readonly InventarioService _servicio;

        public RegistrarEntradaConEstadoTests()
        {
            _servicio = new InventarioService(_repo, new BitacoraFalsa());
        }

        private static EntradaInventarioCrearDto Entrada(
            string nombre, string categoria, string? estado, string unidad = "Unidad", int cantidad = 3) =>
            new()
            {
                NombreArticulo = nombre,
                Categoria = categoria,
                Estado = estado,
                UnidadMedida = unidad,
                Cantidad = cantidad,
                Fecha = DateTime.Today,
                Origen = OrigenesEntradaInventario.Compra
            };

        [Fact]
        public async Task Mismo_nombre_en_distinto_estado_crea_articulos_separados_con_su_propio_stock()
        {
            await _servicio.RegistrarEntradaAsync(Entrada("Silla", CategoriasArticulo.Equipo, EstadosArticulo.Nuevo, cantidad: 8));
            await _servicio.RegistrarEntradaAsync(Entrada("Silla", CategoriasArticulo.Equipo, EstadosArticulo.Danado, cantidad: 2));

            Assert.Equal(2, _repo.Articulos.Count);
            Assert.Equal(8, _repo.Articulos.Single(a => a.Estado == EstadosArticulo.Nuevo).StockActual);
            Assert.Equal(2, _repo.Articulos.Single(a => a.Estado == EstadosArticulo.Danado).StockActual);
        }

        [Fact]
        public async Task Mismo_nombre_y_mismo_estado_suma_al_articulo_existente()
        {
            await _servicio.RegistrarEntradaAsync(Entrada("Silla", CategoriasArticulo.Equipo, EstadosArticulo.Nuevo, cantidad: 8));
            await _servicio.RegistrarEntradaAsync(Entrada("Silla", CategoriasArticulo.Equipo, EstadosArticulo.Nuevo, cantidad: 4));

            var unico = Assert.Single(_repo.Articulos);
            Assert.Equal(12, unico.StockActual);
        }

        [Fact]
        public async Task Entrada_a_un_equipo_existente_sin_estado_se_rechaza()
        {
            await _servicio.RegistrarEntradaAsync(Entrada("Silla", CategoriasArticulo.Equipo, EstadosArticulo.Nuevo));

            // Con el nombre de un Equipo ya registrado, el estado es lo que elige la
            // fila: sin él no hay a qué artículo sumar.
            await Assert.ThrowsAsync<ValidationException>(() =>
                _servicio.RegistrarEntradaAsync(Entrada("Silla", CategoriasArticulo.Equipo, null)));
        }

        [Fact]
        public async Task Equipo_nuevo_sin_estado_se_rechaza()
        {
            await Assert.ThrowsAsync<ValidationException>(() =>
                _servicio.RegistrarEntradaAsync(Entrada("Mesa", CategoriasArticulo.Equipo, null)));

            Assert.Empty(_repo.Articulos);
        }

        [Fact]
        public async Task Articulo_que_no_es_equipo_se_guarda_sin_estado_aunque_llegue_uno()
        {
            await _servicio.RegistrarEntradaAsync(
                Entrada("Arroz", CategoriasArticulo.Alimento, EstadosArticulo.Nuevo, unidad: "Kilogramo"));

            Assert.Null(Assert.Single(_repo.Articulos).Estado);
        }

        [Fact]
        public async Task Un_nombre_de_equipo_no_puede_darse_de_alta_en_otra_categoria()
        {
            await _servicio.RegistrarEntradaAsync(Entrada("Silla", CategoriasArticulo.Equipo, EstadosArticulo.Nuevo));

            // Se manda un estado que "Silla" todavía no tiene: la búsqueda no
            // encuentra fila y se llega al alta, que es donde se detecta que el
            // nombre ya es de otra categoría. (Con el estado que ya existe, la
            // entrada sumaría a esa fila, que es el comportamiento de siempre.)
            var ex = await Assert.ThrowsAsync<ValidationException>(() =>
                _servicio.RegistrarEntradaAsync(Entrada("Silla", CategoriasArticulo.Ropa, EstadosArticulo.Danado)));

            Assert.Contains("otra categoría", ex.Message);
            Assert.Single(_repo.Articulos);
        }

        [Fact]
        public async Task Editar_al_estado_de_otro_articulo_con_el_mismo_nombre_es_duplicado()
        {
            await _servicio.RegistrarEntradaAsync(Entrada("Silla", CategoriasArticulo.Equipo, EstadosArticulo.Nuevo));
            await _servicio.RegistrarEntradaAsync(Entrada("Silla", CategoriasArticulo.Equipo, EstadosArticulo.Danado));

            var danado = _repo.Articulos.Single(a => a.Estado == EstadosArticulo.Danado);

            await Assert.ThrowsAsync<DuplicateException>(() => _servicio.EditarArticuloAsync(danado.Id,
                new ArticuloEditarDto
                {
                    Nombre = "Silla",
                    Categoria = CategoriasArticulo.Equipo,
                    Estado = EstadosArticulo.Nuevo,
                    UnidadMedida = "Unidad",
                    StockMinimo = 5
                }));
        }

        [Fact]
        public async Task Editar_el_estado_a_uno_libre_conserva_el_stock()
        {
            await _servicio.RegistrarEntradaAsync(Entrada("Silla", CategoriasArticulo.Equipo, EstadosArticulo.Nuevo, cantidad: 8));
            var silla = Assert.Single(_repo.Articulos);

            await _servicio.EditarArticuloAsync(silla.Id, new ArticuloEditarDto
            {
                Nombre = "Silla",
                Categoria = CategoriasArticulo.Equipo,
                Estado = "en buen estado",
                UnidadMedida = "Unidad",
                StockMinimo = 5
            });

            var editada = Assert.Single(_repo.Articulos);
            Assert.Equal(EstadosArticulo.EnBuenEstado, editada.Estado);
            Assert.Equal(8, editada.StockActual);
        }

        [Fact]
        public async Task Pasar_un_articulo_de_equipo_a_otra_categoria_le_quita_el_estado()
        {
            await _servicio.RegistrarEntradaAsync(Entrada("Delantal", CategoriasArticulo.Equipo, EstadosArticulo.Nuevo));
            var delantal = Assert.Single(_repo.Articulos);

            await _servicio.EditarArticuloAsync(delantal.Id, new ArticuloEditarDto
            {
                Nombre = "Delantal",
                Categoria = CategoriasArticulo.Ropa,
                Estado = EstadosArticulo.Nuevo,
                UnidadMedida = "Unidad",
                StockMinimo = 5
            });

            Assert.Null(Assert.Single(_repo.Articulos).Estado);
        }

        // ---- Dobles de prueba: solo lo que usan RegistrarEntrada y EditarArticulo ----

        private sealed class BitacoraFalsa : IBitacoraService
        {
            public Task RegistrarAsync(string accion, string modulo, string? detalle = null) => Task.CompletedTask;

            public Task RegistrarDeUsuarioAsync(string? usuarioId, string nombreUsuario, string? rol,
                string accion, string modulo, string? detalle = null) => Task.CompletedTask;

            public Task<ResultadoPaginado<BitacoraAccionDto>> ObtenerBitacoraAsync(FiltrosBitacoraDto filtros) =>
                throw new NotImplementedException();
        }

        private sealed class RepositorioFalso : IInventarioRepository
        {
            public List<Articulo> Articulos { get; } = new();
            private int _siguienteId = 1;

            public Task<IReadOnlyList<Articulo>> ObtenerArticulosPorNombreAsync(string nombre) =>
                Task.FromResult<IReadOnlyList<Articulo>>(Articulos
                    .Where(a => string.Equals(a.Nombre, nombre, StringComparison.OrdinalIgnoreCase))
                    .ToList());

            public Task<Articulo?> ObtenerArticuloPorIdAsync(int id) =>
                Task.FromResult(Articulos.FirstOrDefault(a => a.Id == id));

            public Task ActualizarArticuloAsync(Articulo articulo)
            {
                // Como el repositorio real: copia los campos del catálogo y deja el
                // stock quieto (solo cambia dentro de un movimiento).
                var existente = Articulos.Single(a => a.Id == articulo.Id);
                existente.Nombre = articulo.Nombre;
                existente.Codigo = articulo.Codigo;
                existente.Categoria = articulo.Categoria;
                existente.Estado = articulo.Estado;
                existente.UnidadMedida = articulo.UnidadMedida;
                existente.Ubicacion = articulo.Ubicacion;
                existente.StockMinimo = articulo.StockMinimo;
                return Task.CompletedTask;
            }

            public Task<bool> ExisteCodigoAsync(string? codigo, int? idExcluir = null) =>
                Task.FromResult(!string.IsNullOrEmpty(codigo)
                    && Articulos.Any(a => a.Codigo == codigo && a.Id != idExcluir));

            public Task RegistrarEntradaConStockAsync(EntradaInventario entrada, Articulo? articuloNuevo)
            {
                if (articuloNuevo is not null)
                {
                    articuloNuevo.Id = _siguienteId++;
                    Articulos.Add(articuloNuevo);
                    entrada.ArticuloId = articuloNuevo.Id;
                }

                Articulos.Single(a => a.Id == entrada.ArticuloId).StockActual += entrada.Cantidad;
                return Task.CompletedTask;
            }

            public Task<ResultadoPaginado<Articulo>> ObtenerExistenciasAsync(FiltrosExistenciaDto filtros) => throw new NotImplementedException();
            public Task<int> ContarStockBajoAsync() => throw new NotImplementedException();
            public Task<bool> TieneMovimientosAsync(int articuloId) => throw new NotImplementedException();
            public Task EliminarArticuloAsync(int articuloId) => throw new NotImplementedException();
            public Task RegistrarSalidaConStockAsync(SalidaInventario salida) => throw new NotImplementedException();
            public Task AprobarPrestamoConStockAsync(SolicitudPrestamo solicitud, SalidaInventario salida) => throw new NotImplementedException();
            public Task<IEnumerable<EntradaInventario>> ObtenerEntradasAsync(int? articuloId, DateTime? desde, DateTime? hasta) => throw new NotImplementedException();
            public Task<IEnumerable<SalidaInventario>> ObtenerSalidasAsync(int? articuloId, DateTime? desde, DateTime? hasta) => throw new NotImplementedException();
            public Task AgregarSolicitudPrestamoAsync(SolicitudPrestamo solicitud) => throw new NotImplementedException();
            public Task<SolicitudPrestamo?> ObtenerSolicitudPorIdAsync(int id) => throw new NotImplementedException();
            public Task ActualizarSolicitudAsync(SolicitudPrestamo solicitud) => throw new NotImplementedException();
            public Task<IEnumerable<SolicitudPrestamo>> ObtenerSolicitudesAsync(EstadoSolicitudPrestamo? estado = null) => throw new NotImplementedException();
        }
    }
}
