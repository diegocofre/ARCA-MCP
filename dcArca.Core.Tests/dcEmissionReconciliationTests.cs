using System.Net;
using System.Text;
using dcArca.Core.Models;
using dcArca.Core.Services;
using Xunit;

namespace dcArca.Core.Tests;

public class dcEmissionReconciliationTests
{
    [Fact]
    public async Task TransporteFalla_PeroArcaTieneComprobante_DevuelveExitoRecuperado()
    {
        var handler = new SequenceHandler(
            _ => throw new HttpRequestException("connection reset after send"),
            _ => Response(ConsultSuccessXml));

        using var client = await BuildClientAsync(handler);
        var result = await client.FECAESolicitarAsync(ValidRequest());

        Assert.True(result.Success);
        Assert.Equal(dcEmissionOutcome.RecoveredSuccess, result.EmissionOutcome);
        Assert.Equal("FECAE_RECOVERED", result.Codigo);
        Assert.Equal("12345678901234", result.Cae);
        Assert.Equal(2, handler.CallCount);
    }

    [Fact]
    public async Task TransporteFalla_YReconciliacionNoConfirma_DevuelveResultadoInciertoSinTercerEnvio()
    {
        var handler = new SequenceHandler(
            _ => throw new HttpRequestException("timeout"),
            _ => throw new HttpRequestException("reconciliation unavailable"));

        using var client = await BuildClientAsync(handler);
        var result = await client.FECAESolicitarAsync(ValidRequest());

        Assert.False(result.Success);
        Assert.Equal(dcEmissionOutcome.Uncertain, result.EmissionOutcome);
        Assert.Equal("FECAE_OUTCOME_UNCERTAIN", result.Codigo);
        Assert.Equal(2, handler.CallCount);
    }

    [Fact]
    public async Task RechazoFiscalNormal_NoDisparaReconciliacion()
    {
        var handler = new SequenceHandler(_ => Response(RejectedXml));

        using var client = await BuildClientAsync(handler);
        var result = await client.FECAESolicitarAsync(ValidRequest());

        Assert.False(result.Success);
        Assert.Equal(dcEmissionOutcome.FiscalRejected, result.EmissionOutcome);
        Assert.Equal(1, handler.CallCount);
    }

    private static async Task<dcWsfeClient> BuildClientAsync(HttpMessageHandler handler)
    {
        var cuit = $"20{Random.Shared.NextInt64(100000000, 999999999):D9}";
        var config = new dcArcaConfig
        {
            Cuit = cuit,
            CertificatePath = "unused.pfx",
            WsaaUrl = "https://example.test/wsaa",
            WsfeUrl = "https://example.test/wsfe",
            PuntoVenta = 1
        };

        var store = new MemoryWsaaTokenStore();
        await store.WriteAsync(
            $"{cuit}_wsfe",
            new WsaaTokenEntry("token", "sign", DateTime.UtcNow.AddHours(1)));

        var auth = new dcArcaAuthService(
            config.WsaaUrl,
            config.CertificatePath,
            "",
            cuit,
            tokenStore: store);

        return new dcWsfeClient(config, auth, new HttpClient(handler));
    }

    private static dcFacturaRequest ValidRequest() => new()
    {
        TipoComprobante = dcTipoComprobante.FacturaB,
        NumeroComprobante = 1,
        Concepto = dcConcepto.Productos,
        CuitReceptor = 20123456786,
        TipoDocReceptor = (int)dcTipoDocumento.CUIT,
        CondicionIvaReceptor = dcCondicionIvaReceptor.ResponsableInscripto,
        ImporteNeto = 100m,
        ImporteIva = 21m,
        ImporteTotal = 121m,
        AlicuotaIva = dcAlicuotaIva.Veintiuno,
        FechaComprobante = "20260930"
    };

    private static HttpResponseMessage Response(string xml) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(xml, Encoding.UTF8, "application/soap+xml")
    };

    private const string ConsultSuccessXml = """
        <soap:Envelope xmlns:soap="http://www.w3.org/2003/05/soap-envelope" xmlns:ar="http://ar.gov.afip.dif.FEV1/">
          <soap:Body>
            <ar:FECompConsultarResponse>
              <ar:FECompConsultarResult>
                <ar:ResultGet>
                  <ar:Resultado>A</ar:Resultado>
                  <ar:CbteDesde>1</ar:CbteDesde>
                  <ar:CbteHasta>1</ar:CbteHasta>
                  <ar:CodAutorizacion>12345678901234</ar:CodAutorizacion>
                  <ar:FchVto>20261010</ar:FchVto>
                </ar:ResultGet>
              </ar:FECompConsultarResult>
            </ar:FECompConsultarResponse>
          </soap:Body>
        </soap:Envelope>
        """;

    private const string RejectedXml = """
        <soap:Envelope xmlns:soap="http://www.w3.org/2003/05/soap-envelope" xmlns:ar="http://ar.gov.afip.dif.FEV1/">
          <soap:Body>
            <ar:FECAESolicitarResponse>
              <ar:FECAESolicitarResult>
                <ar:FeCabResp><ar:Resultado>R</ar:Resultado></ar:FeCabResp>
                <ar:FeDetResp>
                  <ar:FECAEDetResponse>
                    <ar:Resultado>R</ar:Resultado>
                    <ar:Observaciones><ar:Obs><ar:Code>10016</ar:Code><ar:Msg>rechazado</ar:Msg></ar:Obs></ar:Observaciones>
                  </ar:FECAEDetResponse>
                </ar:FeDetResp>
              </ar:FECAESolicitarResult>
            </ar:FECAESolicitarResponse>
          </soap:Body>
        </soap:Envelope>
        """;

    private sealed class SequenceHandler(params Func<HttpRequestMessage, HttpResponseMessage>[] steps) : HttpMessageHandler
    {
        private readonly Queue<Func<HttpRequestMessage, HttpResponseMessage>> _steps = new(steps);
        internal int CallCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            if (_steps.Count == 0)
            {
                throw new InvalidOperationException("Unexpected extra HTTP call");
            }

            return Task.FromResult(_steps.Dequeue()(request));
        }
    }
}
