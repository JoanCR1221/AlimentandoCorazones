namespace SIGAC.Domain
{
    // Roles del sistema: lista cerrada, mismo criterio que TiposDocumento. Fuente
    // única para el seed de ASP.NET Identity, el servicio de usuarios, los
    // desplegables de la UI y PermisosPorRol.
    //
    // Los nombres se guardan tal cual en AspNetRoles y viajan como claim de rol en
    // la cookie de sesión, así que cambiar una constante implica una migración que
    // renombre el rol ya guardado.
    public static class RolesSistema
    {
        // Acceso total. Es el único rol cuyos permisos no se pueden recortar por
        // usuario (ver PermisosPorRol.CalcularEfectivos).
        public const string Administrador = "Administrador";

        // Módulos operativos: beneficiarios, asistencia, inventario, donaciones y
        // gastos. Sin acceso a la gestión de usuarios ni a la bitácora.
        public const string Colaborador = "Colaborador";

        // Solo registra: asistencia diaria y entradas de inventario. No consulta
        // ninguna pantalla, por decisión del cliente.
        public const string Asistente = "Asistente";

        public static readonly IReadOnlyList<string> Todos = new[]
        {
            Administrador,
            Colaborador,
            Asistente
        };

        // Todo usuario nuevo nace con el rol de menos privilegios. El rol se eleva
        // después, desde el listado de usuarios, y solo por un administrador.
        public const string PorDefecto = Asistente;

        public static bool EsValido(string? rol) =>
            rol is not null && Todos.Contains(rol);
    }
}
