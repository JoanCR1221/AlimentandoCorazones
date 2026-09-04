using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIGAC.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AmpliaNombresYValidacionesBeneficiario : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // El índice único se recrea al final: cambian sus columnas (Nombre pasa
            // a llamarse PrimerNombre y se suma SegundoNombre).
            migrationBuilder.DropIndex(
                name: "UX_Beneficiarios_Nombre_Apellidos_FechaNacimiento",
                table: "Beneficiarios");

            migrationBuilder.RenameColumn(
                name: "Nombre",
                table: "Beneficiarios",
                newName: "PrimerNombre");

            // NOT NULL con cadena vacía por defecto, igual que SegundoApellido: en
            // SQL Server dos NULL no se consideran iguales y el índice único no
            // detectaría los duplicados entre personas sin segundo nombre.
            migrationBuilder.AddColumn<string>(
                name: "SegundoNombre",
                table: "Beneficiarios",
                type: "varchar(100)",
                unicode: false,
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            // El teléfono pasa a ser de 8 dígitos exactos (formato de Costa Rica).
            // Los valores viejos con guiones, espacios o de otra longitud ya no
            // entran en la columna: se descartan antes de achicarla.
            migrationBuilder.Sql(@"
UPDATE Beneficiarios
   SET Telefono = NULL
 WHERE Telefono IS NOT NULL
   AND (LEN(Telefono) <> 8 OR Telefono LIKE '%[^0-9]%');");

            migrationBuilder.AlterColumn<string>(
                name: "Telefono",
                table: "Beneficiarios",
                type: "varchar(8)",
                unicode: false,
                maxLength: 8,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(20)",
                oldUnicode: false,
                oldMaxLength: 20,
                oldNullable: true);

            // TipoDocumento pasa a ser una lista cerrada (SIGAC.Domain.TiposDocumento).
            // Se reasignan los valores viejos, o las filas existentes quedarían
            // inválidas la próxima vez que se editaran.
            // DIMEX y "cédula de residencia" eran dos opciones para el mismo documento
            // y ahora convergen en una sola.
            migrationBuilder.Sql(@"
UPDATE Beneficiarios
   SET TipoDocumento = 'Cédula nacional'
 WHERE TipoDocumento = 'Cédula';

UPDATE Beneficiarios
   SET TipoDocumento = 'DIMEX (cédula de residencia)'
 WHERE TipoDocumento IN ('DIMEX', 'Cédula de residencia');

-- Solo las filas sin tipo Y sin número pasan a 'Sin documento': si tienen un
-- número no se toca nada, para no perder el dato. Esas quedan con el tipo en NULL
-- y hay que corregirlas a mano o al editarlas.
UPDATE Beneficiarios
   SET TipoDocumento = 'Sin documento'
 WHERE (TipoDocumento IS NULL OR LTRIM(RTRIM(TipoDocumento)) = '')
   AND (NumIdentidad IS NULL OR LTRIM(RTRIM(NumIdentidad)) = '');

-- 'Sin documento' no lleva número: no se deja un número huérfano que no se
-- corresponda con lo que muestra la pantalla.
UPDATE Beneficiarios
   SET NumIdentidad = NULL
 WHERE TipoDocumento = 'Sin documento';

-- La especificación solo tiene sentido con el tipo 'Otro'.
UPDATE Beneficiarios
   SET TipoDocumentoOtro = NULL
 WHERE TipoDocumento <> 'Otro' OR TipoDocumento IS NULL;");

            migrationBuilder.CreateIndex(
                name: "UX_Beneficiarios_Nombres_Apellidos_FechaNacimiento",
                table: "Beneficiarios",
                columns: new[] { "PrimerNombre", "SegundoNombre", "PrimerApellido", "SegundoApellido", "FechaNacimiento" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_Beneficiarios_Nombres_Apellidos_FechaNacimiento",
                table: "Beneficiarios");

            migrationBuilder.DropColumn(
                name: "SegundoNombre",
                table: "Beneficiarios");

            migrationBuilder.AlterColumn<string>(
                name: "Telefono",
                table: "Beneficiarios",
                type: "varchar(20)",
                unicode: false,
                maxLength: 20,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(8)",
                oldUnicode: false,
                oldMaxLength: 8,
                oldNullable: true);

            migrationBuilder.RenameColumn(
                name: "PrimerNombre",
                table: "Beneficiarios",
                newName: "Nombre");

            migrationBuilder.CreateIndex(
                name: "UX_Beneficiarios_Nombre_Apellidos_FechaNacimiento",
                table: "Beneficiarios",
                columns: new[] { "Nombre", "PrimerApellido", "SegundoApellido", "FechaNacimiento" },
                unique: true);
        }
    }
}
