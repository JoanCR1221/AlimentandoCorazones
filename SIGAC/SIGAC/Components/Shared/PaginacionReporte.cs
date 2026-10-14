namespace SIGAC.Components.Shared
{
    // Cuántas filas de un reporte se dibujan a la vez. El reporte sí se genera
    // completo (el PDF y el Excel tienen que traer todo), pero pintar miles de
    // filas en la pantalla hacía que la página se pegara: cada fila es un nodo del
    // DOM que Blazor Server tiene que armar y mandar por el circuito. La grilla
    // pagina en memoria sobre las filas ya generadas, sin volver a consultar.
    public static class PaginacionReporte
    {
        public const int TamanoPredeterminado = 20;

        public const int TamanoMaximo = 100;

        public static readonly int[] Opciones = { TamanoPredeterminado, 50, TamanoMaximo };
    }
}
