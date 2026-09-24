using SIGAC.Application.Exceptions;
using SIGAC.Domain;

namespace SIGAC.Application.Validators
{
    public sealed record TelefonoValidado(string? CodigoPais, string? Numero);

    // Validación del teléfono con código de país, compartida por BeneficiarioValidator
    // y DonanteValidator para que los dos módulos exijan exactamente lo mismo. Las
    // reglas puras viven en ReglasTelefono; acá se aplican y se traducen a mensajes.
    public static class TelefonoValidator
    {
        // Opcional: sin número se guardan los dos campos en NULL, aunque la
        // pantalla haya mandado un código de país (el selector siempre tiene uno).
        //
        // Sin código de país se asume Costa Rica: es lo que muestra el selector por
        // defecto y lo que se exigía antes de que el código existiera.
        public static TelefonoValidado Validar(string? codigoPais, string? numero)
        {
            var valor = numero?.Trim();

            if (string.IsNullOrEmpty(valor))
                return new TelefonoValidado(null, null);

            var codigo = codigoPais?.Trim().TrimStart('+');

            if (string.IsNullOrEmpty(codigo))
                codigo = ReglasTelefono.CodigoPaisCostaRica;

            if (!ReglasTelefono.EsCodigoPaisValido(codigo))
                throw new ValidationException("El código de país del teléfono no es válido.");

            if (!ReglasTelefono.TieneFormatoNumero(codigo, valor))
                throw new ValidationException(
                    $"El teléfono no es válido para el código +{codigo}. Se espera: {ReglasTelefono.DescribirNumero(codigo)}");

            return new TelefonoValidado(codigo, valor);
        }
    }
}
