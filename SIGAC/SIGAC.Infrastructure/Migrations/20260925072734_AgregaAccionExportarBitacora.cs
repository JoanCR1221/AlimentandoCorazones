using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIGAC.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AgregaAccionExportarBitacora : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Bitacora_Accion",
                table: "Bitacora");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Bitacora_Accion",
                table: "Bitacora",
                sql: "[Accion] IN ('IniciarSesion', 'IniciarSesionFallido', 'CerrarSesion', 'AccesoDenegado', 'Registrar', 'Editar', 'Eliminar', 'Anular', 'Activar', 'Desactivar', 'Aprobar', 'Rechazar', 'Finalizar', 'Entregar', 'Exportar', 'CambiarRol', 'CambiarPermisos', 'CambiarPassword', 'RestablecerPassword')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Bitacora_Accion",
                table: "Bitacora");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Bitacora_Accion",
                table: "Bitacora",
                sql: "[Accion] IN ('IniciarSesion', 'IniciarSesionFallido', 'CerrarSesion', 'AccesoDenegado', 'Registrar', 'Editar', 'Eliminar', 'Anular', 'Activar', 'Desactivar', 'Aprobar', 'Rechazar', 'Finalizar', 'Entregar', 'CambiarRol', 'CambiarPermisos', 'CambiarPassword', 'RestablecerPassword')");
        }
    }
}
