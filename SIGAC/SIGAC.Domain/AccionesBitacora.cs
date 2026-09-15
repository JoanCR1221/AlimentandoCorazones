namespace SIGAC.Domain
{
    // Acciones que puede registrar la bitácora: lista cerrada, respaldada por
    // CK_Bitacora_Accion. Son genéricas a propósito ("Registrar", "Anular") y el
    // qué exacto va en Modulo y Detalle, para que agregar una pantalla nueva no
    // exija una migración salvo que aparezca un verbo realmente nuevo.
    public static class AccionesBitacora
    {
        // Sesión
        public const string IniciarSesion = "IniciarSesion";
        public const string IniciarSesionFallido = "IniciarSesionFallido";
        public const string CerrarSesion = "CerrarSesion";
        public const string AccesoDenegado = "AccesoDenegado";

        // Operaciones de los módulos
        public const string Registrar = "Registrar";
        public const string Editar = "Editar";
        public const string Eliminar = "Eliminar";
        public const string Anular = "Anular";
        public const string Activar = "Activar";
        public const string Desactivar = "Desactivar";
        public const string Aprobar = "Aprobar";
        public const string Rechazar = "Rechazar";
        public const string Finalizar = "Finalizar";
        public const string Entregar = "Entregar";

        // Gestión de usuarios
        public const string CambiarRol = "CambiarRol";
        public const string CambiarPermisos = "CambiarPermisos";
        public const string CambiarPassword = "CambiarPassword";
        public const string RestablecerPassword = "RestablecerPassword";

        public static readonly IReadOnlyList<string> Todas = new[]
        {
            IniciarSesion,
            IniciarSesionFallido,
            CerrarSesion,
            AccesoDenegado,
            Registrar,
            Editar,
            Eliminar,
            Anular,
            Activar,
            Desactivar,
            Aprobar,
            Rechazar,
            Finalizar,
            Entregar,
            CambiarRol,
            CambiarPermisos,
            CambiarPassword,
            RestablecerPassword
        };

        public static bool EsValida(string? accion) =>
            accion is not null && Todas.Contains(accion);
    }
}
