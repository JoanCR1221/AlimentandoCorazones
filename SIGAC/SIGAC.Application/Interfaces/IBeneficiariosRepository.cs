using SIGAC.Domain.Entities;
using SIGAC.Application.DTOs;
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
        Task<bool> ExisteAsync(string primerNombre, string segundoNombre, string primerApellido, string segundoApellido, DateTime fechaNacimiento, int? idExcluir = null);
        // Devuelve una sola página, ya filtrada y ordenada en SQL, junto con el
        // total de registros que cumplen los filtros (que la grilla necesita para
        // saber cuántas páginas hay). Nunca materializa la tabla entera.
        Task<ResultadoPaginado<Beneficiario>> ObtenerPaginaAsync(FiltrosBeneficiarioDto filtros);
        Task CambiarEstadoAsync(int id, bool estado);
    }
}