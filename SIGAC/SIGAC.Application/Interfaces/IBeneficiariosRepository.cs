using SIGAC.Domain.Entities;
using SIGAC.Application.DTOs.Beneficiarios;

namespace SIGAC.Application.Interfaces
{
    public interface IBeneficiariosRepository
    {
        Task AgregarAsync(Beneficiario beneficiario);
        Task ActualizarAsync(Beneficiario beneficiario);
        Task<Beneficiario?> ObtenerPorIdAsync(int id);
        // La comparación es normalizada (sin tildes, sin mayúsculas y sin espacios
        // sobrantes). idExcluir permite editar un beneficiario sin chocar consigo mismo.
        Task<bool> ExisteAsync(string nombre, string primerApellido, string segundoApellido, DateTime fechaNacimiento, int? idExcluir = null);
        Task<IEnumerable<Beneficiario>> ObtenerTodosAsync(FiltrosBeneficiarioDto filtros);
        Task CambiarEstadoAsync(int id, bool estado);
    }
}