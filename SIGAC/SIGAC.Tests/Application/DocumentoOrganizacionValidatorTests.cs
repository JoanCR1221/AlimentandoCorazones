using SIGAC.Application.DTOs.Beneficiarios;
using SIGAC.Application.DTOs.Donaciones;
using SIGAC.Application.Exceptions;
using SIGAC.Application.Validators;
using SIGAC.Domain;

namespace SIGAC.Tests.Application
{
    // Documento de donantes y arrendatarios. Los donantes y arrendatarios pueden ser
    // empresas y extranjeros: por eso hay cédula jurídica, DIMEX, pasaporte y "Otro".
    public class DocumentoOrganizacionValidatorTests
    {
        [Theory]
        [InlineData(TiposDocumento.CedulaNacional, "1-2345-6789", "123456789")]
        [InlineData(TiposDocumento.CedulaNacional, "1 2345 6789", "123456789")]
        [InlineData(TiposDocumento.CedulaJuridica, "3-101-123456", "3101123456")]
        [InlineData(TiposDocumento.Dimex, "123456789012", "123456789012")]
        // Extranjeros: pasaporte con letras, en minúscula y con guion.
        [InlineData(TiposDocumento.Pasaporte, "ab-123456", "AB123456")]
        [InlineData(TiposDocumento.Pasaporte, "X1234567", "X1234567")]
        public void Normaliza_y_acepta_el_numero_segun_el_tipo(string tipo, string escrito, string guardado)
        {
            var documento = DocumentoOrganizacionValidator.Validar(tipo, null, escrito);

            Assert.Equal(tipo, documento.TipoDocumento);
            Assert.Equal(guardado, documento.Identificacion);
            Assert.Null(documento.TipoDocumentoOtro);
        }

        [Fact]
        public void Un_documento_extranjero_se_registra_como_otro_con_su_nombre()
        {
            // Por ejemplo el EIN de una empresa estadounidense.
            var documento = DocumentoOrganizacionValidator.Validar(
                TiposDocumento.Otro, "  Tax   ID (EIN) ", "12-3456789");

            Assert.Equal(TiposDocumento.Otro, documento.TipoDocumento);
            Assert.Equal("Tax ID (EIN)", documento.TipoDocumentoOtro);
            Assert.Equal("123456789", documento.Identificacion);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("  ")]
        public void Sin_tipo_ni_numero_queda_sin_documento(string? tipo)
        {
            var documento = DocumentoOrganizacionValidator.Validar(tipo, null, null);

            Assert.Equal(TiposDocumento.SinDocumento, documento.TipoDocumento);
            Assert.Null(documento.Identificacion);
        }

        [Fact]
        public void Sin_documento_descarta_el_numero_y_el_nombre_del_documento()
        {
            var documento = DocumentoOrganizacionValidator.Validar(
                TiposDocumento.SinDocumento, "Algo", "123456789");

            Assert.Equal(TiposDocumento.SinDocumento, documento.TipoDocumento);
            Assert.Null(documento.TipoDocumentoOtro);
            Assert.Null(documento.Identificacion);
        }

        [Fact]
        public void Un_numero_sin_tipo_se_rechaza()
        {
            var ex = Assert.Throws<ValidationException>(
                () => DocumentoOrganizacionValidator.Validar(null, null, "123456789"));

            Assert.Contains("tipo de documento", ex.Message);
        }

        [Theory]
        [InlineData(TiposDocumento.CedulaNacional)]
        [InlineData(TiposDocumento.CedulaJuridica)]
        [InlineData(TiposDocumento.Pasaporte)]
        public void Con_un_tipo_que_pide_numero_el_numero_es_obligatorio(string tipo)
        {
            var ex = Assert.Throws<ValidationException>(
                () => DocumentoOrganizacionValidator.Validar(tipo, null, null));

            Assert.Contains("obligatorio", ex.Message);
        }

        [Theory]
        [InlineData(TiposDocumento.CedulaNacional, "12345678")]     // 8 dígitos
        [InlineData(TiposDocumento.CedulaNacional, "3101123456")]   // una jurídica no es nacional
        [InlineData(TiposDocumento.CedulaJuridica, "123456789")]    // una nacional no es jurídica
        [InlineData(TiposDocumento.Dimex, "1234567890")]            // 10 dígitos
        public void El_formato_del_numero_tiene_que_coincidir_con_el_tipo(string tipo, string numero)
        {
            Assert.Throws<ValidationException>(
                () => DocumentoOrganizacionValidator.Validar(tipo, null, numero));
        }

        [Fact]
        public void Otro_exige_decir_como_se_llama_el_documento()
        {
            Assert.Throws<ValidationException>(
                () => DocumentoOrganizacionValidator.Validar(TiposDocumento.Otro, " ", "AB123"));
        }

        [Fact]
        public void El_nombre_del_documento_solo_se_guarda_con_otro()
        {
            var documento = DocumentoOrganizacionValidator.Validar(TiposDocumento.Pasaporte, "Algo", "AB123456");

            Assert.Null(documento.TipoDocumentoOtro);
        }

        [Fact]
        public void Un_tipo_inventado_se_rechaza()
        {
            Assert.Throws<ValidationException>(
                () => DocumentoOrganizacionValidator.Validar("Licencia", null, "123"));
        }

        // ---- Qué tipos acepta cada quien ----

        [Fact]
        public void La_cedula_juridica_es_solo_para_donantes_y_arrendatarios()
        {
            Assert.DoesNotContain(TiposDocumento.CedulaJuridica, TiposDocumento.Todos);
            Assert.Contains(TiposDocumento.CedulaJuridica, TiposDocumento.TodosParaOrganizaciones);

            Assert.False(TiposDocumento.EsValido(TiposDocumento.CedulaJuridica));
            Assert.True(TiposDocumento.EsValidoParaOrganizacion(TiposDocumento.CedulaJuridica));
        }

        [Theory]
        [InlineData(TiposDocumento.CedulaNacional, true)]
        [InlineData(TiposDocumento.CedulaJuridica, true)]
        [InlineData(TiposDocumento.Dimex, false)]      // extranjeros: no están en ese servicio
        [InlineData(TiposDocumento.Pasaporte, false)]
        [InlineData(TiposDocumento.Otro, false)]
        [InlineData(TiposDocumento.SinDocumento, false)]
        [InlineData(null, false)]
        public void Solo_las_cedulas_se_consultan_en_hacienda(string? tipo, bool esperado)
        {
            Assert.Equal(esperado, TiposDocumento.SeConsultaEnHacienda(tipo));
        }

        // El formulario rellena "Tipo de persona" según el documento. "Otro" y "Sin
        // documento" no dicen nada: puede ser una empresa extranjera o un donante anónimo.
        [Theory]
        [InlineData(TiposDocumento.CedulaNacional, TiposPersonaDonante.Fisica)]
        [InlineData(TiposDocumento.Dimex, TiposPersonaDonante.Fisica)]
        [InlineData(TiposDocumento.Pasaporte, TiposPersonaDonante.Fisica)]
        [InlineData(TiposDocumento.CedulaJuridica, TiposPersonaDonante.Juridica)]
        [InlineData(TiposDocumento.Otro, null)]
        [InlineData(TiposDocumento.SinDocumento, null)]
        [InlineData(null, null)]
        [InlineData("Licencia", null)]
        public void El_tipo_de_documento_sugiere_el_tipo_de_persona(string? tipoDocumento, string? esperado)
        {
            Assert.Equal(esperado, TiposDocumento.TipoPersonaSugerido(tipoDocumento));
        }

        // Lo sugerido tiene que ser un valor que el validador acepte.
        [Fact]
        public void Lo_sugerido_es_siempre_un_tipo_de_persona_valido()
        {
            foreach (var tipo in TiposDocumento.TodosParaOrganizaciones)
            {
                var sugerido = TiposDocumento.TipoPersonaSugerido(tipo);

                Assert.True(sugerido is null || TiposPersonaDonante.EsValido(sugerido), tipo);
            }
        }

        // ---- Donantes ----

        [Fact]
        public void El_donante_extranjero_con_pasaporte_se_valida()
        {
            var datos = DonanteValidator.Validar(new DonanteCrearDto
            {
                Nombre = "John Smith",
                TipoPersona = TiposPersonaDonante.Fisica,
                TipoDocumento = TiposDocumento.Pasaporte,
                Identificacion = "A12345678",
                CodigoPaisTelefono = "1",
                Telefono = "2025550123"
            });

            Assert.Equal(TiposDocumento.Pasaporte, datos.TipoDocumento);
            Assert.Equal("A12345678", datos.Identificacion);
            Assert.Equal("1", datos.CodigoPaisTelefono);
        }

        [Fact]
        public void El_donante_anonimo_queda_sin_documento()
        {
            var datos = DonanteValidator.Validar(new DonanteCrearDto
            {
                Nombre = "Anónimo",
                TipoPersona = TiposPersonaDonante.Fisica
            });

            Assert.Equal(TiposDocumento.SinDocumento, datos.TipoDocumento);
            Assert.Null(datos.Identificacion);
        }

        // El donante que se registra desde el formulario de donación no pide documento.
        [Fact]
        public void El_donante_nuevo_del_formulario_de_donacion_queda_sin_documento()
        {
            var datos = DonanteValidator.Validar(new NuevoDonanteDto
            {
                Nombre = "Ana Rojas",
                TipoPersona = TiposPersonaDonante.Fisica
            });

            Assert.Equal(TiposDocumento.SinDocumento, datos.TipoDocumento);
            Assert.Null(datos.Identificacion);
        }

        // Un beneficiario es siempre una persona: agregar la cédula jurídica para
        // empresas no puede habérsela abierto a ellos.
        [Fact]
        public void El_validador_de_beneficiarios_sigue_rechazando_la_cedula_juridica()
        {
            var dto = new BeneficiarioCrearDto
            {
                PrimerNombre = "Ana",
                PrimerApellido = "Rojas",
                FechaNacimiento = DateTime.Today.AddYears(-30),
                TipoDocumento = TiposDocumento.CedulaJuridica,
                NumIdentidad = "3101123456"
            };

            Assert.Throws<ValidationException>(() => BeneficiarioValidator.Validar(dto));
        }
    }
}
