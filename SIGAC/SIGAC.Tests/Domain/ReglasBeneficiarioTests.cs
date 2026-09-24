using SIGAC.Application.DTOs.Beneficiarios;
using SIGAC.Application.Exceptions;
using SIGAC.Application.Validators;
using SIGAC.Domain;

namespace SIGAC.Tests.Domain
{
    public class ReglasBeneficiarioTests
    {
        // ---- Nombres ----

        [Theory]
        [InlineData("María", true)]
        [InlineData("D'Ávila", true)]
        [InlineData("D’Ávila", true)]            // apóstrofo tipográfico
        [InlineData("Sánchez-Mora", true)]
        [InlineData("De la Cruz", true)]
        [InlineData("Müller", true)]
        [InlineData("Œdipe", true)]              // letra que agrega la página 1252
        [InlineData("Łukasz", false)]            // fuera de la 1252: se guardaría "?"
        [InlineData("Nguyễn", false)]
        [InlineData("Juan2", false)]
        [InlineData("Ana--María", false)]
        [InlineData("-Ana", false)]
        [InlineData("Ana  María", false)]        // espera el texto ya compactado
        [InlineData("", false)]
        [InlineData(null, false)]
        public void Nombre_tiene_formato(string? valor, bool esperado)
        {
            Assert.Equal(esperado, ReglasBeneficiario.TieneFormatoValido(valor));
        }

        [Fact]
        public void Normalizar_nombre_compone_tildes_separadas()
        {
            // "e" + tilde combinante (NFD) pasa a ser una sola "É".
            var normalizado = TextoNormalizador.NormalizarNombre("élise  de   la cruz");

            Assert.Equal("Élise de la cruz", normalizado);
            Assert.True(ReglasBeneficiario.TieneFormatoValido(normalizado));
        }

        // ---- Número de identidad según el tipo ----

        [Theory]
        [InlineData(TiposDocumento.CedulaNacional, "123456789", true)]
        [InlineData(TiposDocumento.CedulaNacional, "12345678", false)]
        [InlineData(TiposDocumento.CedulaNacional, "1-2345-6789", false)]
        [InlineData(TiposDocumento.Dimex, "12345678901", true)]
        [InlineData(TiposDocumento.Dimex, "123456789012", true)]
        [InlineData(TiposDocumento.Dimex, "1234567890", false)]
        [InlineData(TiposDocumento.Pasaporte, "AB-123456", true)]
        [InlineData(TiposDocumento.Pasaporte, "ab123456", true)]
        [InlineData(TiposDocumento.Pasaporte, "AB#123", false)] // antes el servidor lo aceptaba
        [InlineData(TiposDocumento.Pasaporte, "ÑA123", false)]
        [InlineData(TiposDocumento.Otro, "CN-2026-014", true)]
        [InlineData(TiposDocumento.Otro, "CN/2026", false)]
        [InlineData(TiposDocumento.SinDocumento, "123", false)]
        [InlineData(null, "123", false)]
        public void Num_identidad_tiene_formato_segun_tipo(string? tipo, string numero, bool esperado)
        {
            Assert.Equal(esperado, ReglasBeneficiario.TieneFormatoNumIdentidad(tipo, numero));
        }

        [Theory]
        [InlineData(TiposDocumento.CedulaNacional, "1-2345-6789-0", "123456789")]
        [InlineData(TiposDocumento.Pasaporte, "AB-12 #3", "AB-123")]
        [InlineData(TiposDocumento.Otro, "", "")]
        public void Filtrar_num_identidad(string tipo, string valor, string esperado)
        {
            Assert.Equal(esperado, ReglasBeneficiario.FiltrarNumIdentidad(tipo, valor));
        }

        // ---- Categoría derivada de la fecha de nacimiento ----

        [Theory]
        [InlineData("2014-09-25", CategoriasBeneficiario.Nino)]        // cumple 12 mañana
        [InlineData("2014-09-24", CategoriasBeneficiario.Adolescente)] // cumple 12 hoy
        [InlineData("2008-09-25", CategoriasBeneficiario.Adolescente)] // 17
        [InlineData("2008-09-24", CategoriasBeneficiario.Adulto)]      // 18
        [InlineData("1961-09-25", CategoriasBeneficiario.Adulto)]      // 64
        [InlineData("1961-09-24", CategoriasBeneficiario.AdultoMayor)] // 65
        public void Categoria_segun_edad_cumplida(string nacimiento, string esperada)
        {
            var referencia = new DateTime(2026, 9, 24);

            Assert.Equal(esperada,
                CategoriasBeneficiario.DerivarDesdeFechaNacimiento(DateTime.Parse(nacimiento), referencia));
        }

        // En los años no bisiestos cumple el 1 de marzo, no el 28 de febrero.
        [Fact]
        public void Edad_de_quien_nacio_un_29_de_febrero()
        {
            var nacimiento = new DateTime(2008, 2, 29);

            Assert.Equal(17, CategoriasBeneficiario.CalcularEdad(nacimiento, new DateTime(2026, 2, 28)));
            Assert.Equal(18, CategoriasBeneficiario.CalcularEdad(nacimiento, new DateTime(2026, 3, 1)));
        }

        // ---- Validador ----

        private static BeneficiarioCrearDto DtoValido() => new()
        {
            PrimerNombre = "  maría ",
            PrimerApellido = "d'ávila",
            FechaNacimiento = DateTime.Today.AddYears(-30),
            TipoDocumento = TiposDocumento.CedulaNacional,
            NumIdentidad = "1 2345 6789"
        };

        [Fact]
        public void Validador_normaliza_un_alta_valida()
        {
            var datos = BeneficiarioValidator.Validar(DtoValido());

            Assert.Equal("María", datos.PrimerNombre);
            Assert.Equal(string.Empty, datos.SegundoNombre);
            Assert.Equal("D'ávila", datos.PrimerApellido);
            Assert.Equal("123456789", datos.NumIdentidad);
            Assert.Null(datos.Telefono);
            Assert.Null(datos.CodigoPaisTelefono);
        }

        [Fact]
        public void Validador_informa_primero_el_campo_de_mas_arriba()
        {
            // Nombre y documento inválidos a la vez: antes salía el del documento.
            var dto = DtoValido();
            dto.PrimerNombre = "J";
            dto.NumIdentidad = "123";

            var ex = Assert.Throws<ValidationException>(() => BeneficiarioValidator.Validar(dto));

            Assert.StartsWith("El primer nombre", ex.Message);
        }

        [Fact]
        public void Validador_rechaza_letras_que_la_base_no_guarda()
        {
            var dto = DtoValido();
            dto.PrimerApellido = "Łukasz";

            var ex = Assert.Throws<ValidationException>(() => BeneficiarioValidator.Validar(dto));

            Assert.Contains("alfabeto latino", ex.Message);
        }

        [Fact]
        public void Validador_rechaza_pasaporte_con_simbolos()
        {
            var dto = DtoValido();
            dto.TipoDocumento = TiposDocumento.Pasaporte;
            dto.NumIdentidad = "AB#123";

            var ex = Assert.Throws<ValidationException>(() => BeneficiarioValidator.Validar(dto));

            Assert.Contains("no corresponde al tipo de documento", ex.Message);
        }

        [Fact]
        public void Validador_sin_documento_descarta_el_numero()
        {
            var dto = DtoValido();
            dto.TipoDocumento = TiposDocumento.SinDocumento;
            dto.TipoDocumentoOtro = "algo";

            var datos = BeneficiarioValidator.Validar(dto);

            Assert.Null(datos.NumIdentidad);
            Assert.Null(datos.TipoDocumentoOtro);
        }

        [Fact]
        public void Validador_otro_exige_el_nombre_del_documento()
        {
            var dto = DtoValido();
            dto.TipoDocumento = TiposDocumento.Otro;
            dto.NumIdentidad = "CN-2026-014";

            var ex = Assert.Throws<ValidationException>(() => BeneficiarioValidator.Validar(dto));

            Assert.Contains("cómo se llama el documento", ex.Message);
        }

        [Theory]
        [InlineData(1, "no puede ser futura")]
        [InlineData(-121 * 366, "supera los 120 años")]
        public void Validador_rechaza_fechas_fuera_de_rango(int dias, string mensaje)
        {
            var dto = DtoValido();
            dto.FechaNacimiento = DateTime.Today.AddDays(dias);

            var ex = Assert.Throws<ValidationException>(() => BeneficiarioValidator.Validar(dto));

            Assert.Contains(mensaje, ex.Message);
        }
    }
}
