using SIGAC.Application.DTOs.Proyectos;
using SIGAC.Application.Exceptions;
using SIGAC.Application.Interfaces;
using SIGAC.Domain;
using SIGAC.Application.Validators;
using SIGAC.Domain.Entities;

namespace SIGAC.Application.Services
{
    // Orquesta el alta, edición, consulta y cierre de proyectos comunitarios, y el
    // registro de sus participantes: valida con ProyectoValidator/
    // ParticipanteProyectoValidator, arma la entidad y delega en el repositorio.
    // Las reglas de validación no viven acá.
    public class ProyectosService : IProyectosService
    {
        private readonly IProyectosRepository _repository;
        private readonly IBeneficiariosRepository _beneficiariosRepository;
        private readonly IBitacoraService _bitacora;

        public ProyectosService(
            IProyectosRepository repository,
            IBeneficiariosRepository beneficiariosRepository,
            IBitacoraService bitacora)
        {
            _repository = repository;
            _beneficiariosRepository = beneficiariosRepository;
            _bitacora = bitacora;
        }

        public async Task<int> RegistrarProyectoAsync(ProyectoCrearDto dto)
        {
            try
            {
                var datos = ProyectoValidator.Validar(dto);

                var proyecto = new ProyectoComunitario
                {
                    Nombre = datos.Nombre,
                    Descripcion = datos.Descripcion,
                    FechaInicio = datos.FechaInicio,
                    FechaEstimadaFin = datos.FechaEstimadaFin,
                    Estado = EstadoProyecto.Planificado,
                    FechaRegistro = DateTime.Now
                };

                await _repository.AgregarAsync(proyecto);

                await _bitacora.RegistrarAsync(AccionesBitacora.Registrar, ModulosSistema.Proyectos,
                    $"Proyecto #{proyecto.Id}: {proyecto.Nombre}");

                return proyecto.Id;
            }
            catch (Exception ex) when (ex is not ValidationException)
            {
                throw new Exception("Error al registrar el proyecto comunitario.", ex);
            }
        }

        // Con la descripción incluida: antes la pantalla de edición se prellenaba
        // con una fila del listado (que no la trae) y obligaba a reescribirla.
        public async Task<ProyectoEditarDto?> ObtenerParaEditarAsync(int id)
        {
            try
            {
                var proyecto = await _repository.ObtenerPorIdAsync(id);
                if (proyecto is null)
                    return null;

                return new ProyectoEditarDto
                {
                    Nombre = proyecto.Nombre,
                    Descripcion = proyecto.Descripcion,
                    FechaInicio = proyecto.FechaInicio,
                    FechaEstimadaFin = proyecto.FechaEstimadaFin,
                    Estado = proyecto.Estado
                };
            }
            catch (Exception ex)
            {
                throw new Exception("Error al consultar el proyecto comunitario.", ex);
            }
        }

        public async Task<ProyectoDetalleDto?> ObtenerDetalleAsync(int id)
        {
            try
            {
                var proyecto = await _repository.ObtenerConParticipantesAsync(id);
                if (proyecto is null)
                    return null;

                return new ProyectoDetalleDto
                {
                    Id = proyecto.Id,
                    Nombre = proyecto.Nombre,
                    Descripcion = proyecto.Descripcion,
                    FechaInicio = proyecto.FechaInicio,
                    FechaEstimadaFin = proyecto.FechaEstimadaFin,
                    FechaFinalizacionReal = proyecto.FechaFinalizacionReal,
                    Estado = proyecto.Estado,
                    FechaRegistro = proyecto.FechaRegistro,
                    Participantes = proyecto.Participantes
                        .Select(p => new ParticipanteListaDto
                        {
                            Id = p.Id,
                            EsBeneficiario = p.EsBeneficiario,
                            BeneficiarioId = p.BeneficiarioId,
                            Nombre = NombreDe(p),
                            Contacto = p.EsBeneficiario
                                ? ReglasTelefono.Formatear(p.Beneficiario?.CodigoPaisTelefono, p.Beneficiario?.Telefono)
                                : p.ContactoExterno,
                            BeneficiarioActivo = p.Beneficiario?.Estado ?? false,
                            FechaRegistro = p.FechaRegistro
                        })
                        .OrderBy(p => p.Nombre, StringComparer.CurrentCultureIgnoreCase)
                        .ToList()
                };
            }
            catch (Exception ex)
            {
                throw new Exception("Error al consultar el proyecto comunitario.", ex);
            }
        }

        public async Task EditarProyectoAsync(int id, ProyectoEditarDto dto)
        {
            try
            {
                var proyecto = await _repository.ObtenerPorIdAsync(id)
                    ?? throw new NotFoundException("El proyecto no existe.");

                // Un proyecto finalizado o cancelado es un registro cerrado: editarlo
                // reabriría una gestión que ya se dio por concluida. Mismo criterio
                // que "no se puede editar un gasto anulado" en GastosService.
                if (EsEstadoTerminal(proyecto.Estado))
                    throw new ValidationException(
                        "No se puede editar un proyecto finalizado o cancelado.");

                var datos = ProyectoValidator.Validar(dto);
                var estado = ProyectoValidator.ValidarEstadoParaEdicion(dto.Estado);

                proyecto.Nombre = datos.Nombre;
                proyecto.Descripcion = datos.Descripcion;
                proyecto.FechaInicio = datos.FechaInicio;
                proyecto.FechaEstimadaFin = datos.FechaEstimadaFin;
                proyecto.Estado = estado;

                await _repository.ActualizarAsync(proyecto);

                await _bitacora.RegistrarAsync(AccionesBitacora.Editar, ModulosSistema.Proyectos,
                    $"Proyecto #{proyecto.Id}: {proyecto.Nombre} ({proyecto.Estado})");
            }
            catch (Exception ex) when (ex is not ValidationException and not NotFoundException)
            {
                throw new Exception("Error al editar el proyecto comunitario.", ex);
            }
        }

        public async Task<IReadOnlyList<ProyectoListaDto>> ObtenerProyectosAsync(FiltrosProyectoDto filtros)
        {
            try
            {
                var proyectos = await _repository.ObtenerTodosAsync(filtros);

                return proyectos
                    .Select(p => new ProyectoListaDto
                    {
                        Id = p.Id,
                        Nombre = p.Nombre,
                        FechaInicio = p.FechaInicio,
                        FechaEstimadaFin = p.FechaEstimadaFin,
                        Estado = p.Estado.ToString(),
                        TotalParticipantes = p.Participantes.Count
                    })
                    .ToList();
            }
            catch (Exception ex)
            {
                throw new Exception("Error al consultar los proyectos comunitarios.", ex);
            }
        }

        public async Task FinalizarProyectoAsync(int id)
        {
            try
            {
                var proyecto = await _repository.ObtenerPorIdAsync(id)
                    ?? throw new NotFoundException("El proyecto no existe.");

                if (proyecto.Estado == EstadoProyecto.Finalizado)
                    throw new ValidationException("El proyecto ya está finalizado.");

                if (proyecto.Estado == EstadoProyecto.Cancelado)
                    throw new ValidationException("No se puede finalizar un proyecto cancelado.");

                await _repository.FinalizarAsync(id);

                await _bitacora.RegistrarAsync(AccionesBitacora.Finalizar, ModulosSistema.Proyectos,
                    $"Proyecto #{id}: {proyecto.Nombre}");
            }
            catch (Exception ex) when (ex is not ValidationException and not NotFoundException)
            {
                throw new Exception("Error al finalizar el proyecto comunitario.", ex);
            }
        }

        public async Task RegistrarParticipanteAsync(ParticipanteCrearDto dto)
        {
            try
            {
                var proyecto = await _repository.ObtenerPorIdAsync(dto.ProyectoId)
                    ?? throw new NotFoundException("El proyecto no existe.");

                if (EsEstadoTerminal(proyecto.Estado))
                    throw new ValidationException(
                        "No se pueden agregar participantes a un proyecto finalizado o cancelado.");

                var datos = ParticipanteProyectoValidator.Validar(dto);

                if (datos.EsBeneficiario)
                {
                    var beneficiario = await _beneficiariosRepository.ObtenerPorIdAsync(datos.BeneficiarioId!.Value)
                        ?? throw new NotFoundException("El beneficiario no existe.");

                    // Distinguible de un ValidationException genérico a propósito:
                    // ver BeneficiarioInactivoException.
                    if (!beneficiario.Estado)
                        throw new BeneficiarioInactivoException(
                            beneficiario.Id,
                            $"El beneficiario '{beneficiario.NombreCompleto}' está inactivo y no puede agregarse como participante.");

                    if (await _repository.ExisteParticipanteAsync(dto.ProyectoId, datos.BeneficiarioId.Value))
                        throw new ValidationException(
                            "Este beneficiario ya está registrado como participante en el proyecto.");
                }

                var participante = new ParticipanteProyecto
                {
                    ProyectoId = dto.ProyectoId,
                    EsBeneficiario = datos.EsBeneficiario,
                    BeneficiarioId = datos.BeneficiarioId,
                    NombreExterno = datos.NombreExterno,
                    ContactoExterno = datos.ContactoExterno,
                    FechaRegistro = DateTime.Now
                };

                await _repository.AgregarParticipanteAsync(participante);

                await _bitacora.RegistrarAsync(AccionesBitacora.Registrar, ModulosSistema.Proyectos,
                    $"Participante en proyecto #{dto.ProyectoId}: " +
                    (participante.EsBeneficiario ? $"beneficiario #{participante.BeneficiarioId}" : participante.NombreExterno));
            }
            catch (Exception ex) when (ex is not ValidationException and not NotFoundException)
            {
                throw new Exception("Error al registrar el participante.", ex);
            }
        }

        // Quitar corrige una carga equivocada (se agregó a quien no correspondía).
        // Mismo límite que agregar: un proyecto cerrado conserva la lista con la que
        // se cerró.
        public async Task QuitarParticipanteAsync(int proyectoId, int participanteId)
        {
            try
            {
                var proyecto = await _repository.ObtenerConParticipantesAsync(proyectoId)
                    ?? throw new NotFoundException("El proyecto no existe.");

                if (EsEstadoTerminal(proyecto.Estado))
                    throw new ValidationException(
                        "No se pueden quitar participantes de un proyecto finalizado o cancelado.");

                var participante = proyecto.Participantes.FirstOrDefault(p => p.Id == participanteId)
                    ?? throw new NotFoundException("El participante no pertenece a este proyecto.");

                // Si otro usuario lo quitó entre la lectura y este borrado, se avisa
                // en vez de dar por hecha una operación que no ocurrió.
                if (!await _repository.QuitarParticipanteAsync(proyectoId, participanteId))
                    throw new NotFoundException("El participante ya no pertenece a este proyecto.");

                await _bitacora.RegistrarAsync(AccionesBitacora.Eliminar, ModulosSistema.Proyectos,
                    $"Participante quitado del proyecto #{proyectoId}: {NombreDe(participante)}");
            }
            catch (Exception ex) when (ex is not ValidationException and not NotFoundException)
            {
                throw new Exception("Error al quitar el participante.", ex);
            }
        }

        // Cómo se nombra a un participante en la lista y en la bitácora. El
        // beneficiario se lee de su ficha (requiere el Include del repositorio);
        // si no vino cargada se cae al id para no mostrar un nombre vacío.
        private static string NombreDe(ParticipanteProyecto participante) =>
            participante.EsBeneficiario
                ? participante.Beneficiario?.NombreCompleto ?? $"Beneficiario #{participante.BeneficiarioId}"
                : participante.NombreExterno ?? string.Empty;

        // Finalizado y Cancelado son los dos estados de cierre: ninguno admite
        // edición posterior ni nuevos participantes.
        private static bool EsEstadoTerminal(EstadoProyecto estado) =>
            estado is EstadoProyecto.Finalizado or EstadoProyecto.Cancelado;
    }
}
