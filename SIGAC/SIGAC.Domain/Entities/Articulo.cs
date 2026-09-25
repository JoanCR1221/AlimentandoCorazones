namespace SIGAC.Domain.Entities
{
    public class Articulo
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;

        // Opcional: identifica al artículo para búsquedas rápidas ("P001"), pero no
        // todo artículo llega con uno asignado. Único cuando se define (ver el
        // índice filtrado UX_Articulos_Codigo en SigacDbContext).
        public string? Codigo { get; set; }

        public string Categoria { get; set; } = string.Empty;

        // Subcategoría del Equipo (ver EstadosArticulo): obligatorio cuando la
        // categoría es Equipo y NULL en cualquier otra (lo respalda el CHECK
        // CK_Articulos_Estado_Coherente). Junto con Nombre identifica al artículo.
        public string? Estado { get; set; }

        public string UnidadMedida { get; set; } = string.Empty;
        public int StockActual { get; set; }
        public int StockMinimo { get; set; } = 5;

        // Opcional: dónde está físicamente el artículo (bodega, estante). Texto
        // libre, no una clave foránea: SIGAC todavía no modela ubicaciones como
        // entidad propia.
        public string? Ubicacion { get; set; }

        // Cómo se nombra al artículo donde el Nombre solo no alcanza (historiales,
        // listas de préstamo, mensajes): dos sillas en distinto estado tienen el
        // mismo nombre y se confundirían.
        public string Etiqueta => EtiquetaDe(Nombre, Estado);

        public static string EtiquetaDe(string nombre, string? estado) =>
            string.IsNullOrEmpty(estado) ? nombre : $"{nombre} ({estado})";
    }
}