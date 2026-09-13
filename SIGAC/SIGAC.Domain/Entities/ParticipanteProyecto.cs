namespace SIGAC.Domain.Entities
{
    // Persona vinculada a un proyecto comunitario. Puede ser un beneficiario ya
    // registrado en el sistema o alguien externo (un vecino, un facilitador
    // invitado) sin ficha propia. EsBeneficiario es el discriminador que decide
    // cuál de los dos grupos de campos aplica, mismo patrón que TipoDestinatario en
    // DonacionEntregada.
    public class ParticipanteProyecto
    {
        public int Id { get; set; }

        public int ProyectoId { get; set; }
        public ProyectoComunitario? Proyecto { get; set; }

        public bool EsBeneficiario { get; set; }

        // Se llenan solo cuando EsBeneficiario es true; NULL en caso contrario. La
        // exclusión mutua con NombreExterno/ContactoExterno la sostiene el CHECK
        // CK_ParticipantesProyecto_Discriminador en SigacDbContext.
        public int? BeneficiarioId { get; set; }
        public Beneficiario? Beneficiario { get; set; }

        // Se llenan solo cuando EsBeneficiario es false. ContactoExterno es
        // opcional incluso en ese caso: un participante externo puede no dejar
        // datos de contacto.
        public string? NombreExterno { get; set; }
        public string? ContactoExterno { get; set; }

        public DateTime FechaRegistro { get; set; } = DateTime.Now;
    }
}
