using SIGAC.Application.Exceptions;
using SIGAC.Domain;

namespace SIGAC.Application.Validators
{
    // Documento de un donante o un arrendatario, ya validado y normalizado.
    public sealed record DocumentoValidado(string TipoDocumento, string? TipoDocumentoOtro, string? Identificacion);

    // Validación del documento de donantes y arrendatarios, en un solo lugar para que
    // los dos módulos pidan lo mismo. Las reglas del número por tipo salen de
    // ReglasBeneficiario, la misma fuente que usa la pantalla, así que el campo y
    // el servidor no pueden discrepar.
    //
    // El documento es opcional: "Sin documento" es la forma explícita de no tenerlo
    // (donaciones anónimas, arrendatarios que no lo dan). Si se elige otro tipo, el
    // número sí es obligatorio.
    public static class DocumentoOrganizacionValidator
    {
        public static DocumentoValidado Validar(string? tipoDocumento, string? tipoDocumentoOtro, string? identificacion)
        {
            var tipo = TextoNormalizador.CompactarEspacios(tipoDocumento);

            // Sin espacios ni guiones y en mayúsculas, igual que se guarda: una cédula
            // se escribe "1-2345-6789" y "123456789", y el índice único de
            // arrendatarios las tiene que ver como la misma.
            var numero = ArrendatarioValidator.NormalizarIdentificacion(identificacion);

            // Quien no manda tipo (por ejemplo el donante que se crea desde el
            // formulario de donación) no tiene documento.
            if (tipo.Length == 0)
            {
                if (numero is not null)
                    throw new ValidationException("Elija el tipo de documento del número de identidad.");

                tipo = TiposDocumento.SinDocumento;
            }

            if (!TiposDocumento.EsValidoParaOrganizacion(tipo))
            {
                throw new ValidationException(
                    $"El tipo de documento '{tipo}' no es válido. " +
                    $"Valores válidos: {string.Join(", ", TiposDocumento.TodosParaOrganizaciones)}.");
            }

            // La pantalla oculta el número con este tipo; si llega uno, se descarta
            // en vez de guardarlo huérfano.
            if (!TiposDocumento.RequiereNumIdentidad(tipo))
                return new DocumentoValidado(TiposDocumento.SinDocumento, null, null);

            if (numero is null)
            {
                throw new ValidationException(
                    $"El número de identidad es obligatorio con {tipo}. " +
                    $"Si no tiene documento, elija '{TiposDocumento.SinDocumento}'.");
            }

            if (!ReglasBeneficiario.TieneFormatoNumIdentidad(tipo, numero))
            {
                throw new ValidationException(
                    $"El número de identidad no es válido para {tipo}: " +
                    ReglasBeneficiario.DescribirNumIdentidad(tipo));
            }

            return new DocumentoValidado(tipo, ValidarNombreDelDocumento(tipo, tipoDocumentoOtro), numero);
        }

        // "Otro" pide cómo se llama el documento; con cualquier otro tipo no tiene
        // sentido y no se guarda.
        private static string? ValidarNombreDelDocumento(string tipo, string? valor)
        {
            if (!TiposDocumento.RequiereNombreDelDocumento(tipo))
                return null;

            var nombre = TextoNormalizador.CompactarEspacios(valor);

            if (nombre.Length == 0)
                throw new ValidationException("Indique cómo se llama el documento.");

            if (nombre.Length > ReglasBeneficiario.LongitudMaximaTipoDocumentoOtro)
                throw new ValidationException(
                    $"El nombre del documento no puede superar los {ReglasBeneficiario.LongitudMaximaTipoDocumentoOtro} caracteres.");

            return nombre;
        }
    }
}
