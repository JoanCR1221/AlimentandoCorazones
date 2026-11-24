using System.Net;
using System.Text.Json;
using SIGAC.Application.Interfaces;
using SIGAC.Domain;

namespace SIGAC.Infrastructure.Hacienda
{
    // Cliente del servicio público de Hacienda: GET fe/ae?identificacion={cédula}.
    // Responde un JSON con el campo "nombre"; 404 si no hay datos de esa cédula.
    //
    // La cédula es un dato personal y sale de SIGAC hacia Hacienda, así que solo se
    // envía si tiene el formato de una cédula nacional (9 dígitos) o jurídica (10), y
    // no se escribe en ningún log. Para una jurídica "nombre" es la razón social.
    public sealed class ConsultaContribuyentesHacienda : IConsultaContribuyentes
    {
        private readonly HttpClient _http;

        public ConsultaContribuyentesHacienda(HttpClient http)
        {
            _http = http;
        }

        public async Task<ResultadoConsultaContribuyente> ConsultarCedulaAsync(
            string? cedula, CancellationToken cancellationToken = default)
        {
            if (!ReglasBeneficiario.TieneFormatoCedulaNacional(cedula)
                && !ReglasBeneficiario.TieneFormatoCedulaJuridica(cedula))
                return new ResultadoConsultaContribuyente(EstadoConsultaContribuyente.NoEncontrado);

            try
            {
                using var respuesta = await _http.GetAsync(
                    $"fe/ae?identificacion={Uri.EscapeDataString(cedula!)}", cancellationToken);

                // Hacienda usa 404 cuando no hay datos y 400 cuando rechaza el valor.
                if (respuesta.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.BadRequest)
                    return new ResultadoConsultaContribuyente(EstadoConsultaContribuyente.NoEncontrado);

                if (!respuesta.IsSuccessStatusCode)
                    return new ResultadoConsultaContribuyente(EstadoConsultaContribuyente.NoDisponible);

                await using var contenido = await respuesta.Content.ReadAsStreamAsync(cancellationToken);
                using var documento = await JsonDocument.ParseAsync(contenido, cancellationToken: cancellationToken);

                if (documento.RootElement.ValueKind == JsonValueKind.Object
                    && documento.RootElement.TryGetProperty("nombre", out var nombre)
                    && nombre.ValueKind == JsonValueKind.String
                    && !string.IsNullOrWhiteSpace(nombre.GetString()))
                {
                    return new ResultadoConsultaContribuyente(
                        EstadoConsultaContribuyente.Encontrado,
                        TextoNormalizador.CompactarEspacios(nombre.GetString()));
                }

                // Respondió bien pero sin nombre: no hay nada que aprovechar.
                return new ResultadoConsultaContribuyente(EstadoConsultaContribuyente.NoEncontrado);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // La pantalla ya no espera la respuesta (cambió el número o se cerró).
                throw;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
            {
                // TaskCanceledException sin cancelación pedida es el tiempo agotado
                // del HttpClient.
                return new ResultadoConsultaContribuyente(EstadoConsultaContribuyente.NoDisponible);
            }
        }
    }
}
