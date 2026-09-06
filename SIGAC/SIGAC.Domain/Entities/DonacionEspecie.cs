namespace SIGAC.Domain.Entities
{
    // Donación recibida en artículos. Es la cabecera del documento: el donante, la
    // fecha y el detalle de lo que trajo. Una sola entrega puede incluir varios
    // artículos distintos, por eso el desglose vive en DetalleDonacionEspecie y no
    // en columnas de esta entidad.
    //
    // Esta entidad registra lo que se recibió; el ingreso al stock lo sigue
    // haciendo Inventario mediante EntradaInventario (origen "Donacion"), que el
    // servicio de Donaciones generará delegando en IInventarioService.
    public class DonacionEspecie
    {
        public int Id { get; set; }

        public int DonanteId { get; set; }
        public Donante? Donante { get; set; }

        public DateTime Fecha { get; set; }
        public string? Observaciones { get; set; }

        // Inicializada y no nullable, a diferencia de las navegaciones "hacia el
        // padre": recorrer los detalles de una donación recién creada no debe
        // obligar a comprobar null antes de agregar la primera línea.
        public List<DetalleDonacionEspecie> Detalles { get; set; } = new();
    }
}
