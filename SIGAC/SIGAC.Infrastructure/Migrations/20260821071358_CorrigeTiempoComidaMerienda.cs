using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIGAC.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CorrigeTiempoComidaMerienda : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Los tiempos de comida del comedor son Desayuno, Almuerzo y Merienda.
            // "Cena" fue un error de la implementacion inicial. El CHECK se recrea
            // con el mismo nombre porque el dominio cerrado sigue siendo de 3 valores.
            migrationBuilder.DropCheckConstraint(
                name: "CK_AsistenciasComedor_TiempoComida",
                table: "AsistenciasComedor");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AsistenciasComedor_TiempoComida",
                table: "AsistenciasComedor",
                sql: "[TiempoComida] IN ('Desayuno', 'Almuerzo', 'Merienda')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_AsistenciasComedor_TiempoComida",
                table: "AsistenciasComedor");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AsistenciasComedor_TiempoComida",
                table: "AsistenciasComedor",
                sql: "[TiempoComida] IN ('Desayuno', 'Almuerzo', 'Cena')");
        }
    }
}
