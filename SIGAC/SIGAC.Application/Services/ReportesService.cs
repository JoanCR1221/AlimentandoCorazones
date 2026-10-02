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
        // Meses hacia atrás que cubre el panorama gráfico (incluye el mes actual).
        // Fijo por ahora: no hay pantalla que lo filtre, a diferencia del reporte
        // exportable de arriba.
        private const int MesesPanoramaPorDefecto = 12;

        // Cuántos proveedores entran en el panorama de gastos. Una lista completa
        // no cabe en una gráfica de barras legible.
        private const int TopProveedoresPorDefecto = 5;

        private readonly IAsistenciaRepository _asistenciaRepository;
        private readonly IBeneficiariosRepository _beneficiariosRepository;
        private readonly IGastosRepository _gastosRepository;

        public ReportesService(
            IAsistenciaRepository asistenciaRepository,
            IBeneficiariosRepository beneficiariosRepository,
            IGastosRepository gastosRepository)
        {
            _asistenciaRepository = asistenciaRepository;
            _beneficiariosRepository = beneficiariosRepository;
            _gastosRepository = gastosRepository;
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

        public async Task<PanoramaBeneficiariosDto> ObtenerPanoramaBeneficiariosAsync()
        {
            try
            {
                var porCategoria = await _beneficiariosRepository.ObtenerConteoPorCategoriaAsync();
                var resumenRegistros = await _beneficiariosRepository.ObtenerResumenAsync();
                var altasPorMes = await _beneficiariosRepository.ObtenerAltasPorMesAsync(MesesPanoramaPorDefecto);
                var comidas = await _asistenciaRepository.ObtenerComidasPorTiempoYMesAsync(MesesPanoramaPorDefecto);
                var personasPorMes = await _asistenciaRepository.ObtenerPersonasAtendidasPorMesAsync(MesesPanoramaPorDefecto);

                return new PanoramaBeneficiariosDto
                {
                    BeneficiariosPorCategoria = porCategoria,
                    Activos = resumenRegistros.Activos,
                    Inactivos = resumenRegistros.Inactivos,
                    AltasPorMes = altasPorMes,
                    Desayunos = comidas.Where(c => c.TiempoComida == TiemposComida.Desayuno).Sum(c => c.Cantidad),
                    Almuerzos = comidas.Where(c => c.TiempoComida == TiemposComida.Almuerzo).Sum(c => c.Cantidad),
                    Meriendas = comidas.Where(c => c.TiempoComida == TiemposComida.Merienda).Sum(c => c.Cantidad),
                    // Total del mes sin importar el tiempo de comida: se colapsa acá
                    // porque la consulta viene agrupada también por TiempoComida.
                    ComidasPorMes = comidas
                        .GroupBy(c => new { c.Anio, c.Mes })
                        .Select(g => new ConteoPorMesDto(g.Key.Anio, g.Key.Mes, g.Sum(c => c.Cantidad)))
                        .OrderBy(c => c.Anio).ThenBy(c => c.Mes)
                        .ToList(),
                    PersonasAtendidasPorMes = personasPorMes
                };
            }
            catch (Exception ex)
            {
                throw new Exception("Error al generar el panorama de beneficiarios.", ex);
            }
        }

        public async Task<PanoramaGastosDto> ObtenerPanoramaGastosAsync()
        {
            try
            {
                var montoPorTipo = await _gastosRepository.ObtenerMontoPorTipoAsync(MesesPanoramaPorDefecto);
                var montoPorFormaPago = await _gastosRepository.ObtenerMontoPorFormaPagoAsync(MesesPanoramaPorDefecto);
                var montoPorMes = await _gastosRepository.ObtenerMontoPorMesAsync(MesesPanoramaPorDefecto);
                var cantidadPorMes = await _gastosRepository.ObtenerCantidadPorMesAsync(MesesPanoramaPorDefecto);
                var topProveedores = await _gastosRepository.ObtenerTopProveedoresAsync(MesesPanoramaPorDefecto, TopProveedoresPorDefecto);
                var (activos, anulados) = await _gastosRepository.ObtenerConteoPorEstadoAsync(MesesPanoramaPorDefecto);

                return new PanoramaGastosDto
                {
                    MontoPorTipo = montoPorTipo,
                    MontoPorFormaPago = montoPorFormaPago,
                    MontoPorMes = montoPorMes,
                    CantidadPorMes = cantidadPorMes,
                    TopProveedores = topProveedores,
                    Activos = activos,
                    Anulados = anulados
                };
            }
            catch (Exception ex)
            {
                throw new Exception("Error al generar el panorama de gastos operativos.", ex);
            }
        }
    }
}
