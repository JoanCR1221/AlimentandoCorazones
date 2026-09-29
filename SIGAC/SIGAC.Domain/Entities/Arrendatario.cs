namespace SIGAC.Domain.Entities
{
    // Persona o entidad que alquila un espacio de la asociación. Mismos datos
    // básicos que Donante, pero en una tabla aparte: alquilar y donar son roles
    // distintos, y compartir la tabla mezclaría a quien le paga a la asociación con
    // quien le regala, en listados y en reportes.
    public class Arrendatario
    {
        public int Id { get; set; }

        // Un solo campo de nombre, mismo criterio que Donante: puede ser una
        // empresa o un grupo organizado, y una razón social no se parte en nombres
        // y apellidos.
        public string Nombre { get; set; } = string.Empty;

        // Valor del catálogo cerrado TiposPersonaDonante ("Física" o "Jurídica").
        // Se reutiliza ese catálogo y no se crea otro: son exactamente los mismos
        // dos valores, y dos listas iguales terminarían divergiendo.
        public string TipoPersona { get; set; } = string.Empty;

        // Cédula física o jurídica, opcional. No estaba en la propuesta original: es
        // el único dato que permite la "restricción de unicidad" que pedía, porque el
        // nombre tiene homónimos (ver Donante). Único cuando se da
        // (UX_Arrendatarios_Identificacion, filtrado): quienes no la dan no chocan
        // entre sí, igual que NumIdentidad en Beneficiario.
        public string? Identificacion { get; set; }

        // Teléfono obligatorio, a diferencia de Donante: para confirmar, mover o
        // cancelar una reserva hay que poder ubicar al arrendatario. El correo sigue
        // siendo opcional. Código de país en columna aparte (ver ReglasTelefono).
        public string CodigoPaisTelefono { get; set; } = ReglasTelefono.CodigoPaisCostaRica;
        public string Telefono { get; set; } = string.Empty;
        public string? Correo { get; set; }

        // Baja lógica, mismo patrón que Donante: un arrendatario inactivo deja de
        // aparecer al registrar alquileres, pero su historial sigue apuntando a él.
        public bool Estado { get; set; } = true;

        public DateTime FechaRegistro { get; set; } = DateTime.Now;
    }
}
