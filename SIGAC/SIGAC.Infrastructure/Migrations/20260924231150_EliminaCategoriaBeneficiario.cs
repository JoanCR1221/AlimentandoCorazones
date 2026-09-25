using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIGAC.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EliminaCategoriaBeneficiario : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // La categoría se guardaba al registrar o editar y quedaba vieja cuando
            // la persona cumplía años (un niño de 12 seguía apareciendo como
            // "Niño" en el filtro). Pasa a derivarse siempre de FechaNacimiento
            // (CategoriasBeneficiario), así que la columna sobra. El filtro por
            // categoría usa ahora un rango de fechas: de ahí el índice nuevo.
            migrationBuilder.DropIndex(
                name: "IX_Beneficiarios_Categoria",
                table: "Beneficiarios");

            migrationBuilder.DropColumn(
                name: "Categoria",
                table: "Beneficiarios");

            migrationBuilder.CreateIndex(
                name: "IX_Beneficiarios_FechaNacimiento",
                table: "Beneficiarios",
                column: "FechaNacimiento");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Beneficiarios_FechaNacimiento",
                table: "Beneficiarios");

            migrationBuilder.AddColumn<string>(
                name: "Categoria",
                table: "Beneficiarios",
                type: "varchar(50)",
                unicode: false,
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            // Se recalcula con la edad cumplida a hoy, con los mismos cortes que
            // CategoriasBeneficiario, para no dejar la columna vacía.
            migrationBuilder.Sql(@"
UPDATE Beneficiarios
   SET Categoria = CASE
         WHEN Edad.Anios <= 11 THEN 'Niño'
         WHEN Edad.Anios <= 17 THEN 'Adolescente'
         WHEN Edad.Anios <= 64 THEN 'Adulto'
         ELSE 'Adulto mayor'
       END
  FROM Beneficiarios
 CROSS APPLY (SELECT DATEDIFF(year, FechaNacimiento, CAST(GETDATE() AS date))
                   - CASE WHEN DATEADD(year, DATEDIFF(year, FechaNacimiento, CAST(GETDATE() AS date)), FechaNacimiento) > CAST(GETDATE() AS date)
                          THEN 1 ELSE 0 END AS Anios) AS Edad;");

            migrationBuilder.CreateIndex(
                name: "IX_Beneficiarios_Categoria",
                table: "Beneficiarios",
                column: "Categoria");
        }
    }
}
