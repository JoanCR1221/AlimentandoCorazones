using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIGAC.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTablaAsistenciaComedor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AsistenciasComedor",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BeneficiarioId = table.Column<int>(type: "int", nullable: false),
                    Fecha = table.Column<DateTime>(type: "date", nullable: false),
                    TiempoComida = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AsistenciasComedor", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AsistenciasComedor_Beneficiarios_BeneficiarioId",
                        column: x => x.BeneficiarioId,
                        principalTable: "Beneficiarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AsistenciasComedor_Fecha",
                table: "AsistenciasComedor",
                column: "Fecha");

            migrationBuilder.CreateIndex(
                name: "IX_AsistenciasComedor_TiempoComida",
                table: "AsistenciasComedor",
                column: "TiempoComida");

            migrationBuilder.CreateIndex(
                name: "UX_AsistenciasComedor_Beneficiario_Fecha_TiempoComida",
                table: "AsistenciasComedor",
                columns: new[] { "BeneficiarioId", "Fecha", "TiempoComida" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AsistenciasComedor");
        }
    }
}
