using SIGAC.Application.DTOs.Beneficiarios;

namespace SIGAC.Tests.Application
{
    // El repositorio aplica un filtro por cada palabra de PalabrasBusqueda (todas
    // tienen que estar, cada una en algún campo). La consulta usa la collation de
    // SQL Server y no corre sin base, así que acá se prueba la separación, que es
    // lo que decide qué se busca.
    public class BusquedaBeneficiariosTests
    {
        [Fact]
        public void Una_sola_palabra_se_busca_tal_cual()
        {
            var filtros = new FiltrosBeneficiarioDto { Nombre = "María" };

            Assert.Equal(new[] { "María" }, filtros.PalabrasBusqueda);
        }

        [Fact]
        public void Nombre_y_apellido_se_buscan_como_dos_palabras()
        {
            var filtros = new FiltrosBeneficiarioDto { Nombre = "María Pérez" };

            Assert.Equal(new[] { "María", "Pérez" }, filtros.PalabrasBusqueda);
        }

        [Fact]
        public void Espacios_de_mas_tabuladores_y_bordes_no_generan_palabras_vacias()
        {
            var filtros = new FiltrosBeneficiarioDto { Nombre = "  María \t  Pérez   Mora " };

            Assert.Equal(new[] { "María", "Pérez", "Mora" }, filtros.PalabrasBusqueda);
        }

        [Fact]
        public void Una_palabra_repetida_se_busca_una_sola_vez_sin_importar_mayusculas()
        {
            var filtros = new FiltrosBeneficiarioDto { Nombre = "maria MARÍA María" };

            // "maria" y "MARÍA" difieren en la tilde: son dos palabras para la
            // separación, aunque la collation de la base las trate igual.
            Assert.Equal(new[] { "maria", "MARÍA" }, filtros.PalabrasBusqueda);
        }

        [Fact]
        public void Un_numero_de_identidad_se_busca_como_una_sola_palabra()
        {
            var filtros = new FiltrosBeneficiarioDto { Nombre = "123456789" };

            Assert.Equal(new[] { "123456789" }, filtros.PalabrasBusqueda);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Sin_texto_no_hay_palabras_y_no_se_filtra_por_nombre(string? nombre)
        {
            var filtros = new FiltrosBeneficiarioDto { Nombre = nombre };

            Assert.Empty(filtros.PalabrasBusqueda);
        }
    }
}
