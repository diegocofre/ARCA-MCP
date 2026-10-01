/*
 * Copyright (c) 2025 Diego Cofré, DC Sistemas
 * www.diegocofre.com.ar
 *
 * Licensed under the Apache License, Version 2.0.
 * You may obtain a copy of the License at
 * http://www.apache.org/licenses/LICENSE-2.0
 */

using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Xml;
using dcArca.Core.Services.Logging;

namespace dcArca.Core.Services;

/// <summary>
/// Servicio de autenticación con WSAA de ARCA
/// Gestiona la generación de Login Ticket Requests y obtención de tokens
/// </summary>
public class dcArcaAuthService
{
    private readonly string _wsaaUrl;
    private readonly string _certificatePath;
    private readonly string _certificatePassword;
    private readonly string _cuit;
    private readonly string _serviceName;
    private readonly string _cacheKey;
    private readonly IWsaaTokenStore _tokenStore;
    private readonly IAfipLogger _logger;
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> _tokenLocks = new();
    private static readonly TimeSpan _renewalSkew = TimeSpan.FromMinutes(2);

    private static int _globalUniqueId = 0;

    private string? _token;
    private string? _sign;
    private DateTime _tokenExpiration;

    public dcArcaAuthService(
        string wsaaUrl,
        string certificatePath,
        string certificatePassword,
        string cuit,
        string serviceName = "wsfe",
        IAfipLogger? logger = null,
        IWsaaTokenStore? tokenStore = null)
    {
        _wsaaUrl = wsaaUrl;
        _certificatePath = certificatePath;
        _certificatePassword = certificatePassword;
        _cuit = cuit;
        _serviceName = serviceName;
        _tokenExpiration = DateTime.MinValue;
        _cacheKey = $"{_cuit}_{_serviceName}";
        _logger = logger ?? NoOpAfipLogger.Instance;
        _tokenStore = tokenStore ?? MemoryWsaaTokenStore.Shared;
    }

    /// <summary>
    /// Obtiene un token válido. Si el token actual expiró, solicita uno nuevo.
    /// </summary>
    public async Task<(string token, string sign)> GetTokenAsync(CancellationToken cancellationToken = default)
    {
        // Fast path: token presente y dentro de ventana de validez (con margen de seguridad)
    if (!string.IsNullOrEmpty(_token) && !string.IsNullOrEmpty(_sign) && DateTime.UtcNow < _tokenExpiration - _renewalSkew)
        {
            _logger.LogInformation($"[dcAuthService] Token válido hasta {_tokenExpiration}");
            return (_token, _sign);
        }

        var gate = _tokenLocks.GetOrAdd(_cacheKey, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            await using var storeLock = await _tokenStore.AcquireLockAsync(_cacheKey, cancellationToken);

            // Double-check tras adquirir ambos locks: otro hilo/proceso pudo refrescar y guardar.
            await LoadTokenFromStoreAsync(cancellationToken);
            if (!string.IsNullOrEmpty(_token) && !string.IsNullOrEmpty(_sign) && DateTime.UtcNow < _tokenExpiration - _renewalSkew)
            {
                _logger.LogInformation($"[dcAuthService] Token válido (post-lock) hasta {_tokenExpiration}");
                return (_token, _sign);
            }

            _logger.LogInformation("[dcAuthService] Solicitando nuevo token WSAA...");
            await RequestNewTokenAsync(cancellationToken);
            return (_token!, _sign!);
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>
    /// Invalida el token rechazado, salvo que otra llamada ya lo haya renovado.
    /// </summary>
    public async Task InvalidateCacheAsync(string rejectedToken, CancellationToken cancellationToken = default)
    {
        var gate = _tokenLocks.GetOrAdd(_cacheKey, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            await using var storeLock = await _tokenStore.AcquireLockAsync(_cacheKey, cancellationToken);
            await LoadTokenFromStoreAsync(cancellationToken);
            if (!string.IsNullOrEmpty(_token) && _token != rejectedToken
                && !string.IsNullOrEmpty(_sign) && DateTime.UtcNow < _tokenExpiration - _renewalSkew)
            {
                return;
            }

            _token = null;
            _sign = null;
            _tokenExpiration = DateTime.MinValue;
            await _tokenStore.RemoveAsync(_cacheKey, cancellationToken);
            _logger.LogInformation("[dcAuthService] Cache WSAA invalidado");
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task RequestNewTokenAsync(CancellationToken cancellationToken)
    {
        try
        {
            // 1. Crear el Login Ticket Request (TRA)
            var loginTicketRequest = CreateLoginTicketRequest();
            _logger.LogDebug("[dcAuthService] Login Ticket Request creado.");
            _logger.LogTrace(loginTicketRequest);

            // 2. Firmar el TRA con el certificado
            var signedTra = SignLoginTicketRequest(loginTicketRequest);
            _logger.LogInformation("[dcAuthService] TRA firmado correctamente");
            _logger.LogDebug($"[dcAuthService] CMS length: {signedTra.Length} chars");

            // 3. Enviar al WSAA y obtener respuesta
            var response = await SendToWsaaAsync(signedTra, cancellationToken);
            _logger.LogInformation("[dcAuthService] Respuesta recibida de WSAA");

            // 4. Extraer token, sign y expiration
            ParseWsaaResponse(response);
            await SaveTokenStoreAsync(cancellationToken);
            _logger.LogInformation($"[dcAuthService] Token obtenido. Válido hasta {_tokenExpiration}");
        }
        catch (Exception ex)
        {
            if (ex.Message.Contains("coe.alreadyAuthenticated", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("[dcAuthService] WSAA indica que ya existe un TA válido. Reutilizando cache si está disponible.");
                await LoadTokenFromStoreAsync(cancellationToken);
                if (!string.IsNullOrEmpty(_token) && DateTime.UtcNow < _tokenExpiration)
                {
                    _logger.LogInformation($"[dcAuthService] Token en cache vigente hasta {_tokenExpiration}");
                    return;
                }
            }

            _logger.LogError($"[dcAuthService] Error al obtener token: {ex.Message}", ex);
            throw;
        }
    }

    private string CreateLoginTicketRequest()
    {
    var baseId = (uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    var uniqueId = unchecked((uint)(baseId + (uint)Interlocked.Increment(ref _globalUniqueId))); 
    var nowUtc = DateTime.UtcNow;
    var generationTime = nowUtc.AddMinutes(-10).ToString("yyyy-MM-ddTHH:mm:ss'Z'", CultureInfo.InvariantCulture);
    var expirationTime = nowUtc.AddMinutes(+10).ToString("yyyy-MM-ddTHH:mm:ss'Z'", CultureInfo.InvariantCulture);

        // IMPORTANTE: NO incluir <?xml...?> - el schema de WSAA espera solo el elemento raíz
        var tra = $@"<loginTicketRequest version=""1.0"">
<header>
<uniqueId>{uniqueId}</uniqueId>
<generationTime>{generationTime}</generationTime>
<expirationTime>{expirationTime}</expirationTime>
</header>
<service>{_serviceName}</service>
</loginTicketRequest>";

        return tra;
    }

    private string SignLoginTicketRequest(string loginTicketRequest)
    {
        // Cargar certificado - usar PersistKeySet como en ejemplo oficial AFIP
        X509Certificate2 certificate;
        try
        {
            if (string.IsNullOrEmpty(_certificatePassword))
            {
                // Intentar cargar PFX sin contraseña (leer bytes y crear certificado sin password)
                var raw = File.ReadAllBytes(_certificatePath);
                certificate = X509CertificateLoader.LoadPkcs12(raw, (string?)null, X509KeyStorageFlags.PersistKeySet);
            }
            else
            {
                certificate = X509CertificateLoader.LoadPkcs12FromFile(_certificatePath, _certificatePassword, X509KeyStorageFlags.PersistKeySet);
            }
        }
        catch (System.Security.Cryptography.CryptographicException ex)
        {
            // En algunos entornos ciertos PFX sin contraseña requieren pasar cadena vacía en lugar de null.
            if (string.IsNullOrEmpty(_certificatePassword))
            {
                try
                {
                    certificate = X509CertificateLoader.LoadPkcs12FromFile(_certificatePath, string.Empty, X509KeyStorageFlags.PersistKeySet);
                }
                catch (System.Security.Cryptography.CryptographicException)
                {
                    throw new Exception($"No se pudo cargar el certificado .pfx sin contraseña. Verifique la ruta '{_certificatePath}' y el formato del archivo. Detalle: {ex.Message}", ex);
                }
            }
            else
            {
                throw new Exception($"No se pudo cargar el certificado .pfx con la contraseña proporcionada. Verifique la ruta '{_certificatePath}' y la contraseña. Detalle: {ex.Message}", ex);
            }
        }

        // Validar que el certificado no esté vencido
        var nowUtc = DateTime.UtcNow;
        if (nowUtc < certificate.NotBefore.ToUniversalTime() || nowUtc > certificate.NotAfter.ToUniversalTime())
        {
            throw new Exception($"El certificado está vencido o no es válido aún. " +
                              $"Válido desde {certificate.NotBefore:dd/MM/yyyy} hasta {certificate.NotAfter:dd/MM/yyyy}");
        }

        _logger.LogInformation($"[dcAuthService] Certificado: {certificate.Subject}");
        _logger.LogInformation($"[dcAuthService] Thumbprint: {certificate.Thumbprint}");
        _logger.LogInformation($"[dcAuthService] Válido hasta: {certificate.NotAfter:dd/MM/yyyy HH:mm}");

        // Firmar el Login Ticket Request 
        try
        {
            var msgBytes = Encoding.UTF8.GetBytes(loginTicketRequest);
            var contentInfo = new System.Security.Cryptography.Pkcs.ContentInfo(msgBytes);
            var signedCms = new System.Security.Cryptography.Pkcs.SignedCms(contentInfo);

            var cmsSigner = new System.Security.Cryptography.Pkcs.CmsSigner(certificate);
            cmsSigner.IncludeOption = System.Security.Cryptography.X509Certificates.X509IncludeOption.EndCertOnly;

            signedCms.ComputeSignature(cmsSigner);

            _logger.LogInformation("[dcAuthService] Mensaje firmado con PKCS#7");

            var encodedSignedCms = signedCms.Encode();
            return Convert.ToBase64String(encodedSignedCms);
        }
        catch (Exception ex)
        {
            throw new Exception($"Error al firmar el LoginTicketRequest: {ex.Message}", ex);
        }
    }

    private async Task<string> SendToWsaaAsync(string signedTra, CancellationToken cancellationToken)
    {
        var soapEnvelope = $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<soap:Envelope xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/"" xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" xmlns:xsd=""http://www.w3.org/2001/XMLSchema"">
    <soap:Body>
        <loginCms xmlns=""http://wsaa.view.sua.dvadac.desein.afip.gov"">
            <in0>{signedTra}</in0>
        </loginCms>
    </soap:Body>
</soap:Envelope>";

        using var httpClient = new HttpClient();
        httpClient.Timeout = TimeSpan.FromSeconds(30);
        
        var content = new StringContent(soapEnvelope, Encoding.UTF8, "text/xml");
        content.Headers.Add("SOAPAction", "");

    var response = await httpClient.PostAsync(_wsaaUrl, content, cancellationToken);
        var responseText = await response.Content.ReadAsStringAsync();

    if (!response.IsSuccessStatusCode)
        {
            try
            {
                var xmlDoc = new XmlDocument();
                xmlDoc.LoadXml(responseText);

                var nsmgr = new XmlNamespaceManager(xmlDoc.NameTable);
                nsmgr.AddNamespace("soap", "http://schemas.xmlsoap.org/soap/envelope/");

                var faultCode = xmlDoc.SelectSingleNode("//soap:Fault/faultcode", nsmgr)?.InnerText
                    ?? xmlDoc.SelectSingleNode("//faultcode")?.InnerText;
                var faultString = xmlDoc.SelectSingleNode("//soap:Fault/faultstring", nsmgr)?.InnerText
                    ?? xmlDoc.SelectSingleNode("//faultstring")?.InnerText;
                var detailNode = xmlDoc.SelectSingleNode("//soap:Fault/detail", nsmgr)
                    ?? xmlDoc.SelectSingleNode("//detail");
                var faultDetail = detailNode?.InnerXml;

                throw new dcWsaaFaultException((int)response.StatusCode, faultCode, faultString, faultDetail, responseText);
            }
            catch (XmlException)
            {
                throw new dcWsaaFaultException((int)response.StatusCode, null, $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}", responseText, responseText);
            }
        }

        return responseText;
    }

    private void ParseWsaaResponse(string xmlResponse)
    {
        var xmlDoc = new XmlDocument();
        xmlDoc.LoadXml(xmlResponse);

        var nsmgr = new XmlNamespaceManager(xmlDoc.NameTable);
        nsmgr.AddNamespace("soap", "http://schemas.xmlsoap.org/soap/envelope/");
        nsmgr.AddNamespace("ns1", "http://wsaa.view.sua.dvadac.desein.afip.gov");

        // Extraer el LoginCmsReturn que está en base64
        var loginCmsReturnNode = xmlDoc.SelectSingleNode("//ns1:loginCmsReturn", nsmgr);
        if (loginCmsReturnNode == null)
            throw new Exception("No se pudo obtener loginCmsReturn del WSAA");

        var loginCmsRaw = loginCmsReturnNode.InnerText.Trim();
        string loginCmsReturnXml;

        try
        {
            loginCmsReturnXml = Encoding.UTF8.GetString(Convert.FromBase64String(loginCmsRaw));
        }
        catch (FormatException)
        {
            // WSAA puede devolver el XML escapado en lugar de Base64
            loginCmsReturnXml = WebUtility.HtmlDecode(loginCmsRaw);
        }

        // Parsear el XML interno
        var loginDoc = new XmlDocument();
        loginDoc.LoadXml(loginCmsReturnXml);

        _token = loginDoc.SelectSingleNode("//token")?.InnerText 
            ?? throw new Exception("Token no encontrado en respuesta WSAA");
        
        _sign = loginDoc.SelectSingleNode("//sign")?.InnerText 
            ?? throw new Exception("Sign no encontrado en respuesta WSAA");

        var expirationStr = loginDoc.SelectSingleNode("//expirationTime")?.InnerText;
        if (string.IsNullOrWhiteSpace(expirationStr))
        {
            _tokenExpiration = DateTime.UtcNow.AddHours(12);
        }
        else if (DateTimeOffset.TryParse(expirationStr, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var expiration))
        {
            _tokenExpiration = expiration.UtcDateTime;
        }
        else
        {
            _tokenExpiration = DateTime.UtcNow.AddHours(12);
        }
    }

    private async Task LoadTokenFromStoreAsync(CancellationToken cancellationToken)
    {
        try
        {
            var cached = await _tokenStore.ReadAsync(_cacheKey, cancellationToken);
            if (cached == null)
            {
                return;
            }

            if (DateTime.UtcNow >= cached.Expiration)
            {
                await _tokenStore.RemoveAsync(_cacheKey, cancellationToken);
                return;
            }

            _token = cached.Token;
            _sign = cached.Sign;
            _tokenExpiration = cached.Expiration;
            _logger.LogInformation($"[dcAuthService] Token cargado desde cache. Válido hasta {_tokenExpiration}");
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"[dcAuthService] No se pudo leer el cache de WSAA: {ex.Message}");
        }
    }

    private async Task SaveTokenStoreAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrEmpty(_token) || string.IsNullOrEmpty(_sign))
            {
                return;
            }

            await _tokenStore.WriteAsync(
                _cacheKey,
                new WsaaTokenEntry(_token, _sign, _tokenExpiration),
                cancellationToken);

            _logger.LogInformation($"[dcAuthService] Token cacheado hasta {_tokenExpiration}");
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"[dcAuthService] No se pudo guardar el cache de WSAA: {ex.Message}");
        }
    }

}