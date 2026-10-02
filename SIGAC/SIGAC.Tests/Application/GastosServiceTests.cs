using SIGAC.Application.DTOs.Gastos;
using SIGAC.Application.Exceptions;
using SIGAC.Application.Services;
using SIGAC.Domain;
using SIGAC.Domain.Entities;

namespace SIGAC.Tests.Application
{
    public class GastosServiceTests
    {
        private readonly RepositorioTiposGastoFalso _tipos = new();
        private readonly RepositorioGastosFalso _gastos;
        private readonly BitacoraGastosFalsa _bitacora = new();
        private readonly GastosService _servicio;

        private readonly TipoGasto _combustible;
        private readonly TipoGasto _materiales;
        private readonly TipoGasto _inactivo;

        public GastosServiceTests()
        {
            _gastos = new RepositorioGastosFalso(_tipos);
            _servicio = new GastosService(_gastos, _tipos, _bitacora);

            _combustible = _tipos.Agregar("Combustible");
            _materiales = _tipos.Agregar("Materiales y Suministros", generaInventario: true);
            _inactivo = _tipos.Agregar("Viáticos", activo: false);
        }

        // --- Registrar ---

        [Fact]
        public async Task Registrar_guarda_todos_los_campos_normalizados()
        {
            var dto = DatosGasto.Crear(_combustible.Id);
            dto.Proveedor = "  Servicentro   La Uruca ";
            dto.NumeroCheque = "  ";
            dto.FormaPago = FormasPago.Credito;
            dto.Moneda = TiposMoneda.Dolares;

            var id = await _servicio.RegistrarGastoAsync(dto);

            var gasto = Assert.Single(_gastos.Gastos);
            Assert.Equal(id, gasto.Id);
            Assert.Equal(_combustible.Id, gasto.TipoGastoId);
            Assert.Equal("Servicentro La Uruca", gasto.Proveedor);
            Assert.Equal("FAC-001", gasto.NumeroFactura);
            Assert.Equal(10000m, gasto.MontoSinIva);
            Assert.Equal(1300m, gasto.Iva);
            Assert.Equal(TiposMoneda.Dolares, gasto.Moneda);
            Assert.Equal(FormasPago.Credito, gasto.FormaPago);
            Assert.Null(gasto.NumeroCheque);
            Assert.Equal(ReglasGastoOperativo.CuentaContablePorDefecto, gasto.CuentaContable);
            Assert.Equal(ReglasGastoOperativo.DescripcionCuentaPorDefecto, gasto.DescripcionCuenta);
            Assert.Equal(EstadoGastoOperativo.Activo, gasto.Estado);
            Assert.Null(gasto.MotivoAnulacion);
            Assert.NotEqual(default, gasto.FechaRegistro);
        }

        [Fact]
        public async Task Registrar_deja_rastro_en_la_bitacora_con_tipo_proveedor_y_factura()
        {
            await _servicio.RegistrarGastoAsync(DatosGasto.Crear(_combustible.Id));

            var registro = Assert.Single(_bitacora.Registros);
            Assert.Equal(AccionesBitacora.Registrar, registro.Accion);
            Assert.Equal(ModulosSistema.Gastos, registro.Modulo);
            Assert.Contains("Combustible", registro.Detalle);
            Assert.Contains("Servicentro La Uruca", registro.Detalle);
            Assert.Contains("FAC-001", registro.Detalle);
        }

        [Fact]
        public async Task Registrar_con_tipo_inexistente_se_rechaza_y_no_guarda()
        {
            await Assert.ThrowsAsync<ValidationException>(() => _servicio.RegistrarGastoAsync(DatosGasto.Crear(999)));

            Assert.Empty(_gastos.Gastos);
            Assert.Empty(_bitacora.Registros);
        }

        [Fact]
        public async Task Registrar_con_tipo_inactivo_se_rechaza()
        {
            var ex = await Assert.ThrowsAsync<ValidationException>(() => _servicio.RegistrarGastoAsync(DatosGasto.Crear(_inactivo.Id)));

            Assert.Contains("inactivo", ex.Message);
            Assert.Empty(_gastos.Gastos);
        }

        [Fact]
        public async Task Registrar_con_datos_invalidos_no_llega_al_repositorio()
        {
            var dto = DatosGasto.Crear(_combustible.Id);
            dto.MontoSinIva = 0;

            await Assert.ThrowsAsync<ValidationException>(() => _servicio.RegistrarGastoAsync(dto));
            Assert.Empty(_gastos.Gastos);
        }

        // --- Editar ---

        [Fact]
        public async Task Obtener_para_editar_devuelve_todos_los_campos()
        {
            var gasto = DatosGasto.Entidad(1, _combustible.Id, 500m, 65m, TiposMoneda.Euros);
            gasto.NumeroCheque = "CH-9";
            _gastos.Gastos.Add(gasto);

            var dto = await _servicio.ObtenerParaEditarAsync(1);

            Assert.NotNull(dto);
            Assert.Equal(_combustible.Id, dto.TipoGastoId);
            Assert.Equal("Proveedor 1", dto.Proveedor);
            Assert.Equal("F-1", dto.NumeroFactura);
            Assert.Equal(500m, dto.MontoSinIva);
            Assert.Equal(65m, dto.Iva);
            Assert.Equal(TiposMoneda.Euros, dto.Moneda);
            Assert.Equal("CH-9", dto.NumeroCheque);
            Assert.Equal(ReglasGastoOperativo.CuentaContablePorDefecto, dto.CuentaContable);
        }

        [Fact]
        public async Task Obtener_para_editar_un_gasto_inexistente_devuelve_null()
        {
            Assert.Null(await _servicio.ObtenerParaEditarAsync(42));
        }

        // Regresión del bug de Moneda: la edición tiene que llevar TODOS los campos
        // a la entidad, Moneda incluida.
        [Fact]
        public async Task Editar_aplica_todos_los_campos_incluida_la_moneda()
        {
            _gastos.Gastos.Add(DatosGasto.Entidad(1, _combustible.Id, 500m, 65m, TiposMoneda.Colones));

            await _servicio.EditarGastoAsync(1, new GastoOperativoEditarDto
            {
                TipoGastoId = _materiales.Id,
                Proveedor = "Ferretería El Clavo",
                NumeroFactura = "F-77",
                Fecha = DateTime.Today.AddDays(-3),
                MontoSinIva = 800m,
                Iva = 0m,
                Moneda = TiposMoneda.Dolares,
                FormaPago = FormasPago.Credito,
                NumeroCheque = "CH-1",
                CuentaContable = "2 BANCO NACIONAL",
                DescripcionCuenta = "GASTOS DE OPERACION",
                Descripcion = "Tornillos",
                Responsable = "Luis Mora"
            });

            var gasto = _gastos.Gastos.Single();
            Assert.Equal(_materiales.Id, gasto.TipoGastoId);
            Assert.Equal("Ferretería El Clavo", gasto.Proveedor);
            Assert.Equal("F-77", gasto.NumeroFactura);
            Assert.Equal(DateTime.Today.AddDays(-3), gasto.Fecha);
            Assert.Equal(800m, gasto.MontoSinIva);
            Assert.Equal(0m, gasto.Iva);
            Assert.Equal(TiposMoneda.Dolares, gasto.Moneda);
            Assert.Equal(FormasPago.Credito, gasto.FormaPago);
            Assert.Equal("CH-1", gasto.NumeroCheque);
            Assert.Equal("2 BANCO NACIONAL", gasto.CuentaContable);
            Assert.Equal("GASTOS DE OPERACION", gasto.DescripcionCuenta);
            Assert.Equal("Tornillos", gasto.Descripcion);
            Assert.Equal("Luis Mora", gasto.Responsable);

            var registro = Assert.Single(_bitacora.Registros);
            Assert.Equal(AccionesBitacora.Editar, registro.Accion);
            Assert.Contains(TiposMoneda.Dolares, registro.Detalle);
        }

        [Fact]
        public async Task Editar_un_gasto_inexistente_lanza_NotFound()
        {
            await Assert.ThrowsAsync<NotFoundException>(() =>
                _servicio.EditarGastoAsync(42, ADtoEditar(DatosGasto.Crear(_combustible.Id))));
        }

        [Fact]
        public async Task Editar_un_gasto_anulado_se_rechaza()
        {
            _gastos.Gastos.Add(DatosGasto.Entidad(1, _combustible.Id, 500m, 0m, estado: EstadoGastoOperativo.Anulado));

            await Assert.ThrowsAsync<ValidationException>(() =>
                _servicio.EditarGastoAsync(1, ADtoEditar(DatosGasto.Crear(_combustible.Id))));
        }

        // Un gasto viejo cuyo tipo se desactivó después tiene que poder corregirse
        // sin obligar a cambiarle el tipo.
        [Fact]
        public async Task Editar_conservando_un_tipo_que_ya_se_desactivo_esta_permitido()
        {
            _gastos.Gastos.Add(DatosGasto.Entidad(1, _inactivo.Id, 500m, 0m));
            var dto = ADtoEditar(DatosGasto.Crear(_inactivo.Id));
            dto.NumeroFactura = "CORREGIDA";

            await _servicio.EditarGastoAsync(1, dto);

            Assert.Equal("CORREGIDA", _gastos.Gastos.Single().NumeroFactura);
        }

        [Fact]
        public async Task Editar_cambiando_a_otro_tipo_inactivo_se_rechaza()
        {
            var otroInactivo = _tipos.Agregar("Transporte", activo: false);
            _gastos.Gastos.Add(DatosGasto.Entidad(1, _combustible.Id, 500m, 0m));

            await Assert.ThrowsAsync<ValidationException>(() =>
                _servicio.EditarGastoAsync(1, ADtoEditar(DatosGasto.Crear(otroInactivo.Id))));

            Assert.Equal(_combustible.Id, _gastos.Gastos.Single().TipoGastoId);
        }

        // --- Consultar ---

        [Fact]
        public async Task Listado_expone_el_nombre_del_tipo_y_el_total_con_iva()
        {
            _gastos.Gastos.Add(DatosGasto.Entidad(1, _combustible.Id, 1000m, 130m));

            var resultado = await _servicio.ObtenerGastosAsync(new FiltrosGastoDto());

            var fila = Assert.Single(resultado.Gastos);
            Assert.Equal("Combustible", fila.TipoGasto);
            Assert.Equal(1130m, fila.Total);
            Assert.Equal(nameof(EstadoGastoOperativo.Activo), fila.Estado);
        }

        [Fact]
        public async Task Totales_suman_monto_mas_iva_por_moneda_y_excluyen_anulados()
        {
            _gastos.Gastos.Add(DatosGasto.Entidad(1, _combustible.Id, 1000m, 130m, TiposMoneda.Colones));
            _gastos.Gastos.Add(DatosGasto.Entidad(2, _combustible.Id, 500m, 0m, TiposMoneda.Colones));
            _gastos.Gastos.Add(DatosGasto.Entidad(3, _combustible.Id, 20m, 2.60m, TiposMoneda.Dolares));
            _gastos.Gastos.Add(DatosGasto.Entidad(4, _combustible.Id, 9999m, 99m, TiposMoneda.Colones, EstadoGastoOperativo.Anulado));

            var resultado = await _servicio.ObtenerGastosAsync(new FiltrosGastoDto());

            Assert.Equal(4, resultado.Gastos.Count);
            Assert.Equal(2, resultado.TotalesPorMoneda.Count);
            Assert.Equal(1630m, resultado.TotalesPorMoneda.Single(t => t.Moneda == TiposMoneda.Colones).Total);
            Assert.Equal(22.60m, resultado.TotalesPorMoneda.Single(t => t.Moneda == TiposMoneda.Dolares).Total);
        }

        [Fact]
        public async Task Sin_gastos_activos_no_hay_totales()
        {
            _gastos.Gastos.Add(DatosGasto.Entidad(1, _combustible.Id, 100m, 0m, estado: EstadoGastoOperativo.Anulado));

            var resultado = await _servicio.ObtenerGastosAsync(new FiltrosGastoDto());

            Assert.Empty(resultado.TotalesPorMoneda);
        }

        // --- Anular ---

        [Fact]
        public async Task Anular_delega_en_el_repositorio_y_registra_el_motivo()
        {
            _gastos.Gastos.Add(DatosGasto.Entidad(1, _materiales.Id, 100m, 0m));

            await _servicio.AnularGastoAsync(new AnulacionGastoDto { GastoId = 1, MotivoAnulacion = "  Factura   duplicada " });

            Assert.Equal(new[] { 1 }, _gastos.Anulados);
            Assert.Equal("Factura duplicada", _gastos.Gastos.Single().MotivoAnulacion);
            var registro = Assert.Single(_bitacora.Registros);
            Assert.Equal(AccionesBitacora.Anular, registro.Accion);
            Assert.Contains("Factura duplicada", registro.Detalle);
        }

        [Fact]
        public async Task Anular_un_gasto_ya_anulado_se_rechaza()
        {
            _gastos.Gastos.Add(DatosGasto.Entidad(1, _combustible.Id, 100m, 0m, estado: EstadoGastoOperativo.Anulado));

            await Assert.ThrowsAsync<ValidationException>(() =>
                _servicio.AnularGastoAsync(new AnulacionGastoDto { GastoId = 1, MotivoAnulacion = "Otra vez" }));

            Assert.Empty(_gastos.Anulados);
        }

        [Fact]
        public async Task Anular_sin_motivo_se_rechaza()
        {
            _gastos.Gastos.Add(DatosGasto.Entidad(1, _combustible.Id, 100m, 0m));

            await Assert.ThrowsAsync<ValidationException>(() =>
                _servicio.AnularGastoAsync(new AnulacionGastoDto { GastoId = 1, MotivoAnulacion = "   " }));

            Assert.Empty(_gastos.Anulados);
        }

        [Fact]
        public async Task Anular_un_gasto_inexistente_lanza_NotFound()
        {
            await Assert.ThrowsAsync<NotFoundException>(() =>
                _servicio.AnularGastoAsync(new AnulacionGastoDto { GastoId = 42, MotivoAnulacion = "x" }));
        }

        // --- Tipos y proveedores para el formulario ---

        [Fact]
        public async Task Tipos_para_el_formulario_son_solo_los_activos_ordenados()
        {
            var tipos = await _servicio.ObtenerTiposActivosAsync();

            Assert.Equal(new[] { "Combustible", "Materiales y Suministros" }, tipos.Select(t => t.Nombre));
            Assert.True(tipos.Single(t => t.Id == _materiales.Id).GeneraInventario);
        }

        [Fact]
        public async Task Tipos_para_editar_incluyen_el_tipo_inactivo_del_gasto()
        {
            var tipos = await _servicio.ObtenerTiposActivosAsync(incluirTipoId: _inactivo.Id);

            Assert.Contains(tipos, t => t.Id == _inactivo.Id && !t.Activo);
            Assert.Equal(3, tipos.Count);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public async Task Buscar_proveedores_sin_texto_no_consulta(string texto)
        {
            var resultado = await _servicio.BuscarProveedoresAsync(texto);

            Assert.Empty(resultado);
            Assert.Null(_gastos.UltimaBusquedaProveedores);
        }

        [Fact]
        public async Task Buscar_proveedores_compacta_el_texto_y_limita_las_sugerencias()
        {
            var resultado = await _servicio.BuscarProveedoresAsync("  servicentro   la ");

            Assert.Equal("servicentro la", _gastos.UltimaBusquedaProveedores);
            Assert.Equal(10, _gastos.UltimoMaximoProveedores);
            Assert.Empty(resultado);
        }

        private static GastoOperativoEditarDto ADtoEditar(GastoOperativoCrearDto c) => new()
        {
            TipoGastoId = c.TipoGastoId,
            Proveedor = c.Proveedor,
            NumeroFactura = c.NumeroFactura,
            Fecha = c.Fecha,
            MontoSinIva = c.MontoSinIva,
            Iva = c.Iva,
            Moneda = c.Moneda,
            FormaPago = c.FormaPago,
            NumeroCheque = c.NumeroCheque,
            CuentaContable = c.CuentaContable,
            DescripcionCuenta = c.DescripcionCuenta,
            Descripcion = c.Descripcion,
            Responsable = c.Responsable
        };
    }
}
