using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIGAC.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUnicidadParticipanteProyecto : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Hasta ahora la unicidad la garantizaba solo ExisteParticipanteAsync, así
            // que una base que ya venía usándose puede tener al mismo beneficiario
            // repetido en un proyecto, y sobre esas filas el CREATE UNIQUE INDEX de
            // abajo falla entero. Se deja la participación más antigua de cada par
            // (ProyectoId, BeneficiarioId) y se borran las repetidas: son el mismo
            // dato registrado dos veces, no información distinta.
            //
            // Los participantes externos quedan fuera del WHERE: llevan BeneficiarioId
            // en NULL y el índice nuevo los excluye con su filtro.
            migrationBuilder.Sql(@"
                WITH Repetidos AS (
                    SELECT ROW_NUMBER() OVER (
                        PARTITION BY ProyectoId, BeneficiarioId
                        ORDER BY FechaRegistro, Id) AS Fila
                    FROM ParticipantesProyecto
                    WHERE BeneficiarioId IS NOT NULL
                )
                DELETE FROM Repetidos WHERE Fila > 1;");

            migrationBuilder.DropIndex(
                name: "IX_ParticipantesProyecto_Proyecto_Beneficiario",
                table: "ParticipantesProyecto");

            migrationBuilder.CreateIndex(
                name: "UX_ParticipantesProyecto_Proyecto_Beneficiario",
                table: "ParticipantesProyecto",
                columns: new[] { "ProyectoId", "BeneficiarioId" },
                unique: true,
                filter: "[BeneficiarioId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_ParticipantesProyecto_Proyecto_Beneficiario",
                table: "ParticipantesProyecto");

            migrationBuilder.CreateIndex(
                name: "IX_ParticipantesProyecto_Proyecto_Beneficiario",
                table: "ParticipantesProyecto",
                columns: new[] { "ProyectoId", "BeneficiarioId" });
        }
    }
}
