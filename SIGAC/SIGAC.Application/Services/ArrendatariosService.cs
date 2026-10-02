using SIGAC.Application.DTOs.Alquileres;
using SIGAC.Application.Exceptions;
using SIGAC.Application.Interfaces;
using SIGAC.Application.Validators;
using SIGAC.Domain;
using SIGAC.Domain.Entities;

namespace SIGAC.Application.Services
{
    // Orquesta el alta y la búsqueda de arrendatarios: valida con
    // ArrendatarioValidator, arma la entidad y delega en el repositorio.
    public class ArrendatariosService : IArrendatariosService
    {
        // Lo que muestra el buscador del formulario de alquiler: suficiente para
        // elegir, sin traer la tabla entera en cada tecla.
        private const int MaximoResultadosBusqueda = 20;

        private readonly IArrendatariosRepository _repository;
        private readonly IBitacoraService _bitacora;

        public ArrendatariosService(IArrendatariosRepository repository, IBitacoraService bitacora)
        {
            _repository = repository;
            _bitacora = bitacora;
        }

        public async Task<int> RegistrarArrendatarioAsync(ArrendatarioCrearDto dto)
        {
            try
            {
                var datos = ArrendatarioValidator.Validar(dto);

                // A diferencia de Donante, acá SÍ se bloquea el duplicado: la
                // identificación, cuando se da, distingue a dos personas sin
                // ambigüedad. El índice único respalda este chequeo ante dos altas
                // simultáneas (ver ArrendatariosRepositoryEfCore.AgregarAsync).
                if (datos.Identificacion is not null)
                {
                    var existente = await _repository.BuscarPorIdentificacionAsync(datos.Identificacion);
                    if (existente is not null)
                        throw new DuplicateException(
                            $"Ya existe un arrendatario con la identificación {datos.Identificacion}: {existente.Nombre}.");
                }

                var arrendatario = new Arrendatario
                {
                    Nombre = datos.Nombre,
                    TipoPersona = datos.TipoPersona,
                    Identificacion = datos.Identificacion,
                    CodigoPaisTelefono = datos.CodigoPaisTelefono,
                    Telefono = datos.Telefono,
                    Correo = datos.Correo,
                    Estado = true,
                    FechaRegistro = DateTime.Now
                };

                await _repository.AgregarAsync(arrendatario);

                await _bitacora.RegistrarAsync(AccionesBitacora.Registrar, ModulosSistema.Alquileres,
                    $"Arrendatario #{arrendatario.Id}: {arrendatario.Nombre} ({arrendatario.TipoPersona})");

                return arrendatario.Id;
            }
            catch (Exception ex) when (ex is not ValidationException and not DuplicateException)
            {
                throw new Exception("Error al registrar el arrendatario.", ex);
            }
        }

        public async Task<IReadOnlyList<ArrendatarioListaDto>> BuscarArrendatariosActivosAsync(string texto)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(texto))
                    return Array.Empty<ArrendatarioListaDto>();

                var arrendatarios = await _repository.BuscarActivosAsync(texto.Trim(), MaximoResultadosBusqueda);

                return arrendatarios.Select(ALista).ToList();
            }
            catch (Exception ex)
            {
                throw new Exception("Error al buscar arrendatarios.", ex);
            }
        }

        public async Task<ArrendatarioListaDto?> ObtenerArrendatarioActivoAsync(int id)
        {
            try
            {
                var arrendatario = await _repository.ObtenerPorIdAsync(id);
                return arrendatario is { Estado: true } ? ALista(arrendatario) : null;
            }
            catch (Exception ex)
            {
                throw new Exception("Error al consultar el arrendatario.", ex);
            }
        }

        private static ArrendatarioListaDto ALista(Arrendatario a) => new()
        {
            Id = a.Id,
            Nombre = a.Nombre,
            TipoPersona = a.TipoPersona,
            Identificacion = a.Identificacion,
            TelefonoCompleto = ReglasTelefono.Formatear(a.CodigoPaisTelefono, a.Telefono),
            Correo = a.Correo
        };
    }
}
