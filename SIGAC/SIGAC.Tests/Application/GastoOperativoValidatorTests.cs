using SIGAC.Application.DTOs.Gastos;
using SIGAC.Application.Exceptions;
using SIGAC.Application.Validators;
using SIGAC.Domain;

namespace SIGAC.Tests.Application
{
    public class GastoOperativoValidatorTests
    {
        private static GastoOperativoCrearDto GastoValido() => new()
        {
            TipoGastoId = 3,
            Proveedor = "Servicentro La Uruca",
            NumeroFactura = "00100001010000012345",
            Fecha = DateTime.Today,
            MontoSinIva = 25000m,
            Iva = 3250m,
            Descripcion = "Diésel para el vehículo de reparto",
            Responsable = "Ana Rojas"
        };

        [Fact]
        public void Gasto_nuevo_trae_los_valores_por_defecto_del_reporte()
        {
            var datos = GastoOperativoValidator.Validar(GastoValido());

            Assert.Equal(TiposMoneda.Colones, datos.Moneda);
            Assert.Equal(FormasPago.Contado, datos.FormaPago);
            Assert.Equal(ReglasGastoOperativo.CuentaContablePorDefecto, datos.CuentaContable);
            Assert.Equal(ReglasGastoOperativo.DescripcionCuentaPorDefecto, datos.DescripcionCuenta);
        }

        [Fact]
        public void Iva_en_cero_es_valido()
        {
            var dto = GastoValido();
            dto.Iva = 0m;

            Assert.Equal(0m, GastoOperativoValidator.Validar(dto).Iva);
        }

        [Fact]
        public void Iva_negativo_se_rechaza()
        {
            var dto = GastoValido();
            dto.Iva = -1m;

            Assert.Throws<ValidationException>(() => GastoOperativoValidator.Validar(dto));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-10)]
        public void Monto_sin_iva_tiene_que_ser_positivo(decimal monto)
        {
            var dto = GastoValido();
            dto.MontoSinIva = monto;

            Assert.Throws<ValidationException>(() => GastoOperativoValidator.Validar(dto));
        }

        [Fact]
        public void Sin_tipo_de_gasto_se_rechaza()
        {
            var dto = GastoValido();
            dto.TipoGastoId = 0;

            Assert.Throws<ValidationException>(() => GastoOperativoValidator.Validar(dto));
        }

        [Fact]
        public void Forma_de_pago_fuera_del_catalogo_se_rechaza()
        {
            var dto = GastoValido();
            dto.FormaPago = "Trueque";

            Assert.Throws<ValidationException>(() => GastoOperativoValidator.Validar(dto));
        }

        [Fact]
        public void Proveedor_se_guarda_con_los_espacios_compactados()
        {
            var dto = GastoValido();
            dto.Proveedor = "  Coopeagua   R.L. ";

            Assert.Equal("Coopeagua R.L.", GastoOperativoValidator.Validar(dto).Proveedor);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void Proveedor_y_factura_son_obligatorios(string vacio)
        {
            var sinProveedor = GastoValido();
            sinProveedor.Proveedor = vacio;
            var sinFactura = GastoValido();
            sinFactura.NumeroFactura = vacio;

            Assert.Throws<ValidationException>(() => GastoOperativoValidator.Validar(sinProveedor));
            Assert.Throws<ValidationException>(() => GastoOperativoValidator.Validar(sinFactura));
        }

        [Fact]
        public void Numero_de_cheque_es_opcional_en_cualquier_forma_de_pago()
        {
            foreach (var forma in FormasPago.Todos)
            {
                var dto = GastoValido();
                dto.FormaPago = forma;
                dto.NumeroCheque = "  ";

                Assert.Null(GastoOperativoValidator.Validar(dto).NumeroCheque);
            }
        }

        [Fact]
        public void Proveedor_mas_largo_que_la_columna_se_rechaza()
        {
            var dto = GastoValido();
            dto.Proveedor = new string('x', ReglasGastoOperativo.LongitudMaximaProveedor + 1);

            Assert.Throws<ValidationException>(() => GastoOperativoValidator.Validar(dto));
        }
    }
}
