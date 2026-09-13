using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIGAC.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMonedaADonacionesYGastos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Moneda",
                table: "GastosOperativos",
                type: "varchar(20)",
                unicode: false,
                maxLength: 20,
                nullable: false,
                defaultValue: "Colones");

            migrationBuilder.AddColumn<string>(
                name: "Moneda",
                table: "DonacionesDinero",
                type: "varchar(20)",
                unicode: false,
                maxLength: 20,
                nullable: false,
                defaultValue: "Colones");

            migrationBuilder.AddCheckConstraint(
                name: "CK_GastosOperativos_Moneda",
                table: "GastosOperativos",
                sql: "[Moneda] IN ('Colones', 'Dólares', 'Euros')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_DonacionesDinero_Moneda",
                table: "DonacionesDinero",
                sql: "[Moneda] IN ('Colones', 'Dólares', 'Euros')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_GastosOperativos_Moneda",
                table: "GastosOperativos");

            migrationBuilder.DropCheckConstraint(
                name: "CK_DonacionesDinero_Moneda",
                table: "DonacionesDinero");

            migrationBuilder.DropColumn(
                name: "Moneda",
                table: "GastosOperativos");

            migrationBuilder.DropColumn(
                name: "Moneda",
                table: "DonacionesDinero");
        }
    }
}
