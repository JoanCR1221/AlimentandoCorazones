using SIGAC.Application.DTOs.Seguridad;
using SIGAC.Application.Exceptions;
using SIGAC.Application.Validators;
using SIGAC.Domain;

namespace SIGAC.Tests.Domain
{
    public class ReglasUsuarioTests
    {
        [Theory]
        [InlineData("Clave.Segura1", true)]
        [InlineData("Admin.SIGAC2026!", true)]
        [InlineData("corta1!", false)]          // menos de 8
        [InlineData("sinmayuscula1!", false)]
        [InlineData("SINMINUSCULA1!", false)]
        [InlineData("SinNumero!!", false)]
        [InlineData("SinSimbolo12", false)]
        [InlineData("", false)]
        [InlineData(null, false)]
        public void Password_cumple_reglas(string? password, bool esperado)
        {
            Assert.Equal(esperado, ReglasUsuario.PasswordCumpleReglas(password));
        }

        [Theory]
        [InlineData("ana@ejemplo.com", true)]
        [InlineData("ana.perez@asociacion.local", true)]
        [InlineData("ana@ejemplo", false)]      // sin punto en el dominio
        [InlineData("@ejemplo.com", false)]
        [InlineData("ana@", false)]
        [InlineData("ana ejemplo@x.com", false)]
        [InlineData("ana@@x.com", false)]
        [InlineData("", false)]
        [InlineData(null, false)]
        public void Correo_tiene_formato(string? correo, bool esperado)
        {
            Assert.Equal(esperado, ReglasUsuario.TieneFormatoCorreo(correo));
        }
    }

    public class UsuarioValidatorTests
    {
        private static UsuarioCrearDto DtoValido() => new()
        {
            Nombre = "  ana   maría  ",
            Correo = " Ana.Maria@Ejemplo.COM ",
            Password = "Clave.Segura1",
            ConfirmarPassword = "Clave.Segura1"
        };

        [Fact]
        public void Normaliza_nombre_y_correo()
        {
            var datos = UsuarioValidator.Validar(DtoValido());

            Assert.Equal("Ana maría", datos.Nombre);
            Assert.Equal("ana.maria@ejemplo.com", datos.Correo);
            Assert.Equal("Clave.Segura1", datos.Password);
        }

        [Fact]
        public void Rechaza_contrasenas_que_no_coinciden()
        {
            var dto = DtoValido();
            dto.ConfirmarPassword = "Otra.Clave1";

            var ex = Assert.Throws<ValidationException>(() => UsuarioValidator.Validar(dto));
            Assert.Contains("no coinciden", ex.Message);
        }

        [Fact]
        public void Rechaza_contrasena_debil_con_la_descripcion_de_las_reglas()
        {
            var dto = DtoValido();
            dto.Password = dto.ConfirmarPassword = "debil";

            var ex = Assert.Throws<ValidationException>(() => UsuarioValidator.Validar(dto));
            Assert.Contains(ReglasUsuario.DescripcionReglasPassword, ex.Message);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("A")]
        public void Rechaza_nombre_vacio_o_demasiado_corto(string nombre)
        {
            var dto = DtoValido();
            dto.Nombre = nombre;

            Assert.Throws<ValidationException>(() => UsuarioValidator.Validar(dto));
        }

        [Fact]
        public void Rechaza_nombre_demasiado_largo()
        {
            var dto = DtoValido();
            dto.Nombre = new string('a', ReglasUsuario.LongitudMaximaNombre + 1);

            Assert.Throws<ValidationException>(() => UsuarioValidator.Validar(dto));
        }

        [Theory]
        [InlineData("")]
        [InlineData("sin-arroba")]
        [InlineData("ana@sinpunto")]
        public void Rechaza_correo_invalido(string correo)
        {
            var dto = DtoValido();
            dto.Correo = correo;

            Assert.Throws<ValidationException>(() => UsuarioValidator.Validar(dto));
        }
    }
}
