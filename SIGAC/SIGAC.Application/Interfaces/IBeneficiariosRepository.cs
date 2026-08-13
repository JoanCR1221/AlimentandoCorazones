using SIGAC.Domain.Entities;
using SIGAC.Application.DTOs.Beneficiarios;

namespace SIGAC.Application.Interfaces
{
    public interface IBeneficiariosRepository
    {
        Task AgregarAsync(Beneficiario beneficiario);
        Task ActualizarAsync(Beneficiario beneficiario);
        Task<Beneficiario?> ObtenerPorIdAsync(int id);
        Task<bool> ExisteAsync(string nombre, DateTime fechaNacimiento);
        Task<IEnumerable<Beneficiario>> ObtenerTodosAsync(FiltrosBeneficiarioDto filtros);
        Task CambiarEstadoAsync(int id, bool estado);
    }
}