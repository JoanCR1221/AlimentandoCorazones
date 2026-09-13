using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIGAC.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddModuloProyectos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProyectosComunitarios",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nombre = table.Column<string>(type: "varchar(150)", unicode: false, maxLength: 150, nullable: false),
                    Descripcion = table.Column<string>(type: "varchar(500)", unicode: false, maxLength: 500, nullable: false),
                    FechaInicio = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FechaEstimadaFin = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FechaFinalizacionReal = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Estado = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    FechaRegistro = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProyectosComunitarios", x => x.Id);
                    table.CheckConstraint("CK_ProyectosComunitarios_Estado", "[Estado] IN ('Planificado', 'EnCurso', 'Finalizado', 'Cancelado')");
                    table.CheckConstraint("CK_ProyectosComunitarios_FechaFinalizacionReal", "([Estado] = 'Finalizado' AND [FechaFinalizacionReal] IS NOT NULL) OR ([Estado] <> 'Finalizado' AND [FechaFinalizacionReal] IS NULL)");
                    table.CheckConstraint("CK_ProyectosComunitarios_Fechas", "[FechaEstimadaFin] >= [FechaInicio]");
                });

            migrationBuilder.CreateTable(
                name: "ParticipantesProyecto",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProyectoId = table.Column<int>(type: "int", nullable: false),
                    EsBeneficiario = table.Column<bool>(type: "bit", nullable: false),
                    BeneficiarioId = table.Column<int>(type: "int", nullable: true),
                    NombreExterno = table.Column<string>(type: "varchar(150)", unicode: false, maxLength: 150, nullable: true),
                    ContactoExterno = table.Column<string>(type: "varchar(150)", unicode: false, maxLength: 150, nullable: true),
                    FechaRegistro = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParticipantesProyecto", x => x.Id);
                    table.CheckConstraint("CK_ParticipantesProyecto_Discriminador", "([EsBeneficiario] = 1 AND [BeneficiarioId] IS NOT NULL AND [NombreExterno] IS NULL AND [ContactoExterno] IS NULL) OR ([EsBeneficiario] = 0 AND [BeneficiarioId] IS NULL AND [NombreExterno] IS NOT NULL AND [NombreExterno] <> '')");
                    table.ForeignKey(
                        name: "FK_ParticipantesProyecto_Beneficiarios_BeneficiarioId",
                        column: x => x.BeneficiarioId,
                        principalTable: "Beneficiarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ParticipantesProyecto_ProyectosComunitarios_ProyectoId",
                        column: x => x.ProyectoId,
                        principalTable: "ProyectosComunitarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ParticipantesProyecto_Beneficiario",
                table: "ParticipantesProyecto",
                column: "BeneficiarioId");

            migrationBuilder.CreateIndex(
                name: "IX_ParticipantesProyecto_Proyecto_Beneficiario",
                table: "ParticipantesProyecto",
                columns: new[] { "ProyectoId", "BeneficiarioId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProyectosComunitarios_Estado",
                table: "ProyectosComunitarios",
                column: "Estado");

            migrationBuilder.CreateIndex(
                name: "IX_ProyectosComunitarios_FechaInicio",
                table: "ProyectosComunitarios",
                column: "FechaInicio");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ParticipantesProyecto");

            migrationBuilder.DropTable(
                name: "ProyectosComunitarios");
        }
    }
}
