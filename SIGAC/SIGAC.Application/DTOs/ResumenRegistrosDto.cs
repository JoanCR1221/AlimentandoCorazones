namespace SIGAC.Application.DTOs
{
    // Conteos para las tarjetas de resumen que encabezan un listado (por ejemplo,
    // Beneficiarios o Donantes). No depende de los filtros de la grilla: es el
    // panorama general del módulo, no el de la búsqueda en curso.
    //
    // "Este mes" y "mes anterior" son meses calendario (del día 1 al último), y no
    // "los últimos 30 días": así la comparación entre ambos es entre periodos del
    // mismo tipo y el número coincide con lo que la persona espera al mirar el
    // calendario.
    public sealed record ResumenRegistrosDto(
        int Activos,
        int Inactivos,
        int NuevosEsteMes,
        int NuevosMesAnterior)
    {
        public static ResumenRegistrosDto Vacio { get; } = new(0, 0, 0, 0);
    }
}
