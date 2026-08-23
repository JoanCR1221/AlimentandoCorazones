namespace SIGAC.Domain.Entities
{
    public class Articulo
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string Categoria { get; set; } = string.Empty;
        public string UnidadMedida { get; set; } = string.Empty;
        public int StockActual { get; set; }
        public int StockMinimo { get; set; } = 5;
    }
}