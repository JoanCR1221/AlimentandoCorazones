using SIGAC.Application.DTOs.Gastos;
using SIGAC.Application.Exceptions;
using SIGAC.Application.Services;
using SIGAC.Domain;

namespace SIGAC.Tests.Application
{
    public class TiposGastoServiceTests
    {
        private readonly RepositorioTiposGastoFalso _tipos = new();
        private readonly BitacoraGastosFalsa _bitacora = new();
        private readonly TiposGastoService _servicio;

        public TiposGastoServiceTests()
        {
            _servicio = new TiposGastoService(_tipos, _bitacora);
        }

        [Fact]
        public async Task Registrar_normaliza_el_nombre_y_nace_activo()
        {
            await _servicio.RegistrarAsync(new TipoGastoGuardarDto
            {
                Nombre = "  Gas   LP ",
                GeneraInventario = true,
                CuentaContablePorDefecto = " 1 CAJA  Y BANCOS "
            });

            var tipo = Assert.Single(_tipos.Tipos);
            Assert.Equal("Gas LP", tipo.Nombre);
            Assert.True(tipo.Activo);
            Assert.True(tipo.GeneraInventario);
            Assert.Equal("1 CAJA Y BANCOS", tipo.CuentaContablePorDefecto);

            var registro = Assert.Single(_bitacora.Registros);
            Assert.Equal(AccionesBitacora.Registrar, registro.Accion);
            Assert.Equal(ModulosSistema.Gastos, registro.Modulo);
        }

        [Fact]
        public async Task Cuenta_por_defecto_vacia_se_guarda_como_null()
        {
            await _servicio.RegistrarAsync(new TipoGastoGuardarDto { Nombre = "Gas LP", CuentaContablePorDefecto = "   " });

            Assert.Null(_tipos.Tipos.Single().CuentaContablePorDefecto);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public async Task Nombre_vacio_se_rechaza(string nombre)
        {
            await Assert.ThrowsAsync<ValidationException>(() =>
                _servicio.RegistrarAsync(new TipoGastoGuardarDto { Nombre = nombre }));

            Assert.Empty(_tipos.Tipos);
        }

        [Fact]
        public async Task Nombre_mas_largo_que_la_columna_se_rechaza()
        {
            await Assert.ThrowsAsync<ValidationException>(() => _servicio.RegistrarAsync(new TipoGastoGuardarDto
            {
                Nombre = new string('x', ReglasGastoOperativo.LongitudMaximaNombreTipo + 1)
            }));
        }

        // Partiría en dos el mismo subtotal del reporte.
        [Theory]
        [InlineData("Mantenimiento de vehiculo")]
        [InlineData("MANTENIMIENTO DE VEHÍCULO")]
        [InlineData("  mantenimiento   de Vehículo ")]
        public async Task Nombre_repetido_sin_distinguir_tildes_ni_mayusculas_se_rechaza(string nombre)
        {
            _tipos.Agregar("Mantenimiento de Vehículo");

            await Assert.ThrowsAsync<DuplicateException>(() =>
                _servicio.RegistrarAsync(new TipoGastoGuardarDto { Nombre = nombre }));

            Assert.Single(_tipos.Tipos);
        }

        [Fact]
        public async Task Repetir_el_nombre_de_un_tipo_inactivo_sugiere_reactivarlo()
        {
            _tipos.Agregar("Viáticos", activo: false);

            var ex = await Assert.ThrowsAsync<DuplicateException>(() =>
                _servicio.RegistrarAsync(new TipoGastoGuardarDto { Nombre = "Viaticos" }));

            Assert.Contains("Reactívelo", ex.Message);
        }

        [Fact]
        public async Task Editar_cambia_nombre_inventario_y_cuenta()
        {
            var tipo = _tipos.Agregar("Gas", cuenta: "1 CAJA Y BANCOS");

            await _servicio.EditarAsync(tipo.Id, new TipoGastoGuardarDto
            {
                Nombre = "Gas LP",
                GeneraInventario = true,
                CuentaContablePorDefecto = "2 BANCOS"
            });

            var editado = _tipos.Tipos.Single();
            Assert.Equal("Gas LP", editado.Nombre);
            Assert.True(editado.GeneraInventario);
            Assert.Equal("2 BANCOS", editado.CuentaContablePorDefecto);
            Assert.True(editado.Activo);
            Assert.Equal(AccionesBitacora.Editar, Assert.Single(_bitacora.Registros).Accion);
        }

        // Corregir mayúsculas o tildes del propio nombre no es un duplicado.
        [Fact]
        public async Task Editar_conservando_el_propio_nombre_no_es_duplicado()
        {
            var tipo = _tipos.Agregar("Amenidades");

            await _servicio.EditarAsync(tipo.Id, new TipoGastoGuardarDto { Nombre = "AMENIDADES" });

            Assert.Equal("AMENIDADES", _tipos.Tipos.Single().Nombre);
        }

        [Fact]
        public async Task Editar_con_el_nombre_de_otro_tipo_se_rechaza()
        {
            _tipos.Agregar("Combustible");
            var otro = _tipos.Agregar("Amenidades");

            await Assert.ThrowsAsync<DuplicateException>(() =>
                _servicio.EditarAsync(otro.Id, new TipoGastoGuardarDto { Nombre = "combustible" }));

            Assert.Equal("Amenidades", _tipos.Tipos.Single(t => t.Id == otro.Id).Nombre);
        }

        [Fact]
        public async Task Editar_un_tipo_inexistente_lanza_NotFound()
        {
            await Assert.ThrowsAsync<NotFoundException>(() =>
                _servicio.EditarAsync(42, new TipoGastoGuardarDto { Nombre = "X" }));
        }

        [Fact]
        public async Task Desactivar_y_activar_mueven_el_estado_y_quedan_en_la_bitacora()
        {
            var tipo = _tipos.Agregar("Combustible");

            await _servicio.DesactivarAsync(tipo.Id);
            Assert.False(_tipos.Tipos.Single().Activo);

            await _servicio.ActivarAsync(tipo.Id);
            Assert.True(_tipos.Tipos.Single().Activo);

            Assert.Equal(
                new[] { AccionesBitacora.Desactivar, AccionesBitacora.Activar },
                _bitacora.Registros.Select(r => r.Accion));
        }

        [Fact]
        public async Task Cambiar_estado_de_un_tipo_inexistente_lanza_NotFound()
        {
            await Assert.ThrowsAsync<NotFoundException>(() => _servicio.DesactivarAsync(42));
            await Assert.ThrowsAsync<NotFoundException>(() => _servicio.ActivarAsync(42));
        }

        [Fact]
        public async Task Listado_de_administracion_incluye_los_inactivos()
        {
            _tipos.Agregar("Combustible");
            _tipos.Agregar("Viáticos", activo: false);

            var tipos = await _servicio.ObtenerTodosAsync();

            Assert.Equal(2, tipos.Count);
            Assert.Contains(tipos, t => t.Nombre == "Viáticos" && !t.Activo);
        }
    }
}
