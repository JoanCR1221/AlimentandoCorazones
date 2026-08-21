using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIGAC.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SeparaNombreYDerivaCategoriaBeneficiario : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // El índice simple de Nombre se elimina: la columna es la primera del
            // índice único nuevo, que ya cubre las búsquedas que empiezan por ella.
            migrationBuilder.DropIndex(
                name: "IX_Beneficiarios_Nombre",
                table: "Beneficiarios");

            migrationBuilder.AlterColumn<string>(
                name: "Nombre",
                table: "Beneficiarios",
                type: "varchar(100)",
                unicode: false,
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(150)",
                oldUnicode: false,
                oldMaxLength: 150);

            migrationBuilder.AddColumn<string>(
                name: "PrimerApellido",
                table: "Beneficiarios",
                type: "varchar(75)",
                unicode: false,
                maxLength: 75,
                nullable: false,
                defaultValue: "");

            // NOT NULL con cadena vacía por defecto: en SQL Server dos NULL no se
            // consideran iguales, y el índice único no detectaría los duplicados
            // entre personas con un solo apellido.
            migrationBuilder.AddColumn<string>(
                name: "SegundoApellido",
                table: "Beneficiarios",
                type: "varchar(75)",
                unicode: false,
                maxLength: 75,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "UX_Beneficiarios_Nombre_Apellidos_FechaNacimiento",
                table: "Beneficiarios",
                columns: new[] { "Nombre", "PrimerApellido", "SegundoApellido", "FechaNacimiento" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_Beneficiarios_Nombre_Apellidos_FechaNacimiento",
                table: "Beneficiarios");

            migrationBuilder.DropColumn(
                name: "PrimerApellido",
                table: "Beneficiarios");

            migrationBuilder.DropColumn(
                name: "SegundoApellido",
                table: "Beneficiarios");

            migrationBuilder.AlterColumn<string>(
                name: "Nombre",
                table: "Beneficiarios",
                type: "varchar(150)",
                unicode: false,
                maxLength: 150,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(100)",
                oldUnicode: false,
                oldMaxLength: 100);

            migrationBuilder.CreateIndex(
                name: "IX_Beneficiarios_Nombre",
                table: "Beneficiarios",
                column: "Nombre");
        }
    }
}
