using SIGAC.Domain.Entities;
using SIGAC.Application.DTOs.Asistencia;
using SIGAC.Application.DTOs.Reportes;

namespace SIGAC.Application.Interfaces
{
    public interface IAsistenciaRepository
    {
        Task AgregarAsync(AsistenciaComedor asistencia);
        Task<bool> ExisteAsistenciaAsync(int beneficiarioId, DateTime fecha, string tiempoComida);
        Task<IEnumerable<AsistenciaComedor>> ObtenerAsistenciasDiariasAsync(DateTime fecha);
        Task<IEnumerable<AsistenciaComedor>> ObtenerHistorialAsync(FiltrosAsistenciaDto filtros);

        // Para el reporte de beneficiarios atendidos: filtra por categoría (rango
        // de fecha de nacimiento, igual que ListadoBeneficiarios) y por rango de
        // fechas; la agrupación por beneficiario y tiempo de comida la hace
        // ReportesService, mismo criterio que ObtenerHistorialAsync.
        Task<IEnumerable<AsistenciaComedor>> ObtenerParaReporteBeneficiariosAsync(FiltrosReporteBeneficiariosDto filtros);
    }
}