namespace SIGAC.Domain.Entities
{
    // Una fila de la bitácora: quién hizo qué, en qué módulo y cuándo. Es de solo
    // inserción: ni el repositorio ni la aplicación la editan o borran (el PBI 1949
    // exige que nadie pueda alterarla), y la migración revoca UPDATE y DELETE
    // sobre la tabla como última red.
    //
    // NombreUsuario y Rol se copian en el momento de la acción y no se leen por
    // join: si el usuario cambia de nombre o de rol después, la bitácora tiene
    // que seguir diciendo quién era y qué rol tenía cuando hizo la acción.
    public class BitacoraAccion
    {
        public int Id { get; set; }

        // Nullable: un intento de inicio de sesión con un correo inexistente, o un
        // acceso denegado sin sesión, no tienen usuario al que apuntar. Cuando hay
        // usuario apunta a AspNetUsers (clave de Identity, por eso string).
        public string? UsuarioId { get; set; }

        // Lo que se muestra en el listado. En un login fallido lleva el correo que
        // se intentó, para poder detectar intentos repetidos contra una cuenta.
        public string NombreUsuario { get; set; } = string.Empty;

        // Valor de RolesSistema, o NULL cuando no hay sesión (login fallido).
        public string? Rol { get; set; }

        // Valor de AccionesBitacora.
        public string Accion { get; set; } = string.Empty;

        // Valor de ModulosSistema.
        public string Modulo { get; set; } = string.Empty;

        // Texto libre con lo específico: "Beneficiario #12 (María Pérez)", la ruta
        // a la que se intentó acceder, el rol anterior y el nuevo, etc.
        public string? Detalle { get; set; }

        public DateTime Fecha { get; set; } = DateTime.Now;
    }
}
