using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIGAC.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddModuloDonaciones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DonacionesEntregadas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ArticuloId = table.Column<int>(type: "int", nullable: false),
                    Cantidad = table.Column<int>(type: "int", nullable: false),
                    Fecha = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TipoDestinatario = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    BeneficiarioId = table.Column<int>(type: "int", nullable: true),
                    ComunidadDestinataria = table.Column<string>(type: "varchar(150)", unicode: false, maxLength: 150, nullable: true),
                    Observaciones = table.Column<string>(type: "varchar(500)", unicode: false, maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DonacionesEntregadas", x => x.Id);
                    table.CheckConstraint("CK_DonacionesEntregadas_Cantidad", "[Cantidad] > 0");
                    table.CheckConstraint("CK_DonacionesEntregadas_Destinatario", "([TipoDestinatario] = 'Beneficiario' AND [BeneficiarioId] IS NOT NULL AND [ComunidadDestinataria] IS NULL) OR ([TipoDestinatario] = 'Comunidad' AND [ComunidadDestinataria] IS NOT NULL AND [ComunidadDestinataria] <> '' AND [BeneficiarioId] IS NULL)");
                    table.CheckConstraint("CK_DonacionesEntregadas_TipoDestinatario", "[TipoDestinatario] IN ('Beneficiario', 'Comunidad')");
                    table.ForeignKey(
                        name: "FK_DonacionesEntregadas_Articulos_ArticuloId",
                        column: x => x.ArticuloId,
                        principalTable: "Articulos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DonacionesEntregadas_Beneficiarios_BeneficiarioId",
                        column: x => x.BeneficiarioId,
                        principalTable: "Beneficiarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Donantes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nombre = table.Column<string>(type: "varchar(150)", unicode: false, maxLength: 150, nullable: false),
                    TipoPersona = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    Telefono = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: true),
                    Correo = table.Column<string>(type: "varchar(150)", unicode: false, maxLength: 150, nullable: true),
                    Estado = table.Column<bool>(type: "bit", nullable: false),
                    FechaRegistro = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Donantes", x => x.Id);
                    table.CheckConstraint("CK_Donantes_TipoPersona", "[TipoPersona] IN ('Física', 'Jurídica')");
                });

            migrationBuilder.CreateTable(
                name: "DonacionesDinero",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DonanteId = table.Column<int>(type: "int", nullable: false),
                    Monto = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Fecha = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Observaciones = table.Column<string>(type: "varchar(500)", unicode: false, maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DonacionesDinero", x => x.Id);
                    table.CheckConstraint("CK_DonacionesDinero_Monto", "[Monto] > 0");
                    table.ForeignKey(
                        name: "FK_DonacionesDinero_Donantes_DonanteId",
                        column: x => x.DonanteId,
                        principalTable: "Donantes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DonacionesEspecie",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DonanteId = table.Column<int>(type: "int", nullable: false),
                    Fecha = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Observaciones = table.Column<string>(type: "varchar(500)", unicode: false, maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DonacionesEspecie", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DonacionesEspecie_Donantes_DonanteId",
                        column: x => x.DonanteId,
                        principalTable: "Donantes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DetallesDonacionEspecie",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DonacionEspecieId = table.Column<int>(type: "int", nullable: false),
                    NombreArticulo = table.Column<string>(type: "varchar(150)", unicode: false, maxLength: 150, nullable: false),
                    Cantidad = table.Column<int>(type: "int", nullable: false),
                    Categoria = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    UnidadMedida = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DetallesDonacionEspecie", x => x.Id);
                    table.CheckConstraint("CK_DetallesDonacionEspecie_Cantidad", "[Cantidad] > 0");
                    table.CheckConstraint("CK_DetallesDonacionEspecie_Categoria", "[Categoria] IN ('Alimento', 'Ropa', 'Calzado', 'Equipo')");
                    table.ForeignKey(
                        name: "FK_DetallesDonacionEspecie_DonacionesEspecie_DonacionEspecieId",
                        column: x => x.DonacionEspecieId,
                        principalTable: "DonacionesEspecie",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EntradasInventario_Donante",
                table: "EntradasInventario",
                column: "DonanteId");

            migrationBuilder.CreateIndex(
                name: "IX_DetallesDonacionEspecie_DonacionEspecie",
                table: "DetallesDonacionEspecie",
                column: "DonacionEspecieId");

            migrationBuilder.CreateIndex(
                name: "IX_DonacionesDinero_Donante_Fecha",
                table: "DonacionesDinero",
                columns: new[] { "DonanteId", "Fecha" });

            migrationBuilder.CreateIndex(
                name: "IX_DonacionesDinero_Fecha",
                table: "DonacionesDinero",
                column: "Fecha");

            migrationBuilder.CreateIndex(
                name: "IX_DonacionesEntregadas_Articulo_Fecha",
                table: "DonacionesEntregadas",
                columns: new[] { "ArticuloId", "Fecha" });

            migrationBuilder.CreateIndex(
                name: "IX_DonacionesEntregadas_Beneficiario",
                table: "DonacionesEntregadas",
                column: "BeneficiarioId");

            migrationBuilder.CreateIndex(
                name: "IX_DonacionesEntregadas_Fecha",
                table: "DonacionesEntregadas",
                column: "Fecha");

            migrationBuilder.CreateIndex(
                name: "IX_DonacionesEspecie_Donante_Fecha",
                table: "DonacionesEspecie",
                columns: new[] { "DonanteId", "Fecha" });

            migrationBuilder.CreateIndex(
                name: "IX_DonacionesEspecie_Fecha",
                table: "DonacionesEspecie",
                column: "Fecha");

            migrationBuilder.CreateIndex(
                name: "IX_Donantes_Estado",
                table: "Donantes",
                column: "Estado");

            migrationBuilder.CreateIndex(
                name: "IX_Donantes_Nombre",
                table: "Donantes",
                column: "Nombre");

            // Limpieza previa obligatoria: hasta esta migración, DonanteId era una
            // columna int NULL sin integridad referencial, y el desplegable de
            // RegistrarEntradaInventario la llenaba desde DonantesMock, un
            // diccionario de ids inventados (2, 3, 4...) que nunca correspondieron a
            // una fila real. Donantes se acaba de crear vacía, así que CUALQUIER
            // DonanteId no nulo apunta a un donante inexistente y hace fallar el
            // ALTER TABLE de abajo con el error 547.
            //
            // Se anulan las referencias en vez de borrar las entradas: la columna es
            // nullable, la entrada de inventario es el respaldo contable del
            // movimiento y su cantidad ya está sumada al StockActual del artículo.
            // Borrarlas descuadraría el stock y podría dejar salidas sin respaldo.
            // Lo que se pierde es solo la identidad del donante, que en estas filas
            // nunca fue un dato real.
            migrationBuilder.Sql(@"
                UPDATE EntradasInventario
                SET DonanteId = NULL
                WHERE DonanteId IS NOT NULL
                  AND NOT EXISTS (SELECT 1 FROM Donantes d WHERE d.Id = EntradasInventario.DonanteId);");

            migrationBuilder.AddForeignKey(
                name: "FK_EntradasInventario_Donantes_DonanteId",
                table: "EntradasInventario",
                column: "DonanteId",
                principalTable: "Donantes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_EntradasInventario_Donantes_DonanteId",
                table: "EntradasInventario");

            migrationBuilder.DropTable(
                name: "DetallesDonacionEspecie");

            migrationBuilder.DropTable(
                name: "DonacionesDinero");

            migrationBuilder.DropTable(
                name: "DonacionesEntregadas");

            migrationBuilder.DropTable(
                name: "DonacionesEspecie");

            migrationBuilder.DropTable(
                name: "Donantes");

            migrationBuilder.DropIndex(
                name: "IX_EntradasInventario_Donante",
                table: "EntradasInventario");
        }
    }
}
