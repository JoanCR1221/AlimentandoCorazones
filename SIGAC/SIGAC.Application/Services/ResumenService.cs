using SIGAC.Application.DTOs;
using SIGAC.Application.Interfaces;

namespace SIGAC.Application.Services
{
    // Sin reglas de negocio: las cifras salen tal cual de las consultas agregadas
    // del repositorio. Existe para que las pantallas no dependan del repositorio
    // y para dar un mensaje de error uniforme.
    public class ResumenService : IResumenService
    {
        private readonly IResumenRepository _repository;

        public ResumenService(IResumenRepository repository)
        {
            _repository = repository;
        }

        public Task<ResumenDonacionesDto> ObtenerResumenDonacionesAsync() =>
            Ejecutar(_repository.ObtenerResumenDonacionesAsync, "donaciones");

        public Task<ResumenInventarioDto> ObtenerResumenInventarioAsync() =>
            Ejecutar(_repository.ObtenerResumenInventarioAsync, "inventario");

        public Task<ResumenAsistenciaDto> ObtenerResumenAsistenciaAsync() =>
            Ejecutar(_repository.ObtenerResumenAsistenciaAsync, "asistencia");

        public Task<ResumenGastosDto> ObtenerResumenGastosAsync() =>
            Ejecutar(_repository.ObtenerResumenGastosAsync, "gastos");

        public Task<ResumenProyectosDto> ObtenerResumenProyectosAsync() =>
            Ejecutar(_repository.ObtenerResumenProyectosAsync, "proyectos");

        private static async Task<T> Ejecutar<T>(Func<Task<T>> consulta, string modulo)
        {
            try
            {
                return await consulta();
            }
            catch (Exception ex)
            {
                throw new Exception($"Error al consultar el resumen de {modulo}.", ex);
            }
        }
    }
}
