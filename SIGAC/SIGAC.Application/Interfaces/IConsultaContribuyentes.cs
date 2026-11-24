namespace SIGAC.Application.Interfaces
{
    public enum EstadoConsultaContribuyente
    {
        Encontrado,

        // Hacienda respondió que no tiene datos de esa cédula.
        NoEncontrado,

        // No se pudo consultar (sin conexión, tiempo agotado, error del servicio).
        // No dice nada sobre la cédula: la persona escribe los datos a mano.
        NoDisponible
    }

    public sealed record ResultadoConsultaContribuyente(EstadoConsultaContribuyente Estado, string? NombreCompleto = null);

    // Consulta de una cédula en el servicio público de Hacienda. Es solo una ayuda
    // para no teclear el nombre: nada del sistema depende de que responda.
    public interface IConsultaContribuyentes
    {
        // Solo acepta una cédula nacional (9 dígitos) o jurídica (10); con cualquier
        // otro valor devuelve NoEncontrado sin llamar a Hacienda, para no mandar
        // afuera texto que no es una cédula.
        Task<ResultadoConsultaContribuyente> ConsultarCedulaAsync(string? cedula, CancellationToken cancellationToken = default);
    }
}
