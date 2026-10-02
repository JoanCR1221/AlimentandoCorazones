using SIGAC.Application.DTOs.Alquileres;
using SIGAC.Application.Exceptions;
using SIGAC.Application.Validators;
using SIGAC.Domain;

namespace SIGAC.Tests.Application
{
    public class ArrendatarioValidatorTests
    {
        [Theory]
        [InlineData("1-2345-6789", "123456789")]
        [InlineData("1 2345 6789", "123456789")]
        [InlineData("3-101-123456", "3101123456")]
        [InlineData("ab123456", "AB123456")]
        public void La_identificacion_se_guarda_sin_guiones_ni_espacios(string escrita, string guardada)
        {
            Assert.Equal(guardada, ArrendatarioValidator.NormalizarIdentificacion(escrita));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("  - ")]
        public void La_identificacion_vacia_queda_en_null(string? escrita)
        {
            Assert.Null(ArrendatarioValidator.NormalizarIdentificacion(escrita));
        }

        [Fact]
        public void La_identificacion_con_simbolos_se_rechaza()
        {
            Assert.Throws<ValidationException>(() => ArrendatarioValidator.NormalizarIdentificacion("1.2345.6789"));
        }

        [Fact]
        public void El_telefono_es_obligatorio()
        {
            var dto = new ArrendatarioCrearDto { Nombre = "Ana Rojas", TipoPersona = TiposPersonaDonante.Fisica };

            var ex = Assert.Throws<ValidationException>(() => ArrendatarioValidator.Validar(dto));
            Assert.Contains("teléfono", ex.Message);
        }

        [Fact]
        public void Un_arrendatario_valido_queda_normalizado()
        {
            var dto = new ArrendatarioCrearDto
            {
                Nombre = "  Ana   Rojas ",
                TipoPersona = TiposPersonaDonante.Fisica,
                Identificacion = "1-2345-6789",
                Telefono = "88887777",
                Correo = " "
            };

            var datos = ArrendatarioValidator.Validar(dto);

            Assert.Equal("Ana Rojas", datos.Nombre);
            Assert.Equal("123456789", datos.Identificacion);
            Assert.Equal(ReglasTelefono.CodigoPaisCostaRica, datos.CodigoPaisTelefono);
            Assert.Equal("88887777", datos.Telefono);
            Assert.Null(datos.Correo);
        }
    }
}
