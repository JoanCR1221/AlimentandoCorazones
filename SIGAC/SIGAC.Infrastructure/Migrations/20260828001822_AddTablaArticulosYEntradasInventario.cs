using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIGAC.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTablaArticulosYEntradasInventario : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Articulos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nombre = table.Column<string>(type: "varchar(150)", unicode: false, maxLength: 150, nullable: false),
                    Categoria = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    UnidadMedida = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    StockActual = table.Column<int>(type: "int", nullable: false),
                    StockMinimo = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Articulos", x => x.Id);
                    table.CheckConstraint("CK_Articulos_StockActual_NoNegativo", "[StockActual] >= 0");
                    table.CheckConstraint("CK_Articulos_StockMinimo_NoNegativo", "[StockMinimo] >= 0");
                });

            migrationBuilder.CreateTable(
                name: "EntradasInventario",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ArticuloId = table.Column<int>(type: "int", nullable: false),
                    Cantidad = table.Column<int>(type: "int", nullable: false),
                    Fecha = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Origen = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    DonanteId = table.Column<int>(type: "int", nullable: true),
                    GastoOperativoId = table.Column<int>(type: "int", nullable: true),
                    Observaciones = table.Column<string>(type: "varchar(500)", unicode: false, maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EntradasInventario", x => x.Id);
                    table.CheckConstraint("CK_EntradasInventario_Cantidad", "[Cantidad] > 0");
                    table.CheckConstraint("CK_EntradasInventario_Origen", "[Origen] IN ('Donacion', 'Compra')");
                    table.ForeignKey(
                        name: "FK_EntradasInventario_Articulos_ArticuloId",
                        column: x => x.ArticuloId,
                        principalTable: "Articulos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Articulos_Categoria",
                table: "Articulos",
                column: "Categoria");

            migrationBuilder.CreateIndex(
                name: "UX_Articulos_Nombre",
                table: "Articulos",
                column: "Nombre",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EntradasInventario_Articulo_Fecha",
                table: "EntradasInventario",
                columns: new[] { "ArticuloId", "Fecha" });

            migrationBuilder.CreateIndex(
                name: "IX_EntradasInventario_Fecha",
                table: "EntradasInventario",
                column: "Fecha");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EntradasInventario");

            migrationBuilder.DropTable(
                name: "Articulos");
        }
    }
}
