using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIGAC.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AgregaModuloReportes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Bitacora_Modulo",
                table: "Bitacora");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Bitacora_Modulo",
                table: "Bitacora",
                sql: "[Modulo] IN ('Beneficiarios', 'Asistencia', 'Inventario', 'Donaciones', 'Gastos', 'Proyectos', 'Seguridad', 'Reportes')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Bitacora_Modulo",
                table: "Bitacora");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Bitacora_Modulo",
                table: "Bitacora",
                sql: "[Modulo] IN ('Beneficiarios', 'Asistencia', 'Inventario', 'Donaciones', 'Gastos', 'Proyectos', 'Seguridad')");
        }
    }
}
