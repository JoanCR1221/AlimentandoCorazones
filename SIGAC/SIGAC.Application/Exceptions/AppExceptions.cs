namespace SIGAC.Application.Exceptions
{
    public class NotFoundException : Exception
    {
        public NotFoundException(string mensaje) : base(mensaje) { }
    }

    public class ValidationException : Exception
    {
        public ValidationException(string mensaje) : base(mensaje) { }
    }

    public class DuplicateException : Exception
    {
        public DuplicateException(string mensaje) : base(mensaje) { }
    }

    // Caso particular de ValidationException: el beneficiario existe pero está
    // inactivo. Se distingue del resto para que una futura pantalla pueda ofrecer
    // el botón de reactivarlo (IBeneficiariosService.ActivarBeneficiarioAsync ya
    // existe) en vez de mostrar solo un mensaje genérico.
    public class BeneficiarioInactivoException : ValidationException
    {
        public int BeneficiarioId { get; }

        public BeneficiarioInactivoException(int beneficiarioId, string mensaje) : base(mensaje)
        {
            BeneficiarioId = beneficiarioId;
        }
    }
}