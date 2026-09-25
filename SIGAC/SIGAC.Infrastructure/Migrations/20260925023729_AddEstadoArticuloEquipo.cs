using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIGAC.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEstadoArticuloEquipo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_Articulos_Nombre",
                table: "Articulos");

            migrationBuilder.AddColumn<string>(
                name: "Estado",
                table: "DetallesDonacionEspecie",
                type: "varchar(50)",
                unicode: false,
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Estado",
                table: "Articulos",
                type: "varchar(50)",
                unicode: false,
                maxLength: 50,
                nullable: true);

            // Backfill ANTES de crear los CHECK: los CHECK exigen que todo Equipo
            // tenga estado, y los que ya existen entran con la columna en NULL. Se
            // marcan "En buen estado", el valor neutro: no afirma que sean nuevos
            // ni que estén dañados, que es algo que nadie ha verificado. Los
            // literales van escritos a propósito y no leídos de EstadosArticulo: una
            // migración es una foto de lo que era cierto en su momento y no debe
            // cambiar si después se renombra una constante.
            //
            // Hace falta también en DetallesDonacionEspecie: las donaciones en
            // especie de Equipo ya registradas violarían igual su CHECK.
            migrationBuilder.Sql(
                "UPDATE [Articulos] SET [Estado] = 'En buen estado' WHERE [Categoria] = 'Equipo';");

            migrationBuilder.Sql(
                "UPDATE [DetallesDonacionEspecie] SET [Estado] = 'En buen estado' WHERE [Categoria] = 'Equipo';");

            migrationBuilder.AddCheckConstraint(
                name: "CK_DetallesDonacionEspecie_Estado_Coherente",
                table: "DetallesDonacionEspecie",
                sql: "([Categoria] = 'Equipo' AND [Estado] IN ('Nuevo', 'En buen estado', 'Dañado')) OR ([Categoria] <> 'Equipo' AND [Estado] IS NULL)");

            migrationBuilder.CreateIndex(
                name: "UX_Articulos_Nombre_Estado",
                table: "Articulos",
                columns: new[] { "Nombre", "Estado" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Articulos_Estado_Coherente",
                table: "Articulos",
                sql: "([Categoria] = 'Equipo' AND [Estado] IN ('Nuevo', 'En buen estado', 'Dañado')) OR ([Categoria] <> 'Equipo' AND [Estado] IS NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_DetallesDonacionEspecie_Estado_Coherente",
                table: "DetallesDonacionEspecie");

            migrationBuilder.DropIndex(
                name: "UX_Articulos_Nombre_Estado",
                table: "Articulos");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Articulos_Estado_Coherente",
                table: "Articulos");

            migrationBuilder.DropColumn(
                name: "Estado",
                table: "DetallesDonacionEspecie");

            migrationBuilder.DropColumn(
                name: "Estado",
                table: "Articulos");

            // Falla si ya hay dos artículos con el mismo nombre en distinto estado
            // (por ejemplo "Silla / Nuevo" y "Silla / Dañado"): al quitar el estado
            // esas filas vuelven a chocar por nombre y hay que fusionarlas a mano
            // antes de revertir. Es la consecuencia natural de perder la columna.
            migrationBuilder.CreateIndex(
                name: "UX_Articulos_Nombre",
                table: "Articulos",
                column: "Nombre",
                unique: true);
        }
    }
}
