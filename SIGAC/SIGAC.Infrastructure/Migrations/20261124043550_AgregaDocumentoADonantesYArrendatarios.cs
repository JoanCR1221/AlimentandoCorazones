using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIGAC.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AgregaDocumentoADonantesYArrendatarios : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Identificacion",
                table: "Donantes",
                type: "varchar(30)",
                unicode: false,
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TipoDocumento",
                table: "Donantes",
                type: "varchar(50)",
                unicode: false,
                maxLength: 50,
                nullable: false,
                defaultValue: "Sin documento");

            migrationBuilder.AddColumn<string>(
                name: "TipoDocumentoOtro",
                table: "Donantes",
                type: "varchar(100)",
                unicode: false,
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TipoDocumento",
                table: "Arrendatarios",
                type: "varchar(50)",
                unicode: false,
                maxLength: 50,
                nullable: false,
                defaultValue: "Sin documento");

            migrationBuilder.AddColumn<string>(
                name: "TipoDocumentoOtro",
                table: "Arrendatarios",
                type: "varchar(100)",
                unicode: false,
                maxLength: 100,
                nullable: true);

            // Los donantes no tenían documento: quedan "Sin documento" por el DEFAULT.
            // Los arrendatarios SÍ tenían Identificacion (sin tipo): se clasifican por
            // su forma, que es lo que permite la columna nueva sin perder el dato.
            // Los textos van literales y no desde TiposDocumento: una migración
            // describe cómo eran las cosas al escribirla, no como sean después.
            //   9 dígitos  -> cédula nacional
            //   10 dígitos -> cédula jurídica
            //   11 o 12    -> DIMEX
            //   otra cosa  -> "Otro" (pasaporte u otro documento; no se sabe cuál)
            migrationBuilder.Sql(@"
UPDATE Arrendatarios
   SET TipoDocumento = CASE
         WHEN Identificacion NOT LIKE '%[^0-9]%' AND LEN(Identificacion) = 9  THEN 'Cédula nacional'
         WHEN Identificacion NOT LIKE '%[^0-9]%' AND LEN(Identificacion) = 10 THEN 'Cédula jurídica'
         WHEN Identificacion NOT LIKE '%[^0-9]%' AND LEN(Identificacion) IN (11, 12) THEN 'DIMEX (cédula de residencia)'
         ELSE 'Otro' END,
       TipoDocumentoOtro = CASE
         WHEN Identificacion NOT LIKE '%[^0-9]%' AND LEN(Identificacion) IN (9, 10, 11, 12) THEN NULL
         ELSE 'Sin especificar' END
 WHERE Identificacion IS NOT NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Identificacion",
                table: "Donantes");

            migrationBuilder.DropColumn(
                name: "TipoDocumento",
                table: "Donantes");

            migrationBuilder.DropColumn(
                name: "TipoDocumentoOtro",
                table: "Donantes");

            migrationBuilder.DropColumn(
                name: "TipoDocumento",
                table: "Arrendatarios");

            migrationBuilder.DropColumn(
                name: "TipoDocumentoOtro",
                table: "Arrendatarios");
        }
    }
}
