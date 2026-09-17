namespace SIGAC.Domain
{
    // Qué permisos trae cada rol por defecto y cómo se combinan con los que el
    // administrador revocó a un usuario en particular. Vive en el dominio y no en
    // Identity para que el claims factory (al iniciar sesión), el panel de permisos
    // (para dibujar los switches) y las pruebas usen exactamente la misma regla.
    public static class PermisosPorRol
    {
        // Colaborador: todos los módulos operativos, nada de Seguridad.
        private static readonly IReadOnlyList<string> DeColaborador = Permisos.Definiciones
            .Where(p => p.Modulo != ModulosSistema.Seguridad)
            .Select(p => p.Clave)
            .ToList();

        // Asistente: solo registra asistencia y entradas de inventario, sin ninguna
        // pantalla de consulta. Decisión del cliente (sprint 6d).
        private static readonly IReadOnlyList<string> DeAsistente = new[]
        {
            Permisos.Asistencia.Registrar,
            Permisos.Inventario.RegistrarEntrada
        };

        // Permisos que un rol tiene antes de cualquier revocación individual.
        public static IReadOnlyList<string> Obtener(string? rol) => rol switch
        {
            RolesSistema.Administrador => Permisos.Todos,
            RolesSistema.Colaborador => DeColaborador,
            RolesSistema.Asistente => DeAsistente,
            _ => Array.Empty<string>()
        };

        // Los permisos de un rol se pueden recortar por usuario, salvo los del
        // Administrador: si se pudieran, la regla "siempre queda al menos un
        // administrador activo" no garantizaría nada, porque ese administrador
        // podría existir sin poder gestionar usuarios.
        public static bool AdmiteRevocaciones(string? rol) =>
            rol is not null && rol != RolesSistema.Administrador && RolesSistema.EsValido(rol);

        // Permisos efectivos = los del rol menos los revocados. Las revocaciones que
        // no pertenecen al rol se ignoran: quedaron de un rol anterior o son claves
        // que ya no existen, y en ningún caso agregan nada.
        public static IReadOnlyList<string> CalcularEfectivos(string? rol, IEnumerable<string>? revocados)
        {
            var delRol = Obtener(rol);

            if (!AdmiteRevocaciones(rol) || revocados is null)
                return delRol;

            var excluidos = new HashSet<string>(revocados, StringComparer.Ordinal);

            return delRol.Where(p => !excluidos.Contains(p)).ToList();
        }

        // Solo se puede revocar lo que el rol incluye: revocar un permiso que el rol
        // no tiene no cambia nada y confundiría al leer la tabla. Lo usa el
        // servicio para rechazar la petición completa, no para filtrarla en
        // silencio.
        public static IReadOnlyList<string> RevocacionesInvalidas(string? rol, IEnumerable<string> revocados)
        {
            var delRol = new HashSet<string>(Obtener(rol), StringComparer.Ordinal);

            return revocados
                .Where(p => !delRol.Contains(p))
                .Distinct(StringComparer.Ordinal)
                .ToList();
        }
    }
}
