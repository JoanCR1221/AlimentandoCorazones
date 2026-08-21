using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIGAC.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCheckTiempoComida : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "CK_AsistenciasComedor_TiempoComida",
                table: "AsistenciasComedor",
                sql: "[TiempoComida] IN ('Desayuno', 'Almuerzo', 'Cena')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_AsistenciasComedor_TiempoComida",
                table: "AsistenciasComedor");
        }
    }
}
