namespace SIGAC.Domain.Entities
{
    public class Donante
    {
        public int Id { get; set; }

        // Un solo campo de nombre y no la descomposición de Beneficiario
        // (PrimerNombre, SegundoApellido...): un donante puede ser una empresa o
        // una fundación, y "Supermercados del Valle S.A." no se parte en nombres
        // y apellidos.
        public string Nombre { get; set; } = string.Empty;

        // Valor del catálogo cerrado TiposPersonaDonante ("Física" o "Jurídica").
        public string TipoPersona { get; set; } = string.Empty;

        // Opcionales: la donación se recibe igual aunque el donante no deje datos
        // de contacto, y en las donaciones anónimas o de paso no hay ninguno.
        public string? Telefono { get; set; }
        public string? Correo { get; set; }

        // Baja lógica, mismo patrón que Beneficiario: un donante inactivo deja de
        // aparecer en los desplegables, pero sus donaciones históricas siguen
        // apuntando a él y el historial no se rompe.
        public bool Estado { get; set; } = true;

        public DateTime FechaRegistro { get; set; } = DateTime.Now;
    }
}
