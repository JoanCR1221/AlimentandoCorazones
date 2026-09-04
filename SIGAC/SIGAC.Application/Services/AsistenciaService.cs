using SIGAC.Application.DTOs.Asistencia;
using SIGAC.Application.Exceptions;
using SIGAC.Application.Interfaces;
using SIGAC.Domain;
using SIGAC.Domain.Entities;

namespace SIGAC.Application.Services
{
    public class AsistenciaService : IAsistenciaService
    {
        private readonly IAsistenciaRepository _asistenciaRepository;
        private readonly IBeneficiariosRepository _beneficiariosRepository;

        public AsistenciaService(
            IAsistenciaRepository asistenciaRepository,
            IBeneficiariosRepository beneficiariosRepository)
        {
            _asistenciaRepository = asistenciaRepository;
            _beneficiariosRepository = beneficiariosRepository;
        }

        public async Task RegistrarAsistenciaAsync(AsistenciaCrearDto dto)
        {
            try
            {
                if (dto.Fecha == default)
                    throw new ValidationException("La fecha de asistencia es obligatoria.");

                // Se compara sin hora y contra una sola fuente de "hoy", la misma que
                // usa el calendario de la pantalla: si no, una fecha elegida al
                // mediodía podría verse como futura frente a una medianoche.
                var hoy = ReglasAsistencia.Hoy;
                var fecha = dto.Fecha.Date;

                if (fecha > hoy)
                    throw new ValidationException("La fecha de asistencia no puede ser futura.");

                // La pantalla ya acota el calendario, pero la regla tiene que estar
                // acá: cualquier otro camino de entrada se saltearía la UI.
                var fechaMinima = ReglasAsistencia.FechaMinimaRegistro(hoy);

                if (fecha < fechaMinima)
                    throw new ValidationException(
                        $"Solo se pueden registrar asistencias de los últimos {ReglasAsistencia.MaximoDiasHaciaAtras} días. " +
                        $"La fecha más antigua permitida es {fechaMinima:dd/MM/yyyy}.");

                if (!TiemposComida.EsValido(dto.TiempoComida))
                    throw new ValidationException(
                        $"El tiempo de comida debe ser uno de: {string.Join(", ", TiemposComida.Todos)}.");

                var beneficiario = await _beneficiariosRepository.ObtenerPorIdAsync(dto.BeneficiarioId)
                    ?? throw new NotFoundException("El beneficiario no existe.");

                if (!beneficiario.Estado)
                    throw new ValidationException("El beneficiario no está activo.");

                if (await _asistenciaRepository.ExisteAsistenciaAsync(dto.BeneficiarioId, fecha, dto.TiempoComida))
                    throw new DuplicateException("Ya existe asistencia registrada para ese beneficiario, fecha y tiempo de comida.");

                var asistenciasDelDia = await _asistenciaRepository.ObtenerAsistenciasDiariasAsync(fecha);
                var registrosBeneficiario = asistenciasDelDia.Count(a => a.BeneficiarioId == dto.BeneficiarioId);

                if (registrosBeneficiario >= ReglasAsistencia.MaximoRegistrosPorDia)
                    throw new ValidationException(
                        $"El beneficiario ya alcanzó el máximo de {ReglasAsistencia.MaximoRegistrosPorDia} registros para este día.");

                var asistencia = new AsistenciaComedor
                {
                    BeneficiarioId = dto.BeneficiarioId,
                    Fecha = fecha,
                    TiempoComida = dto.TiempoComida
                };

                await _asistenciaRepository.AgregarAsync(asistencia);
            }
            catch (Exception ex) when (ex is not ValidationException and not NotFoundException and not DuplicateException)
            {
                throw new Exception("Error al registrar la asistencia.", ex);
            }
        }

        public async Task<bool> ExisteAsistenciaAsync(int beneficiarioId, DateTime fecha, string tiempoComida)
        {
            try
            {
                // Con los datos incompletos no hay nada que verificar todavía: la
                // pantalla llama a esto mientras el formulario se está llenando.
                if (beneficiarioId <= 0 || fecha == default || !TiemposComida.EsValido(tiempoComida))
                    return false;

                return await _asistenciaRepository.ExisteAsistenciaAsync(beneficiarioId, fecha, tiempoComida);
            }
            catch (Exception ex)
            {
                throw new Exception("Error al verificar la asistencia.", ex);
            }
        }

        public async Task<HistorialAsistenciaResultadoDto> ObtenerHistorialAsistenciaAsync(FiltrosAsistenciaDto filtros)
        {
            try
            {
                var asistencias = await _asistenciaRepository.ObtenerHistorialAsync(filtros);

                // El nombre sale de la navegación que el repositorio ya trajo con
                // Include, en el mismo viaje a la base: antes era una consulta extra
                // por cada beneficiario distinto del período.
                var registros = asistencias
                    .OrderByDescending(a => a.Fecha)
                    .Select(a => new HistorialAsistenciaDto
                    {
                        Id = a.Id,
                        NombreBeneficiario = a.Beneficiario?.NombreCompleto ?? string.Empty,
                        Fecha = a.Fecha,
                        TiempoComida = a.TiempoComida
                    })
                    .ToList();

                var totalesPorBeneficiario = registros
                    .GroupBy(r => r.NombreBeneficiario)
                    .ToDictionary(g => g.Key, g => g.Count());

                var totalesPorTiempoComida = registros
                    .GroupBy(r => r.TiempoComida)
                    .ToDictionary(g => g.Key, g => g.Count());

                return new HistorialAsistenciaResultadoDto
                {
                    Registros = registros,
                    TotalesPorBeneficiario = totalesPorBeneficiario,
                    TotalesPorTiempoComida = totalesPorTiempoComida
                };
            }
            catch (Exception ex)
            {
                throw new Exception("Error al consultar el historial de asistencia.", ex);
            }
        }
    }
}