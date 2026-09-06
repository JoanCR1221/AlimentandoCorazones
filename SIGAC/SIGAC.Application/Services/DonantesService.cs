using SIGAC.Application.DTOs;
using SIGAC.Application.DTOs.Donaciones;
using SIGAC.Application.Exceptions;
using SIGAC.Application.Interfaces;
using SIGAC.Application.Validators;
using SIGAC.Domain.Entities;

namespace SIGAC.Application.Services
{
    // Orquesta el alta y la edición de donantes: valida con DonanteValidator, arma
    // la entidad y delega en el repositorio. Las reglas de validación no viven acá.
    public class DonantesService : IDonantesService
    {
        private readonly IDonantesRepository _repository;

        public DonantesService(IDonantesRepository repository)
        {
            _repository = repository;
        }

        public async Task RegistrarDonanteAsync(DonanteCrearDto dto)
        {
            try
            {
                var datos = DonanteValidator.Validar(dto);

                // Sin chequeo de duplicados que bloquee, a diferencia de
                // BeneficiariosService (que lanza DuplicateException si ya existe la
                // persona): IX_Donantes_Nombre no es único a propósito porque dos
                // donantes pueden llamarse igual y no hay número de documento con el
                // cual desempatarlos. Un homónimo es un registro legítimo.
                //
                // ExisteNombreAsync existe para que la PANTALLA avise antes de
                // guardar ("ya hay un donante con este nombre, ¿es el mismo?"), no
                // para que el servicio rechace el alta. Por eso no se llama acá:
                // hacerlo y lanzar convertiría el aviso en un bloqueo.
                var donante = new Donante
                {
                    Nombre = datos.Nombre,
                    TipoPersona = datos.TipoPersona,
                    Telefono = datos.Telefono,
                    Correo = datos.Correo,
                    Estado = true,
                    FechaRegistro = DateTime.Now
                };

                await _repository.AgregarAsync(donante);
            }
            catch (Exception ex) when (ex is not ValidationException and not DuplicateException)
            {
                throw new Exception("Error al registrar el donante.", ex);
            }
        }

        public async Task<DonanteEditarDto?> ObtenerParaEditarAsync(int id)
        {
            try
            {
                var donante = await _repository.ObtenerPorIdAsync(id);
                if (donante is null)
                    return null;

                return new DonanteEditarDto
                {
                    Nombre = donante.Nombre,
                    TipoPersona = donante.TipoPersona,
                    Telefono = donante.Telefono,
                    Correo = donante.Correo
                };
            }
            catch (Exception ex)
            {
                throw new Exception("Error al consultar el donante.", ex);
            }
        }

        public async Task ActualizarDonanteAsync(int id, DonanteEditarDto dto)
        {
            try
            {
                var donante = await _repository.ObtenerPorIdAsync(id)
                    ?? throw new NotFoundException("El donante no existe.");

                var datos = DonanteValidator.Validar(dto);

                donante.Nombre = datos.Nombre;
                donante.TipoPersona = datos.TipoPersona;
                donante.Telefono = datos.Telefono;
                donante.Correo = datos.Correo;

                // Estado y FechaRegistro no se tocan: el primero se mueve por
                // Activar/Desactivar y el segundo es histórico. El repositorio
                // tampoco los reescribe (copia campo por campo), así que una
                // desactivación hecha entre la lectura y el guardado no se pierde.
                await _repository.ActualizarAsync(donante);
            }
            catch (Exception ex) when (ex is not ValidationException and not NotFoundException and not DuplicateException)
            {
                throw new Exception("Error al actualizar el donante.", ex);
            }
        }

        public async Task<ResultadoPaginado<DonanteListaDto>> ObtenerDonantesAsync(FiltrosDonanteDto filtros)
        {
            try
            {
                // El repositorio filtra, ordena y pagina en SQL: acá solo llegan los
                // registros de la página pedida, más el total para el paginador.
                var pagina = await _repository.ObtenerTodosAsync(filtros);

                var elementos = pagina.Elementos.Select(d => new DonanteListaDto
                {
                    Id = d.Id,
                    Nombre = d.Nombre,
                    TipoPersona = d.TipoPersona,
                    Correo = d.Correo,
                    Telefono = d.Telefono,
                    Estado = d.Estado
                }).ToList();

                return new ResultadoPaginado<DonanteListaDto>(elementos, pagina.TotalRegistros);
            }
            catch (Exception ex)
            {
                throw new Exception("Error al consultar donantes.", ex);
            }
        }

        public Task ActivarDonanteAsync(int id) => CambiarEstadoAsync(id, true);

        public Task DesactivarDonanteAsync(int id) => CambiarEstadoAsync(id, false);

        // Mismo reparto que BeneficiariosService: los dos métodos públicos dan el
        // nombre a la acción y este privado hace la escritura, que es idéntica en
        // los dos sentidos.
        private async Task CambiarEstadoAsync(int id, bool estado)
        {
            try
            {
                // Se comprueba la existencia antes de delegar: CambiarEstadoAsync del
                // repositorio sale en silencio si el id no existe, así que sin este
                // chequeo desactivar un donante borrado se vería como un éxito.
                _ = await _repository.ObtenerPorIdAsync(id)
                    ?? throw new NotFoundException("El donante no existe.");

                await _repository.CambiarEstadoAsync(id, estado);
            }
            catch (Exception ex) when (ex is not NotFoundException)
            {
                throw new Exception("Error al cambiar el estado del donante.", ex);
            }
        }
    }
}
