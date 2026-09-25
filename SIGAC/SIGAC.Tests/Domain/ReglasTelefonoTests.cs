using SIGAC.Application.Exceptions;
using SIGAC.Application.Validators;
using SIGAC.Domain;

namespace SIGAC.Tests.Domain
{
    public class ReglasTelefonoTests
    {
        [Theory]
        [InlineData("506", "88887777", true)]
        [InlineData("506", "8888777", false)]       // Costa Rica: 8 exactos
        [InlineData("506", "888877776", false)]
        [InlineData("1", "2025550123", true)]       // Estados Unidos: 10
        [InlineData("1", "20255501234567", true)]   // 1 + 14 = 15, tope E.164
        [InlineData("1", "202555012345678", false)] // 1 + 15 = 16
        [InlineData("44", "2071234567", true)]
        [InlineData("1268", "4601234", true)]
        [InlineData("52", "123", false)]            // menos de 4
        [InlineData("1", "202-555-0123", false)]    // solo dígitos
        [InlineData("0", "88887777", false)]        // código inválido
        [InlineData("12345", "88887777", false)]
        [InlineData(null, "88887777", false)]
        [InlineData("506", null, false)]
        public void Numero_tiene_formato(string? codigo, string? numero, bool esperado)
        {
            Assert.Equal(esperado, ReglasTelefono.TieneFormatoNumero(codigo, numero));
        }

        [Theory]
        [InlineData("506", "8888-7777", "88887777")]
        [InlineData("506", "888877779999", "88887777")] // recorta a 8
        [InlineData("1", "(202) 555-0123", "2025550123")]
        [InlineData("1", "", "")]
        public void Filtrar_numero_deja_solo_digitos_hasta_el_maximo(string codigo, string valor, string esperado)
        {
            Assert.Equal(esperado, ReglasTelefono.FiltrarNumero(codigo, valor));
        }

        [Theory]
        [InlineData("506", "88887777", "+506 88887777")]
        [InlineData(null, "888888898", "888888898")] // teléfono viejo sin código
        [InlineData("1", null, null)]
        public void Formatear_para_listados(string? codigo, string? numero, string? esperado)
        {
            Assert.Equal(esperado, ReglasTelefono.Formatear(codigo, numero));
        }

        [Fact]
        public void Validador_sin_numero_guarda_los_dos_campos_en_null()
        {
            var resultado = TelefonoValidator.Validar("1", "  ");

            Assert.Null(resultado.CodigoPais);
            Assert.Null(resultado.Numero);
        }

        [Fact]
        public void Validador_sin_codigo_asume_Costa_Rica()
        {
            var resultado = TelefonoValidator.Validar(null, "88887777");

            Assert.Equal("506", resultado.CodigoPais);
            Assert.Equal("88887777", resultado.Numero);
        }

        [Fact]
        public void Validador_acepta_el_codigo_con_signo_mas()
        {
            var resultado = TelefonoValidator.Validar("+1", "2025550123");

            Assert.Equal("1", resultado.CodigoPais);
        }

        [Fact]
        public void Validador_rechaza_numero_de_Estados_Unidos_con_codigo_de_Costa_Rica()
        {
            var ex = Assert.Throws<ValidationException>(() => TelefonoValidator.Validar("506", "2025550123"));

            Assert.Contains("+506", ex.Message);
        }
    }
}
