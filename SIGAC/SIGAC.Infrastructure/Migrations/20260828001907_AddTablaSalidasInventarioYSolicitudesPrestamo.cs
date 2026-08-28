using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIGAC.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTablaSalidasInventarioYSolicitudesPrestamo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SolicitudesPrestamo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ArticuloId = table.Column<int>(type: "int", nullable: false),
                    Cantidad = table.Column<int>(type: "int", nullable: false),
                    Fecha = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Actividad = table.Column<string>(type: "varchar(150)", unicode: false, maxLength: 150, nullable: false),
                    Solicitante = table.Column<string>(type: "varchar(150)", unicode: false, maxLength: 150, nullable: false),
                    Estado = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    MotivoRechazo = table.Column<string>(type: "varchar(500)", unicode: false, maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SolicitudesPrestamo", x => x.Id);
                    table.CheckConstraint("CK_SolicitudesPrestamo_Cantidad", "[Cantidad] > 0");
                    table.CheckConstraint("CK_SolicitudesPrestamo_Estado", "[Estado] IN ('Pendiente', 'Aprobada', 'Rechazada')");
                    table.ForeignKey(
                        name: "FK_SolicitudesPrestamo_Articulos_ArticuloId",
                        column: x => x.ArticuloId,
                        principalTable: "Articulos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SalidasInventario",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ArticuloId = table.Column<int>(type: "int", nullable: false),
                    Cantidad = table.Column<int>(type: "int", nullable: false),
                    Fecha = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TipoSalida = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    ComunidadDestinataria = table.Column<string>(type: "varchar(150)", unicode: false, maxLength: 150, nullable: true),
                    SolicitudPrestamoId = table.Column<int>(type: "int", nullable: true),
                    Observaciones = table.Column<string>(type: "varchar(500)", unicode: false, maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalidasInventario", x => x.Id);
                    table.CheckConstraint("CK_SalidasInventario_Cantidad", "[Cantidad] > 0");
                    table.CheckConstraint("CK_SalidasInventario_TipoSalida", "[TipoSalida] IN ('Donacion', 'Prestamo')");
                    table.ForeignKey(
                        name: "FK_SalidasInventario_Articulos_ArticuloId",
                        column: x => x.ArticuloId,
                        principalTable: "Articulos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SalidasInventario_SolicitudesPrestamo_SolicitudPrestamoId",
                        column: x => x.SolicitudPrestamoId,
                        principalTable: "SolicitudesPrestamo",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SalidasInventario_Articulo_Fecha",
                table: "SalidasInventario",
                columns: new[] { "ArticuloId", "Fecha" });

            migrationBuilder.CreateIndex(
                name: "IX_SalidasInventario_Fecha",
                table: "SalidasInventario",
                column: "Fecha");

            migrationBuilder.CreateIndex(
                name: "UX_SalidasInventario_SolicitudPrestamo",
                table: "SalidasInventario",
                column: "SolicitudPrestamoId",
                unique: true,
                filter: "[SolicitudPrestamoId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SolicitudesPrestamo_ArticuloId",
                table: "SolicitudesPrestamo",
                column: "ArticuloId");

            migrationBuilder.CreateIndex(
                name: "IX_SolicitudesPrestamo_Estado",
                table: "SolicitudesPrestamo",
                column: "Estado");

            migrationBuilder.CreateIndex(
                name: "IX_SolicitudesPrestamo_Fecha",
                table: "SolicitudesPrestamo",
                column: "Fecha");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SalidasInventario");

            migrationBuilder.DropTable(
                name: "SolicitudesPrestamo");
        }
    }
}
