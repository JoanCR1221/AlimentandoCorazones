using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIGAC.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCheckMotivoAnulacionEntradaInventario : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Regulariza filas incoherentes escritas por SQL manual antes de crear el
            // CHECK, mismo criterio que la limpieza previa a las FK de ea3ae03: los dos
            // UPDATE son condicionales e idempotentes, y en una base sana no tocan
            // ninguna fila.
            //
            // A una entrada anulada se le completa el motivo y NO se revierte la
            // anulación: AnularEntradaConStockAsync ya descontó su cantidad del
            // StockActual, así que devolverla a vigente la dejaría sumando un stock que
            // ya se restó. El texto declara de dónde salió para no confundirse con un
            // motivo escrito por un usuario.
            migrationBuilder.Sql(@"
                UPDATE EntradasInventario
                SET MotivoAnulacion = 'Motivo no registrado (regularizado al crear CK_EntradasInventario_MotivoAnulacion)'
                WHERE Anulada = 1 AND MotivoAnulacion IS NULL;");

            // La dirección contraria: una entrada vigente no puede arrastrar el motivo
            // de una anulación que se revirtió. Sólo se limpia el texto huérfano;
            // Anulada no se toca, así que el stock queda igual.
            migrationBuilder.Sql(@"
                UPDATE EntradasInventario
                SET MotivoAnulacion = NULL
                WHERE Anulada = 0 AND MotivoAnulacion IS NOT NULL;");

            migrationBuilder.AddCheckConstraint(
                name: "CK_EntradasInventario_MotivoAnulacion",
                table: "EntradasInventario",
                sql: "([Anulada] = 1 AND [MotivoAnulacion] IS NOT NULL) OR ([Anulada] = 0 AND [MotivoAnulacion] IS NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_EntradasInventario_MotivoAnulacion",
                table: "EntradasInventario");
        }
    }
}
