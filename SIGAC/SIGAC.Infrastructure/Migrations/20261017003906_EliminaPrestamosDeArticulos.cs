using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIGAC.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EliminaPrestamosDeArticulos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // El préstamo de artículos sale del sistema. Antes de quitar el tipo
            // 'Prestamo' del CHECK hay que sacar sus salidas: primero se devuelve al
            // stock lo que descontaron (un préstamo no consumía la mercadería) y
            // después se borran. Sin esto el CHECK nuevo no se podría crear.
            migrationBuilder.Sql(@"
UPDATE a
   SET a.StockActual = a.StockActual + s.Cantidad
  FROM Articulos a
  JOIN (SELECT ArticuloId, SUM(Cantidad) AS Cantidad
          FROM SalidasInventario
         WHERE TipoSalida = 'Prestamo'
         GROUP BY ArticuloId) s ON s.ArticuloId = a.Id;

DELETE FROM SalidasInventario WHERE TipoSalida = 'Prestamo';");

            // Las revocaciones de los dos permisos eliminados ya no significan nada.
            // La bitácora no se toca: es el registro histórico de lo que pasó.
            migrationBuilder.Sql(@"
DELETE FROM PermisosRevocados
 WHERE Permiso IN ('Inventario.SolicitarPrestamo', 'Inventario.GestionarSolicitudes');");

            migrationBuilder.DropForeignKey(
                name: "FK_SalidasInventario_SolicitudesPrestamo_SolicitudPrestamoId",
                table: "SalidasInventario");

            migrationBuilder.DropTable(
                name: "SolicitudesPrestamo");

            migrationBuilder.DropIndex(
                name: "UX_SalidasInventario_SolicitudPrestamo",
                table: "SalidasInventario");

            migrationBuilder.DropCheckConstraint(
                name: "CK_SalidasInventario_TipoSalida",
                table: "SalidasInventario");

            migrationBuilder.DropColumn(
                name: "SolicitudPrestamoId",
                table: "SalidasInventario");

            migrationBuilder.AddCheckConstraint(
                name: "CK_SalidasInventario_TipoSalida",
                table: "SalidasInventario",
                sql: "[TipoSalida] IN ('Donacion')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_SalidasInventario_TipoSalida",
                table: "SalidasInventario");

            migrationBuilder.AddColumn<int>(
                name: "SolicitudPrestamoId",
                table: "SalidasInventario",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SolicitudesPrestamo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ArticuloId = table.Column<int>(type: "int", nullable: false),
                    Actividad = table.Column<string>(type: "varchar(150)", unicode: false, maxLength: 150, nullable: false),
                    Cantidad = table.Column<int>(type: "int", nullable: false),
                    Estado = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    Fecha = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FechaResolucion = table.Column<DateTime>(type: "datetime2", nullable: true),
                    MotivoRechazo = table.Column<string>(type: "varchar(500)", unicode: false, maxLength: 500, nullable: true),
                    Solicitante = table.Column<string>(type: "varchar(150)", unicode: false, maxLength: 150, nullable: false)
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

            migrationBuilder.CreateIndex(
                name: "UX_SalidasInventario_SolicitudPrestamo",
                table: "SalidasInventario",
                column: "SolicitudPrestamoId",
                unique: true,
                filter: "[SolicitudPrestamoId] IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_SalidasInventario_TipoSalida",
                table: "SalidasInventario",
                sql: "[TipoSalida] IN ('Donacion', 'Prestamo')");

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

            migrationBuilder.AddForeignKey(
                name: "FK_SalidasInventario_SolicitudesPrestamo_SolicitudPrestamoId",
                table: "SalidasInventario",
                column: "SolicitudPrestamoId",
                principalTable: "SolicitudesPrestamo",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
