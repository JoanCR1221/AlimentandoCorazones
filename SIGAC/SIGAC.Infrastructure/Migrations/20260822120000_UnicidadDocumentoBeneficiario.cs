using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIGAC.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UnicidadDocumentoBeneficiario : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Los datos ya guardados se normalizan igual que los nuevos, o el índice
            // no detectaría que "1 2345 6789" y "123456789" son el mismo documento.
            //
            // Después se unifica el "sin documento" en NULL: el código nunca escribe
            // cadena vacía, pero filas anteriores a la validación pueden tenerla, y
            // con ambas formas conviviendo el filtro del índice sería ambiguo.
            migrationBuilder.Sql(@"
UPDATE Beneficiarios
   SET NumIdentidad = REPLACE(REPLACE(REPLACE(NumIdentidad, ' ', ''), CHAR(9), ''), CHAR(160), '')
 WHERE NumIdentidad IS NOT NULL
   AND NumIdentidad <> REPLACE(REPLACE(REPLACE(NumIdentidad, ' ', ''), CHAR(9), ''), CHAR(160), '');

UPDATE Beneficiarios
   SET NumIdentidad = NULL
 WHERE NumIdentidad IS NOT NULL
   AND NumIdentidad = '';");

            // Índice FILTRADO: excluye a los beneficiarios sin documento. La
            // organización atiende varias personas indocumentadas y, sin el filtro,
            // chocarían todas entre sí (en SQL Server el índice único trata dos NULL
            // como iguales).
            //
            // Respalda en la base la validación del servicio y cierra la condición de
            // carrera entre el SELECT previo y el INSERT.
            migrationBuilder.CreateIndex(
                name: "UX_Beneficiarios_TipoDocumento_NumIdentidad",
                table: "Beneficiarios",
                columns: new[] { "TipoDocumento", "NumIdentidad" },
                unique: true,
                filter: "[NumIdentidad] IS NOT NULL AND [NumIdentidad] <> ''");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_Beneficiarios_TipoDocumento_NumIdentidad",
                table: "Beneficiarios");
        }
    }
}
