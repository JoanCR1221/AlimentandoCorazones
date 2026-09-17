namespace SIGAC.Domain
{
    // Un permiso del catálogo: la clave que viaja como claim y se guarda en
    // PermisosRevocados, el módulo que lo agrupa en el panel y el texto que ve el
    // administrador junto al switch.
    public sealed record Permiso(string Clave, string Modulo, string Descripcion);

    // Catálogo de permisos del sistema: una clave por pantalla o acción que se
    // puede habilitar o quitar a un usuario. Fuente única para las policies de
    // autorización (Program.cs registra una por clave), los [Authorize(Policy)] de
    // las páginas, los AuthorizeView de menú y botones, PermisosPorRol y el panel
    // de gestión de permisos.
    //
    // Las claves son "Modulo.Accion" y se guardan tal cual, así que renombrar una
    // constante implica una migración que actualice PermisosRevocados.
    //
    // A propósito NO hay CHECK sobre la columna Permiso de PermisosRevocados: cada
    // pantalla nueva agrega una clave acá, y atarla a una restricción obligaría a
    // una migración por pantalla. La validez la garantiza el servicio contra
    // Permisos.Todos (ver decisión en sprint6d-lista-tareas.md).
    public static class Permisos
    {
        public static class Beneficiarios
        {
            public const string Ver = "Beneficiarios.Ver";
            public const string Registrar = "Beneficiarios.Registrar";
            public const string Editar = "Beneficiarios.Editar";
            public const string CambiarEstado = "Beneficiarios.CambiarEstado";
        }

        public static class Asistencia
        {
            public const string Ver = "Asistencia.Ver";
            public const string Registrar = "Asistencia.Registrar";
        }

        public static class Inventario
        {
            public const string VerExistencias = "Inventario.VerExistencias";
            public const string RegistrarEntrada = "Inventario.RegistrarEntrada";
            public const string EditarArticulo = "Inventario.EditarArticulo";
            public const string SolicitarPrestamo = "Inventario.SolicitarPrestamo";
            public const string GestionarSolicitudes = "Inventario.GestionarSolicitudes";
            public const string VerMovimientos = "Inventario.VerMovimientos";
        }

        public static class Donaciones
        {
            public const string VerDonantes = "Donaciones.VerDonantes";
            public const string RegistrarDonante = "Donaciones.RegistrarDonante";
            public const string EditarDonante = "Donaciones.EditarDonante";
            public const string RegistrarDinero = "Donaciones.RegistrarDinero";
            public const string RegistrarEspecie = "Donaciones.RegistrarEspecie";
            public const string VerHistorial = "Donaciones.VerHistorial";
            public const string RegistrarEntrega = "Donaciones.RegistrarEntrega";
            public const string VerEntregas = "Donaciones.VerEntregas";
        }

        public static class Gastos
        {
            public const string Ver = "Gastos.Ver";
            public const string Registrar = "Gastos.Registrar";
            public const string Editar = "Gastos.Editar";
            public const string Anular = "Gastos.Anular";
        }

        public static class Proyectos
        {
            public const string Ver = "Proyectos.Ver";
            public const string Registrar = "Proyectos.Registrar";
            public const string Editar = "Proyectos.Editar";
            public const string Finalizar = "Proyectos.Finalizar";
            public const string RegistrarParticipante = "Proyectos.RegistrarParticipante";
        }

        public static class Seguridad
        {
            public const string GestionarUsuarios = "Seguridad.GestionarUsuarios";
            public const string VerBitacora = "Seguridad.VerBitacora";
        }

        // Orden de aparición en el panel: por módulo, y dentro de cada módulo
        // primero consultar y después las acciones.
        public static readonly IReadOnlyList<Permiso> Definiciones = new[]
        {
            new Permiso(Beneficiarios.Ver, ModulosSistema.Beneficiarios, "Ver el listado de beneficiarios"),
            new Permiso(Beneficiarios.Registrar, ModulosSistema.Beneficiarios, "Registrar beneficiarios"),
            new Permiso(Beneficiarios.Editar, ModulosSistema.Beneficiarios, "Editar beneficiarios"),
            new Permiso(Beneficiarios.CambiarEstado, ModulosSistema.Beneficiarios, "Activar o desactivar beneficiarios"),

            new Permiso(Asistencia.Ver, ModulosSistema.Asistencia, "Ver el historial de asistencia"),
            new Permiso(Asistencia.Registrar, ModulosSistema.Asistencia, "Registrar asistencia al comedor"),

            new Permiso(Inventario.VerExistencias, ModulosSistema.Inventario, "Ver existencias"),
            new Permiso(Inventario.RegistrarEntrada, ModulosSistema.Inventario, "Registrar entradas de inventario"),
            new Permiso(Inventario.EditarArticulo, ModulosSistema.Inventario, "Editar y eliminar artículos"),
            new Permiso(Inventario.SolicitarPrestamo, ModulosSistema.Inventario, "Solicitar préstamos de artículos"),
            new Permiso(Inventario.GestionarSolicitudes, ModulosSistema.Inventario, "Aprobar o rechazar solicitudes de préstamo"),
            new Permiso(Inventario.VerMovimientos, ModulosSistema.Inventario, "Ver el historial de movimientos"),

            new Permiso(Donaciones.VerDonantes, ModulosSistema.Donaciones, "Ver el listado de donantes"),
            new Permiso(Donaciones.RegistrarDonante, ModulosSistema.Donaciones, "Registrar donantes"),
            new Permiso(Donaciones.EditarDonante, ModulosSistema.Donaciones, "Editar donantes"),
            new Permiso(Donaciones.RegistrarDinero, ModulosSistema.Donaciones, "Registrar donaciones en dinero"),
            new Permiso(Donaciones.RegistrarEspecie, ModulosSistema.Donaciones, "Registrar donaciones en especie"),
            new Permiso(Donaciones.VerHistorial, ModulosSistema.Donaciones, "Ver el historial de donaciones"),
            new Permiso(Donaciones.RegistrarEntrega, ModulosSistema.Donaciones, "Entregar donaciones a beneficiarios o comunidades"),
            new Permiso(Donaciones.VerEntregas, ModulosSistema.Donaciones, "Ver el historial de entregas"),

            new Permiso(Gastos.Ver, ModulosSistema.Gastos, "Ver el listado de gastos operativos"),
            new Permiso(Gastos.Registrar, ModulosSistema.Gastos, "Registrar gastos operativos"),
            new Permiso(Gastos.Editar, ModulosSistema.Gastos, "Editar gastos operativos"),
            new Permiso(Gastos.Anular, ModulosSistema.Gastos, "Anular gastos operativos"),

            new Permiso(Proyectos.Ver, ModulosSistema.Proyectos, "Ver el listado de proyectos comunitarios"),
            new Permiso(Proyectos.Registrar, ModulosSistema.Proyectos, "Registrar proyectos"),
            new Permiso(Proyectos.Editar, ModulosSistema.Proyectos, "Editar proyectos"),
            new Permiso(Proyectos.Finalizar, ModulosSistema.Proyectos, "Finalizar proyectos"),
            new Permiso(Proyectos.RegistrarParticipante, ModulosSistema.Proyectos, "Registrar participantes en proyectos"),

            new Permiso(Seguridad.GestionarUsuarios, ModulosSistema.Seguridad, "Gestionar usuarios, roles y permisos"),
            new Permiso(Seguridad.VerBitacora, ModulosSistema.Seguridad, "Consultar la bitácora de acciones")
        };

        public static readonly IReadOnlyList<string> Todos =
            Definiciones.Select(p => p.Clave).ToList();

        public static bool EsValido(string? clave) =>
            clave is not null && Todos.Contains(clave);

        public static IReadOnlyList<string> DelModulo(string modulo) =>
            Definiciones.Where(p => p.Modulo == modulo).Select(p => p.Clave).ToList();
    }
}
