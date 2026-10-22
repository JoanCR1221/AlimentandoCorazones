using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace SIGAC.Infrastructure.Repositories
{
    // Lectura de los errores de SQL Server que los repositorios traducen a mensajes
    // para el usuario. Existe para que "este INSERT chocó contra un índice único" se
    // reconozca igual en todos los repositorios, en vez de repetir los números.
    internal static class ErroresSql
    {
        // 2627: violación de una restricción UNIQUE. 2601: de un índice único.
        private const int RestriccionUnica = 2627;
        private const int IndiceUnico = 2601;

        public static bool EsViolacionDeUnicidad(DbUpdateException ex) =>
            ex.InnerException is SqlException sql &&
            (sql.Number == RestriccionUnica || sql.Number == IndiceUnico);

        // SQL Server nombra el índice violado dentro del texto del error: permite
        // distinguir CUÁL de varios índices únicos rechazó la escritura.
        public static bool ViolaIndice(DbUpdateException ex, string nombreIndice) =>
            EsViolacionDeUnicidad(ex) &&
            ex.InnerException!.Message.Contains(nombreIndice, StringComparison.OrdinalIgnoreCase);
    }
}
