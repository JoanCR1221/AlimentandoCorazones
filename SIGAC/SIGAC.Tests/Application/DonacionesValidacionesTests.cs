using SIGAC.Application.DTOs;
using SIGAC.Application.DTOs.Donaciones;
using SIGAC.Application.DTOs.Inventario;
using SIGAC.Application.DTOs.Reportes;
using SIGAC.Application.Exceptions;
using SIGAC.Application.Interfaces;
using SIGAC.Application.Services;
using SIGAC.Application.Validators;
using SIGAC.Domain;
using SIGAC.Domain.Entities;

namespace SIGAC.Tests.Application
{
    // Reglas del alta de donaciones y de donantes. El orden de escritura y la atomicidad
    // dependen del repositorio y de la transacción, y se verifican contra SQL Server; acá
    // se prueba lo que el servicio rechaza ANTES de escribir y lo que entrega al
    // repositorio cuando todo es válido.
    public class DonacionesValidacionesTests
    {
        private readonly RepositorioDonacionesFalso _donaciones = new();
        private readonly RepositorioDonantesFalso _donantes = new();
        private readonly BitacoraGastosFalsa _bitacora = new();
        private readonly DonacionesService _servicio;

        public DonacionesValidacionesTests()
        {
            _donantes.Donantes.Add(new Donante { Id = 1, Nombre = "Super Valle", TipoPersona = TiposPersonaDonante.Todos[0], Estado = true });
            _donantes.Donantes.Add(new Donante { Id = 2, Nombre = "Inactivo", TipoPersona = TiposPersonaDonante.Todos[0], Estado = false });

            // Inventario y beneficiarios no intervienen en las donaciones en dinero.
            _servicio = new DonacionesService(_donaciones, _donantes, null!, null!, _bitacora);
        }

        private static DonacionDineroCrearDto Dinero(int? donanteId = 1, NuevoDonanteDto? nuevo = null, decimal monto = 1000m) => new()
        {
            DonanteId = donanteId,
            NuevoDonante = nuevo,
            Monto = monto,
            Moneda = TiposMoneda.Colones,
            Fecha = DateTime.Today
        };

        // ---------------------------------------------------------------
        // Donación en dinero
        // ---------------------------------------------------------------

        [Fact]
        public async Task Una_donacion_valida_llega_al_repositorio_y_queda_en_la_bitacora()
        {
            await _servicio.RegistrarDonacionDineroAsync(Dinero(monto: 2500.5m));

            var donacion = Assert.Single(_donaciones.Dinero);
            Assert.Equal(1, donacion.DonanteId);
            Assert.Equal(2500.5m, donacion.Monto);
            Assert.Contains(_bitacora.Registros, r => r.Modulo == ModulosSistema.Donaciones && r.Detalle!.Contains("Donación en dinero"));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public async Task Un_monto_que_no_es_positivo_se_rechaza(int monto)
        {
            await Assert.ThrowsAsync<ValidationException>(() => _servicio.RegistrarDonacionDineroAsync(Dinero(monto: monto)));
            Assert.Empty(_donaciones.Dinero);
        }

        [Fact]
        public async Task Una_moneda_fuera_del_catalogo_se_rechaza()
        {
            var dto = Dinero();
            dto.Moneda = "XYZ";

            await Assert.ThrowsAsync<ValidationException>(() => _servicio.RegistrarDonacionDineroAsync(dto));
        }

        [Fact]
        public async Task Una_fecha_futura_se_rechaza()
        {
            var dto = Dinero();
            dto.Fecha = DateTime.Today.AddDays(1);

            var ex = await Assert.ThrowsAsync<ValidationException>(() => _servicio.RegistrarDonacionDineroAsync(dto));
            Assert.Contains("futura", ex.Message);
        }

        [Fact]
        public async Task Sin_donante_se_rechaza()
        {
            await Assert.ThrowsAsync<ValidationException>(() => _servicio.RegistrarDonacionDineroAsync(Dinero(donanteId: null)));
        }

        [Fact]
        public async Task Un_donante_existente_y_uno_nuevo_a_la_vez_se_rechazan()
        {
            var nuevo = new NuevoDonanteDto { Nombre = "Ana", TipoPersona = TiposPersonaDonante.Todos[0] };

            await Assert.ThrowsAsync<ValidationException>(() => _servicio.RegistrarDonacionDineroAsync(Dinero(donanteId: 1, nuevo: nuevo)));
        }

        [Fact]
        public async Task Un_donante_inexistente_se_rechaza()
        {
            await Assert.ThrowsAsync<NotFoundException>(() => _servicio.RegistrarDonacionDineroAsync(Dinero(donanteId: 99)));
        }

        [Fact]
        public async Task Un_donante_inactivo_no_puede_recibir_donaciones()
        {
            var ex = await Assert.ThrowsAsync<ValidationException>(() => _servicio.RegistrarDonacionDineroAsync(Dinero(donanteId: 2)));

            Assert.Contains("inactivo", ex.Message);
            Assert.Empty(_donaciones.Dinero);
        }

        [Fact]
        public async Task Un_donante_nuevo_con_datos_invalidos_no_deja_nada_escrito()
        {
            var nuevo = new NuevoDonanteDto { Nombre = "   ", TipoPersona = TiposPersonaDonante.Todos[0] };

            await Assert.ThrowsAsync<ValidationException>(() => _servicio.RegistrarDonacionDineroAsync(Dinero(donanteId: null, nuevo: nuevo)));

            Assert.Empty(_donaciones.Dinero);
        }

        // ---------------------------------------------------------------
        // Donación en especie (lo que se rechaza antes de escribir)
        // ---------------------------------------------------------------

        [Fact]
        public async Task Una_donacion_en_especie_sin_articulos_se_rechaza()
        {
            await Assert.ThrowsAsync<ValidationException>(() => _servicio.RegistrarDonacionEspecieAsync(new DonacionEspecieCrearDto
            {
                DonanteId = 1,
                Fecha = DateTime.Today,
                Articulos = new()
            }));
        }

        [Fact]
        public async Task Una_linea_con_categoria_invalida_rechaza_la_donacion_completa_sin_escribir()
        {
            var ex = await Assert.ThrowsAsync<ValidationException>(() => _servicio.RegistrarDonacionEspecieAsync(new DonacionEspecieCrearDto
            {
                DonanteId = 1,
                Fecha = DateTime.Today,
                Articulos = new()
                {
                    new DetalleArticuloDto
                    {
                        NombreArticulo = "Arroz", Cantidad = 2, Categoria = CategoriasArticulo.Alimento,
                        UnidadMedida = UnidadesMedidaArticulo.ObtenerUnidadesValidas(CategoriasArticulo.Alimento).First()
                    },
                    new DetalleArticuloDto { NombreArticulo = "Cosa", Cantidad = 1, Categoria = "Inventada", UnidadMedida = "Unidad" }
                }
            }));

            Assert.NotEmpty(ex.Message);
            Assert.Empty(_donaciones.Especie);
        }

        // ---------------------------------------------------------------
        // Entregas
        // ---------------------------------------------------------------

        [Fact]
        public async Task Una_entrega_con_tipo_de_destinatario_invalido_se_rechaza()
        {
            await Assert.ThrowsAsync<ValidationException>(() => _servicio.RegistrarDonacionEntregadaAsync(new DonacionEntregadaCrearDto
            {
                ArticuloId = 1, Cantidad = 1, Fecha = DateTime.Today, TipoDestinatario = "Vecino"
            }));
        }

        [Fact]
        public async Task Una_entrega_a_comunidad_sin_nombre_de_comunidad_se_rechaza()
        {
            await Assert.ThrowsAsync<ValidationException>(() => _servicio.RegistrarDonacionEntregadaAsync(new DonacionEntregadaCrearDto
            {
                ArticuloId = 1, Cantidad = 1, Fecha = DateTime.Today, TipoDestinatario = TiposDestinatarioDonacion.Comunidad
            }));
        }

        [Fact]
        public async Task Una_entrega_a_comunidad_con_beneficiario_a_la_vez_se_rechaza()
        {
            await Assert.ThrowsAsync<ValidationException>(() => _servicio.RegistrarDonacionEntregadaAsync(new DonacionEntregadaCrearDto
            {
                ArticuloId = 1, Cantidad = 1, Fecha = DateTime.Today,
                TipoDestinatario = TiposDestinatarioDonacion.Comunidad,
                ComunidadDestinataria = "Barrio Norte", BeneficiarioId = 5
            }));
        }

        // ---------------------------------------------------------------
        // Falsos (contra la interfaz de IDonacionesRepository de esta rama)
        // ---------------------------------------------------------------

        private sealed class RepositorioDonantesFalso : IDonantesRepository
        {
            public List<Donante> Donantes { get; } = new();

            // El donante nuevo se crea dentro de la transacción de la donación
            // (IDonacionesRepository.AgregarDonacion*Async), no por acá.
            public Task AgregarAsync(Donante donante) => throw new NotImplementedException();

            public Task<Donante?> ObtenerPorIdAsync(int id) => Task.FromResult(Donantes.FirstOrDefault(d => d.Id == id));
            public Task ActualizarAsync(Donante donante) => throw new NotImplementedException();
            public Task<ResultadoPaginado<Donante>> ObtenerTodosAsync(FiltrosDonanteDto filtros) => throw new NotImplementedException();
            public Task<bool> ExisteNombreAsync(string nombre, int? idExcluir = null) => throw new NotImplementedException();
            public Task CambiarEstadoAsync(int id, bool estado) => throw new NotImplementedException();
            public Task<ResumenRegistrosDto> ObtenerResumenAsync() => throw new NotImplementedException();
        }

        private sealed class RepositorioDonacionesFalso : IDonacionesRepository
        {
            public List<DonacionDinero> Dinero { get; } = new();
            public List<DonacionEspecie> Especie { get; } = new();

            public Task AgregarDonacionDineroAsync(DonacionDinero donacion, Donante? nuevoDonante)
            {
                donacion.Id = Dinero.Count + 1;
                Dinero.Add(donacion);
                if (nuevoDonante is not null)
                    nuevoDonante.Id = 100 + Dinero.Count;
                return Task.CompletedTask;
            }

            public Task AgregarDonacionEspecieAsync(
                DonacionEspecie donacion, Donante? nuevoDonante, IReadOnlyList<EntradaInventario> entradas)
            {
                donacion.Id = Especie.Count + 1;
                Especie.Add(donacion);
                if (nuevoDonante is not null)
                    nuevoDonante.Id = 100 + Especie.Count;
                return Task.CompletedTask;
            }

            public Task AgregarDonacionEntregadaAsync(DonacionEntregada donacion, SalidaInventario salida) => throw new NotImplementedException();
            public Task<IEnumerable<DonacionDinero>> ObtenerDonacionesDineroAsync(FiltrosHistorialDonacionDto filtros) => throw new NotImplementedException();
            public Task<IEnumerable<DonacionEspecie>> ObtenerDonacionesEspecieAsync(FiltrosHistorialDonacionDto filtros) => throw new NotImplementedException();
            public Task<ResultadoPaginado<ItemHistorialDonacion>> ObtenerPaginaHistorialAsync(FiltrosHistorialDonacionDto filtros) => throw new NotImplementedException();
            public Task<IReadOnlyList<MontoPorMonedaDto>> ObtenerTotalesDineroPorMonedaAsync(FiltrosHistorialDonacionDto filtros) => throw new NotImplementedException();
            public Task<IEnumerable<DonacionEntregada>> ObtenerEntregasAsync(FiltrosHistorialEntregaDto filtros) => throw new NotImplementedException();
            public Task<IReadOnlyList<ConteoDonacionMensualDto>> ObtenerCantidadPorMesAsync(int mesesHaciaAtras) => throw new NotImplementedException();
            public Task<IReadOnlyList<MontoPorMesDto>> ObtenerDineroEnColonesPorMesAsync(int mesesHaciaAtras) => throw new NotImplementedException();
            public Task<IReadOnlyList<DonacionesPorDonanteDto>> ObtenerTopDonantesAsync(int mesesHaciaAtras, int maximo) => throw new NotImplementedException();
        }
    }

    // El validador de donantes lo comparten la pantalla de donantes y el formulario de
    // donación: una regla acá vale para los dos caminos.
    public class DonanteValidadorReglasTests
    {
        [Fact]
        public void El_nombre_se_guarda_sin_espacios_de_mas()
        {
            var datos = DonanteValidator.Validar("  Ana   Rojas ", TiposPersonaDonante.Todos[0], null, null, null);

            Assert.Equal("Ana Rojas", datos.Nombre);
        }

        [Fact]
        public void Un_nombre_vacio_se_rechaza()
        {
            Assert.Throws<ValidationException>(() => DonanteValidator.ValidarNombre("   "));
        }

        [Fact]
        public void Un_nombre_demasiado_largo_se_rechaza()
        {
            Assert.Throws<ValidationException>(() =>
                DonanteValidator.ValidarNombre(new string('a', DonanteValidator.LongitudMaximaNombre + 1)));
        }

        [Fact]
        public void Un_nombre_con_la_longitud_maxima_se_acepta()
        {
            var nombre = new string('a', DonanteValidator.LongitudMaximaNombre);

            Assert.Equal(nombre, DonanteValidator.ValidarNombre(nombre));
        }

        [Theory]
        [InlineData("")]
        [InlineData("Marciano")]
        public void Un_tipo_de_persona_fuera_del_catalogo_se_rechaza(string tipo)
        {
            Assert.Throws<ValidationException>(() => DonanteValidator.ValidarTipoPersona(tipo));
        }

        [Fact]
        public void Todo_tipo_de_persona_del_catalogo_se_acepta()
        {
            foreach (var tipo in TiposPersonaDonante.Todos)
                Assert.Equal(tipo, DonanteValidator.ValidarTipoPersona(tipo));
        }

        [Fact]
        public void Un_correo_vacio_queda_en_null_y_uno_demasiado_largo_se_rechaza()
        {
            Assert.Null(DonanteValidator.ValidarCorreo("  "));
            Assert.Throws<ValidationException>(() =>
                DonanteValidator.ValidarCorreo(new string('a', DonanteValidator.LongitudMaximaCorreo) + "@x.com"));
        }

        [Fact]
        public void Sin_telefono_se_guardan_los_dos_campos_en_null()
        {
            var datos = DonanteValidator.Validar("Ana", TiposPersonaDonante.Todos[0], null, null, null);

            Assert.Null(datos.Telefono);
            Assert.Null(datos.CodigoPaisTelefono);
        }
    }
}
