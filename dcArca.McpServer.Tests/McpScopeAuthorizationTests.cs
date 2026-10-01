using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace dcArca.McpServer.Tests;

/// <summary>
/// Reemplaza JWT Bearer por un esquema de prueba que arma el ClaimsPrincipal a partir de un
/// header, para poder probar la policy "ArcaFacturar" sin tener que firmar un JWT real.
/// </summary>
public class ScopeTestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "ScopeTest";
    public const string ScopeHeader = "X-Test-Scope";

    public ScopeTestAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var scope = Request.Headers.TryGetValue(ScopeHeader, out var values) ? values.ToString() : null;
        if (string.IsNullOrEmpty(scope))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var identity = new ClaimsIdentity([new Claim("scope", scope)], SchemeName);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}

public class McpScopeAuthorizationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public McpScopeAuthorizationTests(WebApplicationFactory<Program> factory)
    {
        var contentRoot = Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "dcArca.McpServer");

        var certPath = Path.Combine(Directory.GetCurrentDirectory(), "test-cert-placeholder.pfx");
        if (!File.Exists(certPath))
        {
            File.WriteAllBytes(certPath, Array.Empty<byte>());
        }

        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseContentRoot(contentRoot);
            builder.UseEnvironment("Testing");
            builder.ConfigureTestServices(services =>
            {
                services.AddAuthentication()
                    .AddScheme<AuthenticationSchemeOptions, ScopeTestAuthHandler>(ScopeTestAuthHandler.SchemeName, null);

                // Program.cs ya fijó DefaultAuthenticateScheme/DefaultChallengeScheme explícitos
                // (JwtBearer/Mcp) en su propio Configure<AuthenticationOptions>; PostConfigure
                // corre después de todos los Configure, así que gana y reemplaza esos valores
                // por el esquema de prueba para este test.
                services.PostConfigure<AuthenticationOptions>(options =>
                {
                    options.DefaultScheme = ScopeTestAuthHandler.SchemeName;
                    options.DefaultAuthenticateScheme = ScopeTestAuthHandler.SchemeName;
                    options.DefaultChallengeScheme = ScopeTestAuthHandler.SchemeName;
                });
            });
        });
    }

    private static HttpRequestMessage BuildJsonRpcRequest(string scope, string method, object? @params = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/")
        {
            Content = JsonContent.Create(new { jsonrpc = "2.0", id = 1, method, @params })
        };
        request.Headers.Add(ScopeTestAuthHandler.ScopeHeader, scope);
        request.Headers.Add("Accept", "application/json, text/event-stream");
        return request;
    }

    [Fact]
    public async Task ToolsList_ConScopeSoloConsultar_NoIncluyeSolicitarCae()
    {
        var client = _factory.CreateClient();

        var response = await client.SendAsync(BuildJsonRpcRequest("arca:consultar", "tools/list"));
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain("\"name\":\"solicitar_cae\"", body);
        // Los tools de lectura requieren explícitamente arca:consultar.
        Assert.Contains("\"name\":\"consultar_padron\"", body);
        Assert.Contains("\"name\":\"consultar_comprobante\"", body);
        Assert.Contains("\"name\":\"consultar_ultimo_comprobante\"", body);
        Assert.Contains("\"name\":\"consultar_condiciones_iva\"", body);
    }

    [Fact]
    public async Task ToolsList_ConScopeFacturar_IncluyeSoloEmision()
    {
        var client = _factory.CreateClient();

        var response = await client.SendAsync(BuildJsonRpcRequest("arca:facturar", "tools/list"));
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("\"name\":\"solicitar_cae\"", body);
        Assert.DoesNotContain("\"name\":\"consultar_padron\"", body);
        Assert.DoesNotContain("\"name\":\"consultar_comprobante\"", body);
        Assert.DoesNotContain("\"name\":\"consultar_ultimo_comprobante\"", body);
        Assert.DoesNotContain("\"name\":\"consultar_condiciones_iva\"", body);
    }

    [Fact]
    public async Task ToolsList_ConScopesCombinados_IncluyeLecturaYEmision()
    {
        var client = _factory.CreateClient();

        var response = await client.SendAsync(BuildJsonRpcRequest("arca:consultar arca:facturar", "tools/list"));
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("\"name\":\"solicitar_cae\"", body);
        Assert.Contains("\"name\":\"consultar_padron\"", body);
        Assert.Contains("\"name\":\"consultar_comprobante\"", body);
        Assert.Contains("\"name\":\"consultar_ultimo_comprobante\"", body);
        Assert.Contains("\"name\":\"consultar_condiciones_iva\"", body);
    }

    [Fact]
    public async Task ToolsCall_SolicitarCae_ConScopeSoloConsultar_EsRechazado()
    {
        var client = _factory.CreateClient();

        var response = await client.SendAsync(BuildJsonRpcRequest("arca:consultar", "tools/call", new
        {
            name = "solicitar_cae",
            arguments = new
            {
                tipoComprobante = "FacturaB",
                numeroComprobante = 1,
                concepto = "Productos",
                cuitReceptor = 20123456786L,
                tipoDocReceptor = "CUIT",
                condicionIvaReceptor = "ConsumidorFinal",
                importeNeto = 100m,
                importeIva = 21m,
                importeTotal = 121m,
                fechaComprobante = "20260101",
            }
        }));
        var body = await response.Content.ReadAsStringAsync();

        // AddAuthorizationFilters() intercepta la llamada antes de ejecutar el tool y devuelve
        // un error JSON-RPC explícito (HTTP 200, la falla vive en el envelope JSON-RPC) cuando
        // el [Authorize(Policy = "ArcaFacturar")] del método no se cumple.
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Access forbidden", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("-32600", body);
    }

    [Fact]
    public async Task ToolsCall_ConsultarPadron_ConScopeSoloFacturar_EsRechazado()
    {
        var client = _factory.CreateClient();

        var response = await client.SendAsync(BuildJsonRpcRequest("arca:facturar", "tools/call", new
        {
            name = "consultar_padron",
            arguments = new { cuit = 20123456786L }
        }));
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Access forbidden", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("-32600", body);
    }

    [Fact]
    public async Task ToolsCall_ConsultarPadron_ConScopeSoloConsultar_NoEsRechazadoPorAutorizacion()
    {
        var client = _factory.CreateClient();

        var response = await client.SendAsync(BuildJsonRpcRequest("arca:consultar", "tools/call", new
        {
            name = "consultar_padron",
            arguments = new { cuit = 20123456786L }
        }));
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain("Unknown tool", body, StringComparison.OrdinalIgnoreCase);
    }
}
