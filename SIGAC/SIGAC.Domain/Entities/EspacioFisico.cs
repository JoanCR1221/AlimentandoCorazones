namespace SIGAC.Domain.Entities
{
    // Sector del local que se puede alquilar: Área de juego, Sala de servicio,
    // Baños, Uso de cocina solo para servir. Es un catálogo en tabla y no un enum
    // porque la propuesta original tenía las dos cosas (EspacioFisico sin uso y un
    // Sector suelto en el alquiler) describiendo lo mismo; quedó solo la tabla, que
    // además permite sumar un sector o desactivar uno en reparación sin migración.
    public class EspacioFisico
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;

        // Personas que caben. Opcional: "Baños" o "Cocina solo para servir" no
        // tienen una capacidad de personas que tenga sentido. AlquileresService la
        // usa para validar AlquilerEspacio.CantidadPersonas contra los sectores
        // elegidos que sí la declaran.
        public int? Capacidad { get; set; }

        // Baja lógica: un sector inactivo no se ofrece en alquileres nuevos, pero los
        // alquileres viejos lo siguen mostrando.
        public bool Estado { get; set; } = true;
    }
}
