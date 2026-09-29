using SIGAC.Application.DTOs.Alquileres;

namespace SIGAC.Application.Interfaces
{
    public interface IArrendatariosService
    {
        // Devuelve el Id del arrendatario recién creado, mismo criterio que
        // ProyectosService.RegistrarProyectoAsync.
        Task<int> RegistrarArrendatarioAsync(ArrendatarioCrearDto dto);

        Task<IReadOnlyList<ArrendatarioListaDto>> BuscarArrendatariosActivosAsync(string texto);

        // Null si no existe o está inactivo: lo usa el formulario de alquiler para
        // precargar al arrendatario recién registrado (?arrendatarioId=).
        Task<ArrendatarioListaDto?> ObtenerArrendatarioActivoAsync(int id);
    }
}
