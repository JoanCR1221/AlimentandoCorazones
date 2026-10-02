using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace SIGAC.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTablaAlquileres : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AlquileresEspacio",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ArrendatarioId = table.Column<int>(type: "int", nullable: false),
                    Fecha = table.Column<DateTime>(type: "date", nullable: false),
                    HoraInicio = table.Column<TimeSpan>(type: "time", nullable: false),
                    HoraFin = table.Column<TimeSpan>(type: "time", nullable: false),
                    CantidadPersonas = table.Column<int>(type: "int", nullable: false),
                    Monto = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Moneda = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    Estado = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    MotivoCancelacion = table.Column<string>(type: "varchar(500)", unicode: false, maxLength: 500, nullable: true),
                    Observaciones = table.Column<string>(type: "varchar(500)", unicode: false, maxLength: 500, nullable: true),
                    FechaRegistro = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AlquileresEspacio", x => x.Id);
                    table.CheckConstraint("CK_AlquileresEspacio_CantidadPersonas", "[CantidadPersonas] > 0");
                    table.CheckConstraint("CK_AlquileresEspacio_Estado", "[Estado] IN ('Reservado', 'Cancelado')");
                    table.CheckConstraint("CK_AlquileresEspacio_Horas", "[HoraFin] > [HoraInicio]");
                    table.CheckConstraint("CK_AlquileresEspacio_Moneda", "[Moneda] IN ('Colones', 'Dólares', 'Euros')");
                    table.CheckConstraint("CK_AlquileresEspacio_Monto", "[Monto] >= 0");
                    table.CheckConstraint("CK_AlquileresEspacio_MotivoCancelacion", "([Estado] = 'Cancelado' AND [MotivoCancelacion] IS NOT NULL) OR ([Estado] <> 'Cancelado' AND [MotivoCancelacion] IS NULL)");
                    table.ForeignKey(
                        name: "FK_AlquileresEspacio_Arrendatarios_ArrendatarioId",
                        column: x => x.ArrendatarioId,
                        principalTable: "Arrendatarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CaracteristicasEspacio",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nombre = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    Estado = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CaracteristicasEspacio", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EspaciosFisicos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nombre = table.Column<string>(type: "varchar(100)", unicode: false, maxLength: 100, nullable: false),
                    Capacidad = table.Column<int>(type: "int", nullable: true),
                    Estado = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EspaciosFisicos", x => x.Id);
                    table.CheckConstraint("CK_EspaciosFisicos_Capacidad", "[Capacidad] IS NULL OR [Capacidad] > 0");
                });

            migrationBuilder.CreateTable(
                name: "CaracteristicasAlquiler",
                columns: table => new
                {
                    AlquilerEspacioId = table.Column<int>(type: "int", nullable: false),
                    CaracteristicaEspacioId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CaracteristicasAlquiler", x => new { x.AlquilerEspacioId, x.CaracteristicaEspacioId });
                    table.ForeignKey(
                        name: "FK_CaracteristicasAlquiler_AlquileresEspacio_AlquilerEspacioId",
                        column: x => x.AlquilerEspacioId,
                        principalTable: "AlquileresEspacio",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CaracteristicasAlquiler_CaracteristicasEspacio_CaracteristicaEspacioId",
                        column: x => x.CaracteristicaEspacioId,
                        principalTable: "CaracteristicasEspacio",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EspaciosAlquiler",
                columns: table => new
                {
                    AlquilerEspacioId = table.Column<int>(type: "int", nullable: false),
                    EspacioFisicoId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EspaciosAlquiler", x => new { x.AlquilerEspacioId, x.EspacioFisicoId });
                    table.ForeignKey(
                        name: "FK_EspaciosAlquiler_AlquileresEspacio_AlquilerEspacioId",
                        column: x => x.AlquilerEspacioId,
                        principalTable: "AlquileresEspacio",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EspaciosAlquiler_EspaciosFisicos_EspacioFisicoId",
                        column: x => x.EspacioFisicoId,
                        principalTable: "EspaciosFisicos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "CaracteristicasEspacio",
                columns: new[] { "Id", "Estado", "Nombre" },
                values: new object[,]
                {
                    { 1, true, "Luz" },
                    { 2, true, "Agua" },
                    { 3, true, "Internet" },
                    { 4, true, "Decoración adicional" },
                    { 5, true, "Mobiliario" }
                });

            migrationBuilder.InsertData(
                table: "EspaciosFisicos",
                columns: new[] { "Id", "Capacidad", "Estado", "Nombre" },
                values: new object[,]
                {
                    { 1, null, true, "Área de juego" },
                    { 2, null, true, "Sala de servicio" },
                    { 3, null, true, "Baños" },
                    { 4, null, true, "Cocina (solo para servir)" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_AlquileresEspacio_Arrendatario",
                table: "AlquileresEspacio",
                column: "ArrendatarioId");

            migrationBuilder.CreateIndex(
                name: "IX_AlquileresEspacio_Fecha_HoraInicio",
                table: "AlquileresEspacio",
                columns: new[] { "Fecha", "HoraInicio" });

            migrationBuilder.CreateIndex(
                name: "IX_CaracteristicasAlquiler_CaracteristicaEspacio",
                table: "CaracteristicasAlquiler",
                column: "CaracteristicaEspacioId");

            migrationBuilder.CreateIndex(
                name: "UX_CaracteristicasEspacio_Nombre",
                table: "CaracteristicasEspacio",
                column: "Nombre",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EspaciosAlquiler_EspacioFisico",
                table: "EspaciosAlquiler",
                column: "EspacioFisicoId");

            migrationBuilder.CreateIndex(
                name: "UX_EspaciosFisicos_Nombre",
                table: "EspaciosFisicos",
                column: "Nombre",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CaracteristicasAlquiler");

            migrationBuilder.DropTable(
                name: "EspaciosAlquiler");

            migrationBuilder.DropTable(
                name: "CaracteristicasEspacio");

            migrationBuilder.DropTable(
                name: "AlquileresEspacio");

            migrationBuilder.DropTable(
                name: "EspaciosFisicos");
        }
    }
}
