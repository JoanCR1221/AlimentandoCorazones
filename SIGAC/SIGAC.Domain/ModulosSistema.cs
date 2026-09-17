namespace SIGAC.Domain
{
    // Módulos del sistema tal como los ve el usuario: agrupan los permisos en el
    // panel de gestión (Permisos.Definiciones) y etiquetan cada fila de la bitácora
    // (columna Modulo, respaldada por CK_Bitacora_Modulo). Lista cerrada, mismo
    // criterio que CategoriasGastoOperativo: agregar un módulo acá exige una
    // migración que reescriba ese CHECK.
    public static class ModulosSistema
    {
        public const string Beneficiarios = "Beneficiarios";
        public const string Asistencia = "Asistencia";
        public const string Inventario = "Inventario";
        public const string Donaciones = "Donaciones";
        public const string Gastos = "Gastos";
        public const string Proyectos = "Proyectos";
        public const string Seguridad = "Seguridad";

        public static readonly IReadOnlyList<string> Todos = new[]
        {
            Beneficiarios,
            Asistencia,
            Inventario,
            Donaciones,
            Gastos,
            Proyectos,
            Seguridad
        };

        public static bool EsValido(string? modulo) =>
            modulo is not null && Todos.Contains(modulo);
    }
}
