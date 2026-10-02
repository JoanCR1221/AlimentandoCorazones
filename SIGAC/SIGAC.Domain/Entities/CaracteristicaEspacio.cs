namespace SIGAC.Domain.Entities
{
    // Servicio o recurso que se puede pedir para un alquiler: luz, agua, internet,
    // decoración adicional, mobiliario. Catálogo en tabla, igual que EspacioFisico,
    // para que la asociación sume o retire opciones sin tocar el código.
    //
    // Sin costo propio: lo que se cobra es el monto total acordado en el alquiler
    // (AlquilerEspacio.Monto). Acá solo se marca qué se pidió.
    public class CaracteristicaEspacio
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;

        // Baja lógica, mismo criterio que EspacioFisico.
        public bool Estado { get; set; } = true;
    }
}
