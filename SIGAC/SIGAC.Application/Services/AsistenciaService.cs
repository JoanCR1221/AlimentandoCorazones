using SIGAC.Application.DTOs.Asistencia;
using SIGAC.Application.Exceptions;
using SIGAC.Application.Interfaces;
using SIGAC.Domain.Entities;

namespace SIGAC.Application.Services
{
    public class AsistenciaService : IAsistenciaService
    {
        private static readonly string[] TiemposComidaValidos = { "Desayuno", "Almuerzo", "Merienda" };

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

                if (dto.Fecha.Date > DateTime.Today)
                    throw new ValidationException("La fecha de asistencia no puede ser futura.");

                if (string.IsNullOrWhiteSpace(dto.TiempoComida) ||
                    !TiemposComidaValidos.Contains(dto.TiempoComida))
                    throw new ValidationException("El tiempo de comida debe ser Desayuno, Almuerzo o Merienda.");

                var beneficiario = await _beneficiariosRepository.ObtenerPorIdAsync(dto.BeneficiarioId)
                    ?? throw new NotFoundException("El beneficiario no existe.");

                if (!beneficiario.Estado)
                    throw new ValidationException("El beneficiario no está activo.");

                if (await _asistenciaRepository.ExisteAsistenciaAsync(dto.BeneficiarioId, dto.Fecha, dto.TiempoComida))
                    throw new DuplicateException("Ya existe asistencia registrada para ese beneficiario, fecha y tiempo de comida.");

                var asistenciasDelDia = await _asistenciaRepository.ObtenerAsistenciasDiariasAsync(dto.Fecha);
                var registrosBeneficiario = asistenciasDelDia.Count(a => a.BeneficiarioId == dto.BeneficiarioId);

                if (registrosBeneficiario >= 3)
                    throw new ValidationException("El beneficiario ya alcanzó el máximo de 3 registros para este día.");

                var asistencia = new AsistenciaComedor
                {
                    BeneficiarioId = dto.BeneficiarioId,
                    Fecha = dto.Fecha,
                    TiempoComida = dto.TiempoComida
                };

                await _asistenciaRepository.AgregarAsync(asistencia);
            }
            catch (Exception ex) when (ex is not ValidationException and not NotFoundException and not DuplicateException)
            {
                throw new Exception("Error al registrar la asistencia.", ex);
            }
        }

        public async Task<HistorialAsistenciaResultadoDto> ObtenerHistorialAsistenciaAsync(FiltrosAsistenciaDto filtros)
        {
            try
            {
                var asistencias = await _asistenciaRepository.ObtenerHistorialAsync(filtros);

                var registros = asistencias
                    .OrderByDescending(a => a.Fecha)
                    .Select(a => new HistorialAsistenciaDto
                    {
                        Id = a.Id,
                        NombreBeneficiario = a.Beneficiario?.Nombre ?? string.Empty,
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