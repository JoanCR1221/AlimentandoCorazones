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
}