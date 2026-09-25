using System.ComponentModel.DataAnnotations;
using SIGAC.Domain.Entities;

namespace SIGAC.Application.DTOs.Proyectos
{
    public class ProyectoCrearDto
    {
        [Required(ErrorMessage = "El nombre es obligatorio.")]
        public string Nombre { get; set; } = string.Empty;

        [Required(ErrorMessage = "La descripción es obligatoria.")]
        public string Descripcion { get; set; } = string.Empty;

        public DateTime FechaInicio { get; set; }

        public DateTime FechaEstimadaFin { get; set; }
    }

    // Estado no es un campo de edición libre en cualquier valor: la validación de
    // negocio (ProyectoValidator.ValidarEstadoParaEdicion) rechaza que se llegue a
    // Finalizado por esta vía, porque esa transición además registra la fecha de
    // finalización real y eso solo lo hace FinalizarProyectoAsync.
    public class ProyectoEditarDto
    {
        [Required(ErrorMessage = "El nombre es obligatorio.")]
        public string Nombre { get; set; } = string.Empty;

        [Required(ErrorMessage = "La descripción es obligatoria.")]
        public string Descripcion { get; set; } = string.Empty;

        public DateTime FechaInicio { get; set; }

        public DateTime FechaEstimadaFin { get; set; }

        public EstadoProyecto Estado { get; set; }
    }

    public class ProyectoListaDto
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public DateTime FechaInicio { get; set; }
        public DateTime FechaEstimadaFin { get; set; }
        public string Estado { get; set; } = string.Empty;
        public int TotalParticipantes { get; set; }
    }

    // Ficha de un proyecto: sus datos completos y la lista de participantes. La
    // lee la pantalla de detalle (/proyectos/{id}) y, para el encabezado y el
    // estado, la de registrar participante.
    //
    // Estado viaja como enum y no como texto (a diferencia de ProyectoListaDto):
    // la pantalla decide con él qué acciones ofrecer.
    public class ProyectoDetalleDto
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string Descripcion { get; set; } = string.Empty;
        public DateTime FechaInicio { get; set; }
        public DateTime FechaEstimadaFin { get; set; }
        public DateTime? FechaFinalizacionReal { get; set; }
        public EstadoProyecto Estado { get; set; }
        public DateTime FechaRegistro { get; set; }
        public List<ParticipanteListaDto> Participantes { get; set; } = new();

        // Finalizado y Cancelado: no admiten edición ni cambios de participantes.
        public bool EstaCerrado => Estado is EstadoProyecto.Finalizado or EstadoProyecto.Cancelado;
    }

    // Una fila de la lista de participantes. Nombre y Contacto ya vienen resueltos
    // según el tipo: para un beneficiario salen de su ficha (y se mantienen al día
    // si se edita), para un externo de lo que se cargó al agregarlo.
    public class ParticipanteListaDto
    {
        public int Id { get; set; }
        public bool EsBeneficiario { get; set; }
        public int? BeneficiarioId { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string? Contacto { get; set; }

        // Solo informativo: un beneficiario que se desactivó después de sumarse al
        // proyecto sigue figurando como participante.
        public bool BeneficiarioActivo { get; set; }
        public DateTime FechaRegistro { get; set; }
    }

    public class FiltrosProyectoDto
    {
        public EstadoProyecto? Estado { get; set; }
    }

    // EsBeneficiario decide cuál de los dos grupos de campos se valida:
    // BeneficiarioId cuando es true, NombreExterno/ContactoExterno cuando es
    // false. Ver ParticipanteProyectoValidator.
    public class ParticipanteCrearDto
    {
        public int ProyectoId { get; set; }
        public bool EsBeneficiario { get; set; }
        public int? BeneficiarioId { get; set; }
        public string? NombreExterno { get; set; }
        public string? ContactoExterno { get; set; }
    }
}
