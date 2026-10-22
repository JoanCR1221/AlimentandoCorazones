using SIGAC.Application.Exceptions;
using SIGAC.Application.Validators;

namespace SIGAC.Tests.Application
{
    // El correo se valida en el servidor porque el donante nuevo del formulario de
    // donación viaja anidado y el formulario no le aplica las DataAnnotations.
    public class DonanteValidatorTests
    {
        [Theory]
        [InlineData("sin-arroba")]
        [InlineData("ana@")]
        [InlineData("@dominio.com")]
        public void Un_correo_con_formato_invalido_se_rechaza(string correo)
        {
            var ex = Assert.Throws<ValidationException>(() => DonanteValidator.ValidarCorreo(correo));

            Assert.Contains("formato", ex.Message);
        }

        [Fact]
        public void Un_correo_valido_se_conserva_y_uno_vacio_queda_en_null()
        {
            Assert.Equal("ana@correo.com", DonanteValidator.ValidarCorreo(" ana@correo.com "));
            Assert.Null(DonanteValidator.ValidarCorreo("   "));
        }
    }
}
