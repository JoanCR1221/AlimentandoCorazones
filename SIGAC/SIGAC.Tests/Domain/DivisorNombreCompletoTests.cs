using SIGAC.Domain;

namespace SIGAC.Tests.Domain
{
    public class DivisorNombreCompletoTests
    {
        [Theory]
        // Caso común: dos nombres y dos apellidos.
        [InlineData("JUAN CARLOS PEREZ MORA", "Juan", "Carlos", "Perez", "Mora")]
        // Un solo nombre.
        [InlineData("JUAN PEREZ MORA", "Juan", null, "Perez", "Mora")]
        // Sin segundo apellido.
        [InlineData("JUAN PEREZ", "Juan", null, "Perez", null)]
        // Partículas pegadas al apellido, en minúscula dentro del campo.
        [InlineData("JUAN DE LA CRUZ MORA", "Juan", null, "De la Cruz", "Mora")]
        [InlineData("ANA PEREZ DEL VALLE", "Ana", null, "Perez", "Del Valle")]
        // Nombre compuesto con partículas: todo lo que sobra es el segundo nombre.
        [InlineData("MARIA DE LOS ANGELES PEREZ MORA", "Maria", "De los Angeles", "Perez", "Mora")]
        // Guion y apóstrofe.
        [InlineData("LUIS SANCHEZ-MORA D'AVILA", "Luis", null, "Sanchez-Mora", "D'Avila")]
        // Espacios sobrantes y minúsculas.
        [InlineData("  juan   perez  mora ", "Juan", null, "Perez", "Mora")]
        public void Reparte_el_nombre_completo(
            string entrada, string primerNombre, string? segundoNombre, string primerApellido, string? segundoApellido)
        {
            var partes = DivisorNombreCompleto.Dividir(entrada);

            Assert.NotNull(partes);
            Assert.Equal(primerNombre, partes!.Value.PrimerNombre);
            Assert.Equal(segundoNombre, partes.Value.SegundoNombre);
            Assert.Equal(primerApellido, partes.Value.PrimerApellido);
            Assert.Equal(segundoApellido, partes.Value.SegundoApellido);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("JUAN")] // una sola palabra: no se sabe si es nombre o apellido
        public void Sin_al_menos_dos_palabras_no_reparte(string? entrada)
        {
            Assert.Null(DivisorNombreCompleto.Dividir(entrada));
        }

        // Para el campo único de nombre de donantes y arrendatarios.
        [Theory]
        [InlineData("JUAN CARLOS PEREZ MORA", "Juan Carlos Perez Mora")]
        [InlineData("MARIA DE LOS ANGELES PEREZ MORA", "Maria de los Angeles Perez Mora")]
        [InlineData("  ana   rojas ", "Ana Rojas")]
        [InlineData("JUAN", "Juan")]
        [InlineData("", "")]
        [InlineData(null, "")]
        public void Formatea_el_nombre_en_un_solo_campo(string? entrada, string esperado)
        {
            Assert.Equal(esperado, DivisorNombreCompleto.FormatearNombre(entrada));
        }

        [Fact]
        public void El_resultado_cumple_el_formato_que_exige_el_validador()
        {
            var partes = DivisorNombreCompleto.Dividir("MARIA DE LOS ANGELES SANCHEZ-MORA D'AVILA");

            Assert.NotNull(partes);
            Assert.True(ReglasBeneficiario.TieneFormatoValido(partes!.Value.PrimerNombre));
            Assert.True(ReglasBeneficiario.TieneFormatoValido(partes.Value.SegundoNombre));
            Assert.True(ReglasBeneficiario.TieneFormatoValido(partes.Value.PrimerApellido));
            Assert.True(ReglasBeneficiario.TieneFormatoValido(partes.Value.SegundoApellido));
        }
    }
}
