using dcArca.Core;
using dcArca.Core.Models;
using dcArca.Core.Services;
using dcArca.Core.Services.Logging;
using dcArca.McpServer;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using ModelContextProtocol.AspNetCore;
using ModelContextProtocol.AspNetCore.Authentication;

var builder = WebApplication.CreateBuilder(args);

var jwtAuthority = builder.Configuration["Jwt:Authority"]
    ?? throw new InvalidOperationException("Falta configurar Jwt:Authority en appsettings.json");
var jwtAudience = builder.Configuration["Jwt:Audience"]
    ?? throw new InvalidOperationException("Falta configurar Jwt:Audience en appsettings.json");

// dcArcaConfig se carga con el mismo helper que usa dcArca.TestApp, valida CUIT/certificado al arrancar.
// appsettings.Development.json no trae su propia seccion dcArcaConfig (solo overrides de Logging), asi que
// solo "Testing" (que sí trae dcArcaConfig con el certificado placeholder) se resuelve a un archivo distinto.
var arcaSettingsFile = builder.Environment.EnvironmentName == "Testing"
    ? $"appsettings.{builder.Environment.EnvironmentName}.json"
    : "appsettings.json";
var arcaConfig = dcConfigurationHelper.LoadFromJson(
    Path.Combine(builder.Environment.ContentRootPath, arcaSettingsFile));

builder.Services.AddSingleton(arcaConfig);
builder.Services.AddSingleton<IAfipLogger>(sp =>
    new AfipLoggerAdapter(sp.GetRequiredService<ILoggerFactory>().CreateLogger("dcArca")));
builder.Services.AddSingleton<dcArcaAuthService>(sp => new dcArcaAuthService(
    arcaConfig.WsaaUrl, arcaConfig.CertificatePath, arcaConfig.CertificatePassword, arcaConfig.Cuit,
    logger: sp.GetRequiredService<IAfipLogger>()));
builder.Services.AddSingleton<IdcWsfeClient, dcWsfeClient>();
builder.Services.AddSingleton<IdcPadronClient, dcPadronClient>();

builder.Services.AddAuthentication(options =>
{
    options.DefaultChallengeScheme = McpAuthenticationDefaults.AuthenticationScheme;
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.Authority = jwtAuthority;
    // Solo en Development se permite un Authority http:// (ej. un IdP local en Docker sin TLS).
    // En cualquier otro ambiente (Testing, Production) sigue exigiendo HTTPS por default.
    options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidAudience = jwtAudience,
        ValidIssuer = jwtAuthority,
    };
})
.AddMcp(options =>
{
    options.ResourceMetadata = new()
    {
        AuthorizationServers = { jwtAuthority },
        ScopesSupported = ["arca:facturar", "arca:consultar"],
    };
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("ArcaConsultar", policy => policy.RequireAssertion(context =>
        ArcaScopeAuthorization.HasScope(context.User, "arca:consultar")));
    options.AddPolicy("ArcaFacturar", policy => policy.RequireAssertion(context =>
        ArcaScopeAuthorization.HasScope(context.User, "arca:facturar")));
});

builder.Services.AddMcpServer()
    .WithTools<ArcaTools>()
    .WithHttpTransport(options =>
    {
        options.SessionMode = HttpServerSessionMode.Stateless;
    })
    // Habilita que [Authorize]/[AllowAnonymous] en los métodos de ArcaTools se
    // respeten por-tool (sin esto, MapMcp().RequireAuthorization() solo exige
    // "autenticado", cualquier scope vale para cualquier tool).
    .AddAuthorizationFilters();

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

app.MapMcp().RequireAuthorization();

app.Run();

public partial class Program { }
