using SIGAC.Application.DTOs.Alquileres;
using SIGAC.Application.DTOs.Donaciones;
using SIGAC.Application.DTOs.Inventario;
using SIGAC.Application.DTOs.Reportes;
using SIGAC.Application.Interfaces;
using SIGAC.Domain;
using SIGAC.Domain.Entities;

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

        // Lo mismo para los donantes del panorama de donaciones y los artículos del de
        // inventario.
        private const int TopDonantesPorDefecto = 5;
        private const int TopArticulosPorDefecto = 5;
        private const int TopProyectosPorDefecto = 5;

        private readonly IAsistenciaRepository _asistenciaRepository;
        private readonly IBeneficiariosRepository _beneficiariosRepository;
        private readonly IGastosRepository _gastosRepository;
        private readonly IDonacionesService _donacionesService;
        private readonly IDonacionesRepository _donacionesRepository;
        private readonly IDonantesRepository _donantesRepository;
        private readonly IAlquileresService _alquileresService;
        private readonly IAlquileresRepository _alquileresRepository;
        private readonly IInventarioService _inventarioService;
        private readonly IInventarioRepository _inventarioRepository;
        private readonly IProyectosRepository _proyectosRepository;

        // Donaciones, Alquileres e Inventario entran cada uno por dos lados: el reporte
        // exportable reutiliza el historial del servicio (que ya une, filtra y
        // calcula los totales por moneda), y el panorama necesita agregados por mes
        // que ese historial no da, así que consulta el repositorio.
        public ReportesService(
            IAsistenciaRepository asistenciaRepository,
            IBeneficiariosRepository beneficiariosRepository,
            IGastosRepository gastosRepository,
            IDonacionesService donacionesService,
            IDonacionesRepository donacionesRepository,
            IDonantesRepository donantesRepository,
            IAlquileresService alquileresService,
            IAlquileresRepository alquileresRepository,
            IInventarioService inventarioService,
            IInventarioRepository inventarioRepository,
            IProyectosRepository proyectosRepository)
        {
            _asistenciaRepository = asistenciaRepository;
            _beneficiariosRepository = beneficiariosRepository;
            _gastosRepository = gastosRepository;
            _donacionesService = donacionesService;
            _donacionesRepository = donacionesRepository;
            _donantesRepository = donantesRepository;
            _alquileresService = alquileresService;
            _alquileresRepository = alquileresRepository;
            _inventarioService = inventarioService;
            _inventarioRepository = inventarioRepository;
            _proyectosRepository = proyectosRepository;
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

        public async Task<PanoramaDonacionesDto> ObtenerPanoramaDonacionesAsync()
        {
            try
            {
                var porMes = await _donacionesRepository.ObtenerCantidadPorMesAsync(MesesPanoramaPorDefecto);
                var dineroPorMes = await _donacionesRepository.ObtenerDineroEnColonesPorMesAsync(MesesPanoramaPorDefecto);
                var topDonantes = await _donacionesRepository.ObtenerTopDonantesAsync(MesesPanoramaPorDefecto, TopDonantesPorDefecto);
                var donantes = await _donantesRepository.ObtenerResumenAsync();

                return new PanoramaDonacionesDto
                {
                    // Los totales por clase salen de la misma consulta mensual, sin
                    // repetirla (igual que los tiempos de comida del panorama de
                    // beneficiarios).
                    CantidadDinero = porMes.Where(c => c.TipoDonacion == DonacionesService.TipoDonacionDinero).Sum(c => c.Cantidad),
                    CantidadEspecie = porMes.Where(c => c.TipoDonacion == DonacionesService.TipoDonacionEspecie).Sum(c => c.Cantidad),
                    DonacionesPorMes = porMes,
                    DineroEnColonesPorMes = dineroPorMes,
                    TopDonantes = topDonantes,
                    DonantesActivos = donantes.Activos,
                    DonantesInactivos = donantes.Inactivos
                };
            }
            catch (Exception ex)
            {
                throw new Exception("Error al generar el panorama de donaciones.", ex);
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

        public async Task<ReporteDonacionesResultadoDto> GenerarReporteDonacionesAsync(FiltrosReporteDonacionesDto filtros)
        {
            try
            {
                // El historial de donaciones ya une dinero y especie, filtra por tipo
                // y fechas y calcula el total por moneda: el reporte es esa misma
                // consulta con las columnas que se exportan, así que no se repite.
                var historial = await _donacionesService.ObtenerHistorialDonacionesAsync(new FiltrosHistorialDonacionDto
                {
                    // Vacío también es "ambas": el historial solo entiende null, y con
                    // una cadena vacía no incluiría ninguna de las dos clases.
                    TipoDonacion = string.IsNullOrWhiteSpace(filtros.TipoDonacion) ? null : filtros.TipoDonacion,
                    FechaDesde = filtros.FechaDesde,
                    FechaHasta = filtros.FechaHasta
                });

                var filas = historial.Donaciones
                    .Select(d => new ReporteDonacionesDto
                    {
                        Fecha = d.Fecha,
                        TipoDonacion = d.TipoDonacion,
                        Donante = d.NombreDonante,
                        Monto = d.Monto,
                        Moneda = d.Moneda,
                        Descripcion = d.Descripcion
                    })
                    .ToList();

                return new ReporteDonacionesResultadoDto
                {
                    Filas = filas,
                    CantidadDinero = filas.Count(f => f.TipoDonacion == DonacionesService.TipoDonacionDinero),
                    CantidadEspecie = filas.Count(f => f.TipoDonacion == DonacionesService.TipoDonacionEspecie),
                    // En el orden del catálogo de monedas (colones, dólares, euros):
                    // el historial las trae en el orden en que aparecen, y en el
                    // reporte cambiaría de un período a otro según qué donación sea
                    // la más reciente.
                    TotalesPorMoneda = historial.TotalesPorMoneda
                        .OrderBy(t => TiposMoneda.Posicion(t.Moneda))
                        .ToList()
                };
            }
            catch (Exception ex)
            {
                throw new Exception("Error al generar el reporte de donaciones.", ex);
            }
        }

        public async Task<ReporteAlquileresResultadoDto> GenerarReporteAlquileresAsync(FiltrosReporteAlquileresDto filtros)
        {
            try
            {
                // El historial de alquileres es el mismo que alimenta el calendario:
                // ya filtra por fechas, sector y estado, trae los sectores resueltos
                // a texto y calcula el ingreso por moneda dejando fuera a los
                // cancelados. El reporte lo reutiliza en vez de repetir la consulta.
                var historial = await _alquileresService.ObtenerHistorialAlquileresAsync(new FiltrosHistorialAlquilerDto
                {
                    FechaDesde = filtros.FechaDesde,
                    FechaHasta = filtros.FechaHasta,
                    EspacioId = filtros.EspacioId,
                    Estado = filtros.Estado
                });

                var filas = historial.Alquileres
                    .Select(a => new ReporteAlquileresDto
                    {
                        Fecha = a.Fecha,
                        Horario = ReglasAlquiler.FormatearFranja(a.HoraInicio, a.HoraFin),
                        Arrendatario = a.Arrendatario,
                        Sectores = string.Join(", ", a.Espacios),
                        Personas = a.CantidadPersonas,
                        Monto = a.Monto,
                        Moneda = a.Moneda,
                        Estado = a.Estado.ToString()
                    })
                    .ToList();

                var reservados = historial.Alquileres.Where(a => a.Estado == EstadoAlquiler.Reservado).ToList();

                // Los cancelados no ocupan el local: no suman horas, igual que no
                // suman ingresos.
                return new ReporteAlquileresResultadoDto
                {
                    Filas = filas,
                    CantidadReservados = reservados.Count,
                    CantidadCancelados = historial.Alquileres.Count - reservados.Count,
                    HorasAlquiladas = HorasDe(reservados.Select(a => (a.HoraInicio, a.HoraFin))),
                    IngresosPorMoneda = historial.TotalesPorMoneda
                        .OrderBy(t => TiposMoneda.Posicion(t.Moneda))
                        .ToList()
                };
            }
            catch (Exception ex)
            {
                throw new Exception("Error al generar el reporte de alquileres.", ex);
            }
        }

        public async Task<ReporteMovimientosResultadoDto> GenerarReporteMovimientosAsync(FiltrosReporteMovimientosDto filtros)
        {
            try
            {
                // El historial de movimientos ya une entradas y salidas, filtra por
                // artículo, tipo y fechas, y suma las unidades de cada lado: el
                // reporte es esa misma consulta con las columnas que se exportan.
                var historial = await _inventarioService.ObtenerHistorialMovimientosAsync(new FiltrosMovimientoDto
                {
                    ArticuloId = filtros.ArticuloId,
                    // Vacío también es "todos": el historial solo entiende null, y con
                    // una cadena vacía no incluiría ni entradas ni salidas.
                    TipoMovimiento = string.IsNullOrWhiteSpace(filtros.TipoMovimiento) ? null : filtros.TipoMovimiento,
                    Desde = filtros.FechaDesde,
                    Hasta = filtros.FechaHasta
                });

                var filas = historial.Movimientos
                    .Select(m => new ReporteMovimientosDto
                    {
                        Fecha = m.Fecha,
                        Articulo = m.Articulo,
                        TipoMovimiento = EtiquetaDeMovimiento(m.TipoMovimiento),
                        Cantidad = m.Cantidad,
                        OrigenODestino = EtiquetaDeMovimiento(m.OrigenODestino ?? string.Empty)
                    })
                    .ToList();

                return new ReporteMovimientosResultadoDto
                {
                    Filas = filas,
                    TotalEntradas = historial.TotalEntradas,
                    TotalSalidas = historial.TotalSalidas
                };
            }
            catch (Exception ex)
            {
                throw new Exception("Error al generar el reporte de movimientos de inventario.", ex);
            }
        }

        public async Task<ReporteProyectosResultadoDto> GenerarReporteProyectosAsync(FiltrosReporteProyectosDto filtros)
        {
            try
            {
                var proyectos = await _proyectosRepository.ObtenerParaReporteAsync(filtros);

                var filas = proyectos
                    .Select(p => new ReporteProyectosDto
                    {
                        Nombre = p.Nombre,
                        Estado = EtiquetaDeEstado(p.Estado),
                        FechaInicio = p.FechaInicio,
                        FechaEstimadaFin = p.FechaEstimadaFin,
                        FechaFinalizacion = p.FechaFinalizacionReal,
                        TotalParticipantes = p.Participantes.Count,
                        Beneficiarios = p.Participantes.Count(x => x.EsBeneficiario),
                        Externos = p.Participantes.Count(x => !x.EsBeneficiario)
                    })
                    .ToList();

                return new ReporteProyectosResultadoDto
                {
                    Filas = filas,
                    Planificados = proyectos.Count(p => p.Estado == EstadoProyecto.Planificado),
                    EnCurso = proyectos.Count(p => p.Estado == EstadoProyecto.EnCurso),
                    Finalizados = proyectos.Count(p => p.Estado == EstadoProyecto.Finalizado),
                    Cancelados = proyectos.Count(p => p.Estado == EstadoProyecto.Cancelado),
                    TotalParticipantes = filas.Sum(f => f.TotalParticipantes),
                    TotalBeneficiarios = filas.Sum(f => f.Beneficiarios),
                    TotalExternos = filas.Sum(f => f.Externos)
                };
            }
            catch (Exception ex)
            {
                throw new Exception("Error al generar el reporte de proyectos comunitarios.", ex);
            }
        }

        public async Task<PanoramaProyectosDto> ObtenerPanoramaProyectosAsync()
        {
            try
            {
                // Todos los proyectos: son pocos y de larga duración (ver
                // PanoramaProyectosDto). Solo las dos series mensuales se acotan a la
                // ventana, y se hace acá y no en SQL para reutilizar la misma consulta
                // del reporte.
                var proyectos = await _proyectosRepository.ObtenerParaReporteAsync(new FiltrosReporteProyectosDto());
                var participantes = proyectos.SelectMany(p => p.Participantes).ToList();

                var inicioVentana = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1).AddMonths(-(MesesPanoramaPorDefecto - 1));

                return new PanoramaProyectosDto
                {
                    TotalProyectos = proyectos.Count,
                    Planificados = proyectos.Count(p => p.Estado == EstadoProyecto.Planificado),
                    EnCurso = proyectos.Count(p => p.Estado == EstadoProyecto.EnCurso),
                    Finalizados = proyectos.Count(p => p.Estado == EstadoProyecto.Finalizado),
                    Cancelados = proyectos.Count(p => p.Estado == EstadoProyecto.Cancelado),
                    ParticipantesBeneficiarios = participantes.Count(p => p.EsBeneficiario),
                    ParticipantesExternos = participantes.Count(p => !p.EsBeneficiario),
                    ProyectosIniciadosPorMes = ContarPorMes(proyectos.Where(p => p.FechaInicio >= inicioVentana).Select(p => p.FechaInicio)),
                    ParticipantesRegistradosPorMes = ContarPorMes(participantes.Where(p => p.FechaRegistro >= inicioVentana).Select(p => p.FechaRegistro)),
                    ProyectosConMasParticipantes = proyectos
                        .Select(p => new ParticipantesPorProyectoDto(p.Nombre, p.Participantes.Count))
                        .Where(p => p.Participantes > 0)
                        .OrderByDescending(p => p.Participantes).ThenBy(p => p.Proyecto, StringComparer.CurrentCultureIgnoreCase)
                        .Take(TopProyectosPorDefecto)
                        .ToList()
                };
            }
            catch (Exception ex)
            {
                throw new Exception("Error al generar el panorama de proyectos comunitarios.", ex);
            }
        }

        private static List<ConteoPorMesDto> ContarPorMes(IEnumerable<DateTime> fechas) => fechas
            .GroupBy(f => (f.Year, f.Month))
            .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month)
            .Select(g => new ConteoPorMesDto(g.Key.Year, g.Key.Month, g.Count()))
            .ToList();

        // Mismo texto que el listado de proyectos: "En curso" con espacio.
        private static string EtiquetaDeEstado(EstadoProyecto estado) => estado switch
        {
            EstadoProyecto.EnCurso => "En curso",
            _ => estado.ToString()
        };

        public async Task<PanoramaInventarioDto> ObtenerPanoramaInventarioAsync()
        {
            try
            {
                var entradas = await _inventarioRepository.ObtenerEntradasPorMesYOrigenAsync(MesesPanoramaPorDefecto);
                var salidas = await _inventarioRepository.ObtenerSalidasPorMesYTipoAsync(MesesPanoramaPorDefecto);
                var articulos = await _inventarioRepository.ObtenerArticulosConMasMovimientosAsync(MesesPanoramaPorDefecto, TopArticulosPorDefecto);

                return new PanoramaInventarioDto
                {
                    // Los totales y el desglose salen de las mismas consultas
                    // mensuales, sin repetirlas (igual que los tiempos de comida del
                    // panorama de beneficiarios).
                    UnidadesEntradas = entradas.Sum(e => e.Unidades),
                    UnidadesSalidas = salidas.Sum(s => s.Unidades),
                    EntradasPorMes = SumarPorMes(entradas),
                    SalidasPorMes = SumarPorMes(salidas),
                    EntradasPorCompra = entradas.Where(e => e.Tipo == OrigenesEntradaInventario.Compra).Sum(e => e.Unidades),
                    EntradasPorDonacion = entradas.Where(e => e.Tipo == OrigenesEntradaInventario.Donacion).Sum(e => e.Unidades),
                    SalidasPorDonacion = salidas.Where(s => s.Tipo == TiposSalidaInventario.Donacion).Sum(s => s.Unidades),
                    SalidasPorPrestamo = salidas.Where(s => s.Tipo == TiposSalidaInventario.Prestamo).Sum(s => s.Unidades),
                    ArticulosConMasMovimientos = articulos
                };
            }
            catch (Exception ex)
            {
                throw new Exception("Error al generar el panorama de inventario.", ex);
            }
        }

        // Colapsa el desglose por tipo en el total del mes.
        private static List<ConteoPorMesDto> SumarPorMes(IEnumerable<UnidadesPorMesYTipoDto> filas) => filas
            .GroupBy(f => (f.Anio, f.Mes))
            .OrderBy(g => g.Key.Anio).ThenBy(g => g.Key.Mes)
            .Select(g => new ConteoPorMesDto(g.Key.Anio, g.Key.Mes, g.Sum(f => f.Unidades)))
            .ToList();

        // Los valores guardados no llevan tilde ("Donacion", "Prestamo"); el reporte
        // los muestra bien escritos, igual que la pantalla del historial.
        private static string EtiquetaDeMovimiento(string valor) => valor switch
        {
            TiposSalidaInventario.Donacion => "Donación",
            TiposSalidaInventario.Prestamo => "Préstamo",
            _ => valor
        };

        public async Task<PanoramaAlquileresDto> ObtenerPanoramaAlquileresAsync()
        {
            try
            {
                var alquileres = await _alquileresRepository.ObtenerParaPanoramaAsync(MesesPanoramaPorDefecto);

                // Un cancelado no ocupa el local ni genera ingreso: solo cuenta en la
                // comparación de reservados contra cancelados y en su tendencia.
                var reservados = alquileres.Where(a => a.Estado == EstadoAlquiler.Reservado).ToList();
                var cancelados = alquileres.Where(a => a.Estado == EstadoAlquiler.Cancelado).ToList();
                var enColones = reservados.Where(a => a.Moneda == TiposMoneda.Colones).ToList();

                return new PanoramaAlquileresDto
                {
                    CantidadReservados = reservados.Count,
                    CantidadCancelados = cancelados.Count,
                    HorasAlquiladas = HorasDe(reservados.Select(a => (a.HoraInicio, a.HoraFin))),
                    IngresosEnColones = enColones.Sum(a => a.Monto),
                    ReservadosPorMes = ContarPorMes(reservados),
                    CanceladosPorMes = ContarPorMes(cancelados),
                    IngresosEnColonesPorMes = enColones
                        .GroupBy(a => (a.Fecha.Year, a.Fecha.Month))
                        .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month)
                        .Select(g => new MontoPorMesDto(g.Key.Year, g.Key.Month, g.Sum(a => a.Monto)))
                        .ToList(),
                    HorasPorMes = reservados
                        .GroupBy(a => (a.Fecha.Year, a.Fecha.Month))
                        .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month)
                        .Select(g => new HorasPorMesDto(g.Key.Year, g.Key.Month, HorasDe(g.Select(a => (a.HoraInicio, a.HoraFin)))))
                        .ToList(),
                    // Un alquiler de dos sectores cuenta en cada uno: es cuánto se usa
                    // cada sector, no cuántos alquileres hubo.
                    AlquileresPorSector = reservados
                        .SelectMany(a => a.Sectores)
                        .GroupBy(sector => sector)
                        .Select(g => new AlquileresPorSectorDto(g.Key, g.Count()))
                        .OrderByDescending(s => s.Cantidad).ThenBy(s => s.Sector, StringComparer.CurrentCultureIgnoreCase)
                        .ToList(),
                    ReservadosEntreSemana = reservados.Count(a => !ReglasAlquiler.EsFinDeSemana(a.Fecha)),
                    ReservadosFinDeSemana = reservados.Count(a => ReglasAlquiler.EsFinDeSemana(a.Fecha))
                };
            }
            catch (Exception ex)
            {
                throw new Exception("Error al generar el panorama de alquileres.", ex);
            }
        }

        private static List<ConteoPorMesDto> ContarPorMes(IEnumerable<AlquilerPanoramaDto> alquileres) => alquileres
            .GroupBy(a => (a.Fecha.Year, a.Fecha.Month))
            .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month)
            .Select(g => new ConteoPorMesDto(g.Key.Year, g.Key.Month, g.Count()))
            .ToList();

        // Suma de la duración de cada franja, en horas con dos decimales. Se suman
        // minutos enteros y se divide al final, para no acumular error de redondeo.
        private static decimal HorasDe(IEnumerable<(TimeSpan Inicio, TimeSpan Fin)> franjas) =>
            Math.Round((decimal)franjas.Sum(f => (f.Fin - f.Inicio).TotalMinutes) / 60m, 2);
    }
}
