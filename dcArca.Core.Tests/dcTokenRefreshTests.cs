using dcArca.Core.Models;
using dcArca.Core.Services;
using dcArca.Core.Services.Logging;
using System.Net;
using System.Text;
using Xunit;

namespace dcArca.Core.Tests;

public class dcTokenRefreshTests
{
    private const string FaultTemplate = """
        <s:Envelope xmlns:s="http://www.w3.org/2003/05/soap-envelope">
          <s:Body><s:Fault><faultcode>soap:Client</faultcode><faultstring>{0}</faultstring></s:Fault></s:Body>
        </s:Envelope>
        """;

    [Theory]
    [InlineData("La firma no válida")]
    [InlineData("Firma inválida")]
    public void ParserDetectaFirmaInvalidaConTildes(string reason)
    {
        var parser = new dcWsfeSoapParser(NoOpAfipLogger.Instance);
        var xml = string.Format(FaultTemplate, reason);

        Assert.Throws<dcTokenInvalidException>(() => parser.ParseUltimoComprobanteResponse(xml, 1));
    }

    [Fact]
    public async Task WsfeReintentaCuandoFaultLlegaConHttp500()
    {
        var cuit = $"20{Random.Shared.NextInt64(100000000, 999999999):D9}";
        var directory = TempDirectory();
        var store = new FileSystemWsaaTokenStore(directory);
        var key = $"{cuit}_wsfe";
        await store.WriteAsync(key, Entry("rechazado"));

        try
        {
            var config = new dcArcaConfig
            {
                Cuit = cuit,
                CertificatePath = "certificado-inexistente.pfx",
                WsaaUrl = "https://example.test/wsaa",
                WsfeUrl = "https://example.test/wsfe",
                PuntoVenta = 1
            };
            var auth = new dcArcaAuthService(
                config.WsaaUrl, config.CertificatePath, "", cuit, tokenStore: store);
            using var http = new HttpClient(new FaultHandler());
            using var client = new dcWsfeClient(config, auth, http);

            var result = await client.FECompUltimoAutorizadoAsync(dcTipoComprobante.FacturaA);

            Assert.Contains("Error tras reintento por token inválido", result.Mensaje);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task InvalidacionTardiaNoBorraTokenRenovado()
    {
        var cuit = $"20{Random.Shared.NextInt64(100000000, 999999999):D9}";
        var directory = TempDirectory();
        var store = new FileSystemWsaaTokenStore(directory);
        var key = $"{cuit}_wsfe";
        await store.WriteAsync(key, Entry("rechazado"));

        try
        {
            var auth = new dcArcaAuthService(
                "https://example.test/wsaa", "certificado-inexistente.pfx", "", cuit, tokenStore: store);

            await store.WriteAsync(key, Entry("renovado"));
            await auth.InvalidateCacheAsync("rechazado");

            Assert.NotNull(await store.ReadAsync(key));
            Assert.Equal("renovado", (await auth.GetTokenAsync()).token);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static WsaaTokenEntry Entry(string token)
        => new(token, "firma", DateTime.UtcNow.AddHours(1));

    private static string TempDirectory()
        => Path.Combine(Path.GetTempPath(), "dcArca-tests", Guid.NewGuid().ToString("N"));

    private sealed class FaultHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent(string.Format(FaultTemplate, "Firma inválida"), Encoding.UTF8, "application/soap+xml")
            });
    }
}
