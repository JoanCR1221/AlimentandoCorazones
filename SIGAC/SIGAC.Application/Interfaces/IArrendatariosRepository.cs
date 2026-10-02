using SIGAC.Domain.Entities;

namespace SIGAC.Application.Interfaces
{
    public interface IArrendatariosRepository
    {
        // Lanza DuplicateException si la identificación choca contra
        // UX_Arrendatarios_Identificacion (dos altas simultáneas de la misma
        // persona pasan las dos el chequeo previo del servicio).
        Task AgregarAsync(Arrendatario arrendatario);

        Task<Arrendatario?> ObtenerPorIdAsync(int id);

        // Comparación exacta contra el valor ya normalizado
        // (ArrendatarioValidator.NormalizarIdentificacion).
        Task<Arrendatario?> BuscarPorIdentificacionAsync(string identificacion);

        // Para el buscador del formulario de alquiler: solo activos, por nombre o
        // identificación, sin distinguir tildes. Acotado a maximo filas.
        Task<IReadOnlyList<Arrendatario>> BuscarActivosAsync(string texto, int maximo);
    }
}
