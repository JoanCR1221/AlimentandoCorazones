namespace SIGAC.Domain.Entities
{
    // Un permiso que el administrador le quitó a un usuario concreto. Solo se
    // guardan las revocaciones, nunca las concesiones: los permisos efectivos son
    // los del rol menos estas filas (PermisosPorRol.CalcularEfectivos), así que un
    // usuario sin filas acá tiene exactamente lo que dicta su rol.
    //
    // UsuarioId es string porque apunta a AspNetUsers (clave de Identity), no a una
    // entidad del dominio. La FK se configura en SigacDbContext.
    public class PermisoRevocadoUsuario
    {
        public int Id { get; set; }
        public string UsuarioId { get; set; } = string.Empty;

        // Clave del catálogo Permisos ("Modulo.Accion"). Sin CHECK en la base a
        // propósito: ver el comentario en Permisos.
        public string Permiso { get; set; } = string.Empty;

        public DateTime FechaRegistro { get; set; } = DateTime.Now;
    }
}
