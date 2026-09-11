using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIGAC.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTablaGastosOperativos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GastosOperativos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Categoria = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: false),
                    Monto = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Fecha = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Descripcion = table.Column<string>(type: "varchar(500)", unicode: false, maxLength: 500, nullable: false),
                    Responsable = table.Column<string>(type: "varchar(150)", unicode: false, maxLength: 150, nullable: false),
                    Estado = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    FechaRegistro = table.Column<DateTime>(type: "datetime2", nullable: false),
                    MotivoAnulacion = table.Column<string>(type: "varchar(500)", unicode: false, maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GastosOperativos", x => x.Id);
                    table.CheckConstraint("CK_GastosOperativos_Categoria", "[Categoria] IN ('ServiciosBasicos', 'Transporte', 'CompraInsumos', 'Salarios', 'Viaticos')");
                    table.CheckConstraint("CK_GastosOperativos_Estado", "[Estado] IN ('Activo', 'Anulado')");
                    table.CheckConstraint("CK_GastosOperativos_Monto", "[Monto] > 0");
                    table.CheckConstraint("CK_GastosOperativos_MotivoAnulacion", "([Estado] = 'Anulado' AND [MotivoAnulacion] IS NOT NULL) OR ([Estado] <> 'Anulado' AND [MotivoAnulacion] IS NULL)");
                });

            migrationBuilder.CreateIndex(
                name: "IX_EntradasInventario_GastoOperativo",
                table: "EntradasInventario",
                column: "GastoOperativoId");

            migrationBuilder.CreateIndex(
                name: "IX_GastosOperativos_Fecha_Categoria",
                table: "GastosOperativos",
                columns: new[] { "Fecha", "Categoria" });

            // Misma limpieza previa que en AddModuloDonaciones y por la misma causa:
            // hasta esta migración GastoOperativoId era una columna int NULL sin
            // integridad referencial, y el desplegable de RegistrarEntradaInventario
            // la llenaba desde GastosOperativosMock, un diccionario de ids inventados
            // (1, 2, 3). GastosOperativos se acaba de crear vacía, así que cualquier
            // GastoOperativoId no nulo apunta a un gasto inexistente y haría fallar el
            // ALTER TABLE de abajo con el error 547.
            //
            // Se anulan las referencias en vez de borrar las entradas, por lo mismo:
            // la columna es nullable y la entrada ya tiene su cantidad sumada al
            // StockActual del artículo.
            migrationBuilder.Sql(@"
                UPDATE EntradasInventario
                SET GastoOperativoId = NULL
                WHERE GastoOperativoId IS NOT NULL
                  AND NOT EXISTS (SELECT 1 FROM GastosOperativos g WHERE g.Id = EntradasInventario.GastoOperativoId);");

            migrationBuilder.AddForeignKey(
                name: "FK_EntradasInventario_GastosOperativos_GastoOperativoId",
                table: "EntradasInventario",
                column: "GastoOperativoId",
                principalTable: "GastosOperativos",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_EntradasInventario_GastosOperativos_GastoOperativoId",
                table: "EntradasInventario");

            migrationBuilder.DropTable(
                name: "GastosOperativos");

            migrationBuilder.DropIndex(
                name: "IX_EntradasInventario_GastoOperativo",
                table: "EntradasInventario");
        }
    }
}
