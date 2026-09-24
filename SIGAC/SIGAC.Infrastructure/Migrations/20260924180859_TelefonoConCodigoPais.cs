using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIGAC.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class TelefonoConCodigoPais : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CodigoPaisTelefono",
                table: "Donantes",
                type: "varchar(4)",
                unicode: false,
                maxLength: 4,
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Telefono",
                table: "Beneficiarios",
                type: "varchar(15)",
                unicode: false,
                maxLength: 15,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(8)",
                oldUnicode: false,
                oldMaxLength: 8,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CodigoPaisTelefono",
                table: "Beneficiarios",
                type: "varchar(4)",
                unicode: false,
                maxLength: 4,
                nullable: true);

            // Todos los teléfonos de beneficiarios ya eran de Costa Rica: la
            // migración AmpliaNombresYValidacionesBeneficiario dejó solo los de 8
            // dígitos.
            migrationBuilder.Sql(@"
UPDATE Beneficiarios
   SET CodigoPaisTelefono = '506'
 WHERE Telefono IS NOT NULL;");

            // Los de donantes eran texto libre. Los que, sin separadores, son 8
            // dígitos se toman como de Costa Rica y quedan limpios. El resto no se
            // puede interpretar con seguridad (puede faltar el prefijo o tener una
            // extensión): queda con su texto original y sin código, y se corrige al
            // editar el donante, que ahí sí exige el formato nuevo.
            migrationBuilder.Sql(@"
UPDATE Donantes
   SET Telefono = Limpio.Numero,
       CodigoPaisTelefono = '506'
  FROM Donantes
 CROSS APPLY (SELECT REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(Telefono, ' ', ''), '-', ''), '(', ''), ')', ''), '.', '') AS Numero) AS Limpio
 WHERE Telefono IS NOT NULL
   AND LEN(Limpio.Numero) = 8
   AND Limpio.Numero NOT LIKE '%[^0-9]%';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Antes de perder el código de país: en Donantes se conserva dentro del
            // texto del número, que admite 20 caracteres. En Beneficiarios la
            // columna vuelve a 8 dígitos y solo caben los de Costa Rica.
            migrationBuilder.Sql(@"
UPDATE Donantes
   SET Telefono = '+' + CodigoPaisTelefono + ' ' + Telefono
 WHERE CodigoPaisTelefono IS NOT NULL
   AND CodigoPaisTelefono <> '506'
   AND LEN(CodigoPaisTelefono) + LEN(Telefono) + 2 <= 20;

UPDATE Beneficiarios
   SET Telefono = NULL
 WHERE Telefono IS NOT NULL
   AND (CodigoPaisTelefono <> '506' OR LEN(Telefono) > 8);");

            migrationBuilder.DropColumn(
                name: "CodigoPaisTelefono",
                table: "Donantes");

            migrationBuilder.DropColumn(
                name: "CodigoPaisTelefono",
                table: "Beneficiarios");

            migrationBuilder.AlterColumn<string>(
                name: "Telefono",
                table: "Beneficiarios",
                type: "varchar(8)",
                unicode: false,
                maxLength: 8,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(15)",
                oldUnicode: false,
                oldMaxLength: 15,
                oldNullable: true);
        }
    }
}
