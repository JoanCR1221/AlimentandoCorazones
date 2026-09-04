using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIGAC.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCodigoYUbicacionArticulo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Codigo",
                table: "Articulos",
                type: "varchar(50)",
                unicode: false,
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Ubicacion",
                table: "Articulos",
                type: "varchar(100)",
                unicode: false,
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "UX_Articulos_Codigo",
                table: "Articulos",
                column: "Codigo",
                unique: true,
                filter: "[Codigo] IS NOT NULL AND [Codigo] <> ''");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_Articulos_Codigo",
                table: "Articulos");

            migrationBuilder.DropColumn(
                name: "Codigo",
                table: "Articulos");

            migrationBuilder.DropColumn(
                name: "Ubicacion",
                table: "Articulos");
        }
    }
}
