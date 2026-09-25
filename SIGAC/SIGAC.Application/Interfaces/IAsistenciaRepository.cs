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

        // Comidas servidas por tiempo de comida y mes calendario, de los últimos
        // mesesHaciaAtras meses (incluye el mes actual). Un solo agregado: de acá
        // salen tanto el total por tiempo de comida como la tendencia mensual del
        // panorama gráfico.
        Task<IReadOnlyList<ConteoComidaMensualDto>> ObtenerComidasPorTiempoYMesAsync(int mesesHaciaAtras);

        // Personas distintas (no comidas) atendidas por mes calendario, de los
        // últimos mesesHaciaAtras meses. COUNT DISTINCT no se puede derivar del
        // agregado anterior, por eso es una consulta aparte.
        Task<IReadOnlyList<ConteoPorMesDto>> ObtenerPersonasAtendidasPorMesAsync(int mesesHaciaAtras);
    }
}