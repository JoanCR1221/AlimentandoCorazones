namespace SIGAC.Application.DTOs
{
    // Cifras de las tarjetas de resumen de cada módulo. Igual que
    // ResumenRegistrosDto, son el panorama general del módulo y no dependen de
    // los filtros de la pantalla. "Este mes" y "mes anterior" son meses
    // calendario.

    // Donaciones: cuántas se recibieron, cuántos artículos (unidades) entraron en
    // las donaciones en especie y cuánto dinero, separado por moneda porque no se
    // pueden sumar colones con dólares.
    public sealed record ResumenDonacionesDto(
        int DonacionesEsteMes,
        int DonacionesMesAnterior,
        int ArticulosDonadosEsteMes,
        int ArticulosDonadosMesAnterior,
        IReadOnlyList<MontoPorMonedaDto> DineroEsteMes);

    // Inventario: tamaño del catálogo y unidades que ingresaron (entradas no
    // anuladas). El stock bajo ya lo cuenta IInventarioService.
    public sealed record ResumenInventarioDto(
        int ArticulosEnCatalogo,
        int UnidadesIngresadasEsteMes,
        int UnidadesIngresadasMesAnterior);

    // Asistencia al comedor: comidas servidas (una por beneficiario y tiempo de
    // comida) y personas distintas atendidas en el mes.
    public sealed record ResumenAsistenciaDto(
        int ComidasHoy,
        int ComidasEsteMes,
        int ComidasMesAnterior,
        int PersonasAtendidasEsteMes);

    // Gastos operativos: solo los activos (los anulados no cuentan como gasto).
    public sealed record ResumenGastosDto(
        int GastosEsteMes,
        int GastosMesAnterior,
        IReadOnlyList<MontoPorMonedaDto> TotalEsteMes);

    // Proyectos comunitarios por estado.
    public sealed record ResumenProyectosDto(
        int Planificados,
        int EnCurso,
        int Finalizados,
        int Cancelados);
}
