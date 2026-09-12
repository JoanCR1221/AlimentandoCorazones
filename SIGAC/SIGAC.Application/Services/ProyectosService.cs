using SIGAC.Application.DTOs.Proyectos;
using SIGAC.Application.Exceptions;
using SIGAC.Application.Interfaces;
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

        public ProyectosService(
            IProyectosRepository repository,
            IBeneficiariosRepository beneficiariosRepository)
        {
            _repository = repository;
            _beneficiariosRepository = beneficiariosRepository;
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

                return proyecto.Id;
            }
            catch (Exception ex) when (ex is not ValidationException)
            {
                throw new Exception("Error al registrar el proyecto comunitario.", ex);
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
            }
            catch (Exception ex) when (ex is not ValidationException and not NotFoundException)
            {
                throw new Exception("Error al registrar el participante.", ex);
            }
        }

        // Finalizado y Cancelado son los dos estados de cierre: ninguno admite
        // edición posterior ni nuevos participantes.
        private static bool EsEstadoTerminal(EstadoProyecto estado) =>
            estado is EstadoProyecto.Finalizado or EstadoProyecto.Cancelado;
    }
}
