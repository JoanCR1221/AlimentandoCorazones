using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIGAC.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTablaArrendatarios : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Bitacora_Modulo",
                table: "Bitacora");

            migrationBuilder.CreateTable(
                name: "Arrendatarios",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nombre = table.Column<string>(type: "varchar(150)", unicode: false, maxLength: 150, nullable: false),
                    TipoPersona = table.Column<string>(type: "varchar(20)", unicode: false, maxLength: 20, nullable: false),
                    Identificacion = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: true),
                    CodigoPaisTelefono = table.Column<string>(type: "varchar(4)", unicode: false, maxLength: 4, nullable: false),
                    Telefono = table.Column<string>(type: "varchar(15)", unicode: false, maxLength: 15, nullable: false),
                    Correo = table.Column<string>(type: "varchar(150)", unicode: false, maxLength: 150, nullable: true),
                    Estado = table.Column<bool>(type: "bit", nullable: false),
                    FechaRegistro = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Arrendatarios", x => x.Id);
                    table.CheckConstraint("CK_Arrendatarios_TipoPersona", "[TipoPersona] IN ('Física', 'Jurídica')");
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Bitacora_Modulo",
                table: "Bitacora",
                sql: "[Modulo] IN ('Beneficiarios', 'Asistencia', 'Inventario', 'Donaciones', 'Gastos', 'Proyectos', 'Seguridad', 'Reportes', 'Alquileres')");

            migrationBuilder.CreateIndex(
                name: "IX_Arrendatarios_Estado",
                table: "Arrendatarios",
                column: "Estado");

            migrationBuilder.CreateIndex(
                name: "IX_Arrendatarios_Nombre",
                table: "Arrendatarios",
                column: "Nombre");

            migrationBuilder.CreateIndex(
                name: "UX_Arrendatarios_Identificacion",
                table: "Arrendatarios",
                column: "Identificacion",
                unique: true,
                filter: "[Identificacion] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Arrendatarios");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Bitacora_Modulo",
                table: "Bitacora");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Bitacora_Modulo",
                table: "Bitacora",
                sql: "[Modulo] IN ('Beneficiarios', 'Asistencia', 'Inventario', 'Donaciones', 'Gastos', 'Proyectos', 'Seguridad', 'Reportes')");
        }
    }
}
