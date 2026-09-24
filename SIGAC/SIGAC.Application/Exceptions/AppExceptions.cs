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

    // La operación principal YA quedó guardada, pero no se pudo anotar en la
    // bitácora (que escribe por su propia conexión, fuera de la transacción).
    // Se distingue del error genérico para que la pantalla no diga "no se pudo"
    // sobre algo que sí se hizo: el usuario reintentaría y chocaría con su
    // propio registro como duplicado.
    public class BitacoraNoRegistradaException : Exception
    {
        public BitacoraNoRegistradaException(string mensaje, Exception inner) : base(mensaje, inner) { }
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

    // Caso particular de ValidationException del módulo de seguridad: la
    // operación dejaría al sistema sin ningún Administrador activo (quitarle el
    // rol o desactivar al único que queda). Se distingue para que la pantalla
    // muestre un mensaje específico y no el genérico de validación.
    public class UltimoAdministradorException : ValidationException
    {
        public UltimoAdministradorException(string mensaje) : base(mensaje) { }
    }
}