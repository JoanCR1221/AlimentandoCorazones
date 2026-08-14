using SIGAC.Application.DTOs.Beneficiarios;

namespace SIGAC.Application.Interfaces
{
    public interface IBeneficiariosService
    {
        Task RegistrarBeneficiarioAsync(BeneficiarioCrearDto dto);
        Task<BeneficiarioEditarDto?> ObtenerParaEditarAsync(int id);
        Task ActualizarBeneficiarioAsync(int id, BeneficiarioEditarDto dto);
        Task<IEnumerable<BeneficiarioListaDto>> ObtenerBeneficiariosAsync(FiltrosBeneficiarioDto filtros);
        Task ActivarBeneficiarioAsync(int id);
        Task DesactivarBeneficiarioAsync(int id);
    }
}