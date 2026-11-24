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
        //
        // Teléfono con código de país en dos columnas, igual que Beneficiario (ver
        // ReglasTelefono). Los teléfonos cargados antes del código de país que no
        // eran de Costa Rica quedaron con CodigoPaisTelefono en NULL y el texto
        // original: se corrigen al editar el donante.
        public string? CodigoPaisTelefono { get; set; }
        public string? Telefono { get; set; }
        public string? Correo { get; set; }

        // Documento del donante, opcional como el resto de los datos de contacto:
        // las donaciones anónimas o de paso no lo tienen ("Sin documento", que es lo
        // que quedó en los donantes anteriores a este campo). Valor de
        // TiposDocumento.TodosParaOrganizaciones; un donante puede ser una empresa,
        // por eso incluye la cédula jurídica.
        //
        // NO es único, a diferencia de Arrendatario.Identificacion: la regla de que
        // dos donantes homónimos son dos donantes (ver IX_Donantes_Nombre) no se
        // cambió, y bloquear por documento es una decisión aparte.
        public string TipoDocumento { get; set; } = TiposDocumento.SinDocumento;

        // Solo con TipoDocumento "Otro": cómo se llama el documento.
        public string? TipoDocumentoOtro { get; set; }

        // Número sin espacios ni guiones y en mayúsculas; NULL si no tiene documento.
        public string? Identificacion { get; set; }

        // Baja lógica, mismo patrón que Beneficiario: un donante inactivo deja de
        // aparecer en los desplegables, pero sus donaciones históricas siguen
        // apuntando a él y el historial no se rompe.
        public bool Estado { get; set; } = true;

        public DateTime FechaRegistro { get; set; } = DateTime.Now;
    }
}
