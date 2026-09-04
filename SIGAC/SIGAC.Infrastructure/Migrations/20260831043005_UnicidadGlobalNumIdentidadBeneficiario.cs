using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIGAC.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UnicidadGlobalNumIdentidadBeneficiario : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_Beneficiarios_TipoDocumento_NumIdentidad",
                table: "Beneficiarios");

            migrationBuilder.CreateIndex(
                name: "UX_Beneficiarios_NumIdentidad",
                table: "Beneficiarios",
                column: "NumIdentidad",
                unique: true,
                filter: "[NumIdentidad] IS NOT NULL AND [NumIdentidad] <> ''");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_Beneficiarios_NumIdentidad",
                table: "Beneficiarios");

            migrationBuilder.CreateIndex(
                name: "UX_Beneficiarios_TipoDocumento_NumIdentidad",
                table: "Beneficiarios",
                columns: new[] { "TipoDocumento", "NumIdentidad" },
                unique: true,
                filter: "[NumIdentidad] IS NOT NULL AND [NumIdentidad] <> ''");
        }
    }
}
