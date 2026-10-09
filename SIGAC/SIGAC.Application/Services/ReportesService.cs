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

                // Se agrupa por BeneficiarioId y no por el objeto Beneficiario: el
                // repositorio consulta con AsNoTracking, y ahí EF crea una instancia
                // distinta del beneficiario por cada asistencia, así que agrupar por
                // el objeto daba una fila por asistencia (y "Beneficiarios atendidos"
                // inflado) apenas alguien asistía más de una vez.
                var filas = asistencias
                    .Where(a => a.Beneficiario is not null)
                    .GroupBy(a => a.BeneficiarioId)
                    .Select(g =>
                    {
                        var beneficiario = g.First().Beneficiario!;

                        return new ReporteBeneficiariosDto
                        {
                            BeneficiarioId = g.Key,
                            NombreCompleto = beneficiario.NombreCompleto,
                            Categoria = CategoriasBeneficiario.DerivarDesdeFechaNacimiento(beneficiario.FechaNacimiento),
                            Desayunos = g.Count(a => a.TiempoComida == TiemposComida.Desayuno),
                            Almuerzos = g.Count(a => a.TiempoComida == TiemposComida.Almuerzo),
                            Meriendas = g.Count(a => a.TiempoComida == TiemposComida.Merienda),
                            TotalAsistencias = g.Count()
                        };
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

        public async Task<ReporteGastosDto> GenerarReporteGastosAsync(FiltrosReporteGastosDto filtros)
        {
            try
            {
                var gastos = await _gastosRepository.ObtenerParaReporteAsync(filtros.Mes, filtros.Anio, filtros.FormaPago);

                // Agrupado por tipo de gasto y descripción de cuenta, igual que el
                // encabezado del papel ("POR TIPO DE GASTO Y DESCRIPCION CUENTA").
                // Orden alfabético por tipo, y dentro de cada grupo por día: mismo
                // orden en el que aparecen en el reporte de la contadora.
                var grupos = gastos
                    .GroupBy(g => (TipoGasto: g.TipoGasto!.Nombre, g.DescripcionCuenta))
                    .OrderBy(g => g.Key.TipoGasto).ThenBy(g => g.Key.DescripcionCuenta)
                    .Select(g =>
                    {
                        var filas = g
                            .OrderBy(x => x.Fecha.Day)
                            .Select(x => new FilaReporteGastosDto(
                                x.Proveedor, x.NumeroFactura, x.Fecha.Day, x.MontoSinIva, x.Iva, x.NumeroCheque, x.CuentaContable))
                            .ToList();

                        return new GrupoReporteGastosDto
                        {
                            TipoGasto = g.Key.TipoGasto,
                            DescripcionCuenta = g.Key.DescripcionCuenta,
                            Filas = filas,
                            SubtotalMonto = filas.Sum(f => f.Monto),
                            SubtotalIva = filas.Sum(f => f.Iva)
                        };
                    })
                    .ToList();

                return new ReporteGastosDto
                {
                    Grupos = grupos,
                    GranTotalMonto = grupos.Sum(g => g.SubtotalMonto),
                    GranTotalIva = grupos.Sum(g => g.SubtotalIva)
                };
            }
            catch (Exception ex)
            {
                throw new Exception("Error al generar el reporte de gastos operativos.", ex);
            }
        }
    }
}
