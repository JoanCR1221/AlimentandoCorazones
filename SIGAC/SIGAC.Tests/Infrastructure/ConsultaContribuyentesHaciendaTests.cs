using System.Net;
using System.Text;
using SIGAC.Application.Interfaces;
using SIGAC.Infrastructure.Hacienda;

namespace SIGAC.Tests.Infrastructure
{
    // Sin red: el cliente se prueba contra un manejador falso.
    public class ConsultaContribuyentesHaciendaTests
    {
        private sealed class ManejadorFalso : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _responder;

            public ManejadorFalso(Func<HttpRequestMessage, Task<HttpResponseMessage>> responder) => _responder = responder;

            public int Llamadas { get; private set; }
            public Uri? UltimaUrl { get; private set; }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Llamadas++;
                UltimaUrl = request.RequestUri;
                return _responder(request);
            }
        }

        private static HttpResponseMessage Json(HttpStatusCode codigo, string json) =>
            new(codigo) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

        private static (ConsultaContribuyentesHacienda cliente, ManejadorFalso manejador) Crear(
            Func<HttpRequestMessage, Task<HttpResponseMessage>> responder)
        {
            var manejador = new ManejadorFalso(responder);
            var http = new HttpClient(manejador) { BaseAddress = new Uri("https://api.hacienda.go.cr/") };
            return (new ConsultaContribuyentesHacienda(http), manejador);
        }

        [Fact]
        public async Task Devuelve_el_nombre_y_llama_con_la_cedula_en_la_url()
        {
            var (cliente, manejador) = Crear(_ => Task.FromResult(Json(HttpStatusCode.OK,
                """{"nombre":"  JUAN   PEREZ MORA ","tipoIdentificacion":"01","actividades":[]}""")));

            var resultado = await cliente.ConsultarCedulaAsync("123456789");

            Assert.Equal(EstadoConsultaContribuyente.Encontrado, resultado.Estado);
            Assert.Equal("JUAN PEREZ MORA", resultado.NombreCompleto);
            Assert.Equal("https://api.hacienda.go.cr/fe/ae?identificacion=123456789", manejador.UltimaUrl!.ToString());
        }

        // Respuesta real de Hacienda para una empresa pública (recortada): "nombre" es
        // la razón social.
        [Fact]
        public async Task Una_cedula_juridica_devuelve_la_razon_social()
        {
            var (cliente, manejador) = Crear(_ => Task.FromResult(Json(HttpStatusCode.OK,
                """{"nombre":"BANCO NACIONAL DE COSTA RICA","tipoIdentificacion":"02","regimen":{"codigo":1},"actividades":[]}""")));

            var resultado = await cliente.ConsultarCedulaAsync("4000001021");

            Assert.Equal(EstadoConsultaContribuyente.Encontrado, resultado.Estado);
            Assert.Equal("BANCO NACIONAL DE COSTA RICA", resultado.NombreCompleto);
            Assert.Equal(1, manejador.Llamadas);
        }

        [Theory]
        [InlineData(HttpStatusCode.NotFound)]
        [InlineData(HttpStatusCode.BadRequest)]
        public async Task Sin_datos_de_la_cedula_es_no_encontrado(HttpStatusCode codigo)
        {
            var (cliente, _) = Crear(_ => Task.FromResult(Json(codigo, """{"code":404,"status":"sin datos"}""")));

            var resultado = await cliente.ConsultarCedulaAsync("888888888");

            Assert.Equal(EstadoConsultaContribuyente.NoEncontrado, resultado.Estado);
            Assert.Null(resultado.NombreCompleto);
        }

        [Theory]
        [InlineData("{}")]
        [InlineData("""{"nombre":""}""")]
        [InlineData("""{"nombre":null}""")]
        [InlineData("[]")]
        public async Task Respuesta_sin_nombre_aprovechable_es_no_encontrado(string json)
        {
            var (cliente, _) = Crear(_ => Task.FromResult(Json(HttpStatusCode.OK, json)));

            var resultado = await cliente.ConsultarCedulaAsync("123456789");

            Assert.Equal(EstadoConsultaContribuyente.NoEncontrado, resultado.Estado);
        }

        [Fact]
        public async Task Error_del_servicio_es_no_disponible()
        {
            var (cliente, _) = Crear(_ => Task.FromResult(Json(HttpStatusCode.InternalServerError, "{}")));

            var resultado = await cliente.ConsultarCedulaAsync("123456789");

            Assert.Equal(EstadoConsultaContribuyente.NoDisponible, resultado.Estado);
        }

        [Fact]
        public async Task Sin_conexion_es_no_disponible()
        {
            var (cliente, _) = Crear(_ => throw new HttpRequestException("sin red"));

            var resultado = await cliente.ConsultarCedulaAsync("123456789");

            Assert.Equal(EstadoConsultaContribuyente.NoDisponible, resultado.Estado);
        }

        [Fact]
        public async Task Tiempo_agotado_es_no_disponible()
        {
            // El HttpClient levanta TaskCanceledException cuando vence su Timeout,
            // sin que nadie haya cancelado el token.
            var (cliente, _) = Crear(_ => throw new TaskCanceledException("tiempo agotado"));

            var resultado = await cliente.ConsultarCedulaAsync("123456789");

            Assert.Equal(EstadoConsultaContribuyente.NoDisponible, resultado.Estado);
        }

        [Fact]
        public async Task Json_invalido_es_no_disponible()
        {
            var (cliente, _) = Crear(_ => Task.FromResult(Json(HttpStatusCode.OK, "no es json")));

            var resultado = await cliente.ConsultarCedulaAsync("123456789");

            Assert.Equal(EstadoConsultaContribuyente.NoDisponible, resultado.Estado);
        }

        [Fact]
        public async Task Si_la_pantalla_cancela_se_propaga_la_cancelacion()
        {
            using var origen = new CancellationTokenSource();
            var (cliente, _) = Crear(_ =>
            {
                origen.Cancel();
                throw new OperationCanceledException(origen.Token);
            });

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => cliente.ConsultarCedulaAsync("123456789", origen.Token));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("12345678")]       // 8 dígitos
        [InlineData("12345678901")]    // 11 dígitos (DIMEX): no está en este servicio
        [InlineData("12345678a")]
        [InlineData("1-2345-6789")]
        [InlineData("3-101-123456")]
        public async Task Solo_se_envian_cedulas_con_formato_de_cedula_nacional_o_juridica(string? cedula)
        {
            var (cliente, manejador) = Crear(_ => Task.FromResult(Json(HttpStatusCode.OK, """{"nombre":"X Y"}""")));

            var resultado = await cliente.ConsultarCedulaAsync(cedula);

            Assert.Equal(EstadoConsultaContribuyente.NoEncontrado, resultado.Estado);
            Assert.Equal(0, manejador.Llamadas);
        }
    }
}
