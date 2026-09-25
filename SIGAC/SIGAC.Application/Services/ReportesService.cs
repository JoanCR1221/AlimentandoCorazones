using SIGAC.Application.DTOs.Reportes;
using SIGAC.Application.Interfaces;
using SIGAC.Domain;

namespace SIGAC.Application.Services
{
    // Reportes institucionales consolidados (PBI 1940 y los que sigan). La
    // agrupación se hace en memoria sobre lo que ya trae el repositorio
    // filtrado, mismo criterio que AsistenciaService.ObtenerHistorialAsistenciaAsync:
    // el volumen de un período de asistencia no justifica un GROUP BY en SQL.
    public class ReportesService : IReportesService
    {
        private readonly IAsistenciaRepository _asistenciaRepository;

        public ReportesService(IAsistenciaRepository asistenciaRepository)
        {
            _asistenciaRepository = asistenciaRepository;
        }

        public async Task<ReporteBeneficiariosResultadoDto> GenerarReporteBeneficiariosAsync(FiltrosReporteBeneficiariosDto filtros)
        {
            try
            {
                var asistencias = await _asistenciaRepository.ObtenerParaReporteBeneficiariosAsync(filtros);

                var filas = asistencias
                    .Where(a => a.Beneficiario is not null)
                    .GroupBy(a => a.Beneficiario!)
                    .Select(g => new ReporteBeneficiariosDto
                    {
                        BeneficiarioId = g.Key.Id,
                        NombreCompleto = g.Key.NombreCompleto,
                        Categoria = CategoriasBeneficiario.DerivarDesdeFechaNacimiento(g.Key.FechaNacimiento),
                        Desayunos = g.Count(a => a.TiempoComida == TiemposComida.Desayuno),
                        Almuerzos = g.Count(a => a.TiempoComida == TiemposComida.Almuerzo),
                        Meriendas = g.Count(a => a.TiempoComida == TiemposComida.Merienda),
                        TotalAsistencias = g.Count()
                    })
                    .OrderBy(f => f.NombreCompleto)
                    .ToList();

                return new ReporteBeneficiariosResultadoDto
                {
                    Filas = filas,
                    TotalGeneral = filas.Sum(f => f.TotalAsistencias)
                };
            }
            catch (Exception ex)
            {
                throw new Exception("Error al generar el reporte de beneficiarios atendidos.", ex);
            }
        }
    }
}
