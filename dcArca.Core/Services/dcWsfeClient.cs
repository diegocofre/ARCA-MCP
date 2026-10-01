/*
 * Copyright (c) 2025 Diego Cofré Sistemas
 * www.diegocofre.com.ar
 *
 * Licensed under the Apache License, Version 2.0.
 * You may obtain a copy of the License at
 * http://www.apache.org/licenses/LICENSE-2.0
 */

using System.Globalization;
using System.Text;
using System.Xml;
using dcArca.Core.Models;
using dcArca.Core.Services.Logging;

namespace dcArca.Core.Services;

/// <summary>
/// Cliente para el servicio WSFEv1 de ARCA (Facturación Electrónica)
/// </summary>
public class dcWsfeClient : IdcWsfeClient, IDisposable
{
    private readonly string _wsfeUrl;
    private readonly dcArcaAuthService _authService;
    private readonly dcArcaConfig _config;
    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;
    private readonly IAfipLogger _logger;
    private readonly dcWsfeSoapBuilder _soapBuilder;
    private readonly dcWsfeSoapParser _soapParser;
    private bool _disposed;

    /// <summary>
    /// Crea una nueva instancia del cliente WSFE.
    /// </summary>
    /// <param name="config">Configuración de ARCA.</param>
    /// <param name="authService">Servicio de autenticación a reutilizar (opcional).</param>
    /// <param name="httpClient">Instancia de <see cref="HttpClient"/> (opcional).</param>
    /// <param name="logger">Logger a utilizar (opcional).</param>
    public dcWsfeClient(dcArcaConfig config, dcArcaAuthService? authService = null, HttpClient? httpClient = null, IAfipLogger? logger = null)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _wsfeUrl = config.WsfeUrl;
        _logger = logger ?? NoOpAfipLogger.Instance;
        _authService = authService ?? new dcArcaAuthService(
                config.WsaaUrl,
                config.CertificatePath,
                config.CertificatePassword,
                config.Cuit,
                logger: _logger
            );
        if (httpClient != null)
        {
            _httpClient = httpClient;
            _ownsHttpClient = false;
        }
        else
        {
            _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            _ownsHttpClient = true;
        }
        _soapBuilder = new dcWsfeSoapBuilder(_config);
        _soapParser = new dcWsfeSoapParser(_logger);
    }

    /// <summary>
    /// Obtiene el último comprobante autorizado para el tipo de comprobante indicado.
    /// </summary>
    /// <param name="tipoComprobante">Tipo de comprobante a consultar.</param>
    /// <param name="cancellationToken">Token de cancelación opcional.</param>
    /// <returns>Respuesta con el número de comprobante o detalle de error.</returns>
    public async Task<dcFacturaResponse> FECompUltimoAutorizadoAsync(dcTipoComprobante tipoComprobante, CancellationToken cancellationToken = default)
    {
        var rejectedToken = string.Empty;
        try
        {
            var tipo = (int)tipoComprobante;
            _logger.LogInformation($"[dcWsfeClient] Consultando último comprobante autorizado (PV: {_config.PuntoVenta}, Tipo: {tipo})");
            var (token, sign) = await _authService.GetTokenAsync(cancellationToken);
            rejectedToken = token;

            var soapRequest = _soapBuilder.BuildUltimoComprobanteRequest(token, sign, tipo);
            var response = await SendSoapRequestAsync(soapRequest, "http://ar.gov.afip.dif.FEV1/FECompUltimoAutorizado", cancellationToken);
            var parsed = _soapParser.ParseUltimoComprobanteResponse(response, tipo);
            if (parsed.Success)
            {
                _logger.LogInformation($"[dcWsfeClient] Último comprobante autorizado: {parsed.NumeroComprobante}");
            }
            else
            {
                _logger.LogError($"[dcWsfeClient] Error al consultar último comprobante: {parsed.Mensaje}");
            }

            return parsed;
        }
        catch (dcTokenInvalidException)
        {
            _logger.LogWarning("[dcWsfeClient] Token inválido detectado, refrescando...");
            try
            {
                await _authService.InvalidateCacheAsync(rejectedToken, cancellationToken);
                var tipo = (int)tipoComprobante;
                var (token, sign) = await _authService.GetTokenAsync(cancellationToken);
                var soapRequest = _soapBuilder.BuildUltimoComprobanteRequest(token, sign, tipo);
                var response = await SendSoapRequestAsync(soapRequest, "http://ar.gov.afip.dif.FEV1/FECompUltimoAutorizado", cancellationToken);
                var parsed = _soapParser.ParseUltimoComprobanteResponse(response, tipo);
                _logger.LogInformation($"[dcWsfeClient] Reintento exitoso. Último comprobante autorizado: {parsed.NumeroComprobante}");
                return parsed;
            }
            catch (Exception retryEx)
            {
                _logger.LogError($"[dcWsfeClient] Error tras reintento por token inválido: {retryEx.Message}", retryEx);
                return CrearRespuestaValidacion("FEULTIMO_ERROR", $"Error tras reintento por token inválido: {retryEx.Message}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"[dcWsfeClient] Excepción en FECompUltimoAutorizado: {ex.Message}", ex);
            return CrearRespuestaValidacion("FEULTIMO_ERROR", $"Error al consultar último comprobante: {ex.Message}");
        }
    }

    /// <summary>
    /// Consulta en ARCA los datos de un comprobante específico, con tipo y número.
    /// </summary>
    /// <param name="numeroComprobante">Número del comprobante.</param>
    /// <param name="tipoComprobante">Tipo de comprobante a consultar.</param>
    /// <param name="cancellationToken">Token de cancelación opcional.</param>
    /// <returns>Respuesta con los datos del comprobante.</returns>
    public async Task<dcFacturaResponse> FECompConsultarAsync(long numeroComprobante, dcTipoComprobante tipoComprobante, CancellationToken cancellationToken = default)
    {
        var rejectedToken = string.Empty;
        try
        {
            var tipo = (int)tipoComprobante;
            _logger.LogInformation($"[dcWsfeClient] Consultando comprobante {numeroComprobante} (Tipo: {tipo})...");

            var (token, sign) = await _authService.GetTokenAsync(cancellationToken);
            rejectedToken = token;
            var soapRequest = _soapBuilder.BuildConsultarComprobanteRequest(token, sign, tipo, numeroComprobante);
            var response = await SendSoapRequestAsync(soapRequest, "http://ar.gov.afip.dif.FEV1/FECompConsultar", cancellationToken);
            var parsed = _soapParser.ParseFECompConsultarResponse(response, numeroComprobante);
            if (!parsed.Success)
            {
                _logger.LogWarning($"[dcWsfeClient] Consulta de comprobante {numeroComprobante} (tipo {tipo}) sin éxito: {parsed.Mensaje}");
            }

            return parsed;
        }
        catch (dcTokenInvalidException)
        {
            // Token inválido detectado, invalidar cache y reintentar una vez
            _logger.LogWarning("[dcWsfeClient] Token inválido detectado, refrescando...");
            try
            {
                await _authService.InvalidateCacheAsync(rejectedToken, cancellationToken);
                var tipo = (int)tipoComprobante;
                var (token, sign) = await _authService.GetTokenAsync(cancellationToken);
                var soapRequest = _soapBuilder.BuildConsultarComprobanteRequest(token, sign, tipo, numeroComprobante);
                var response = await SendSoapRequestAsync(soapRequest, "http://ar.gov.afip.dif.FEV1/FECompConsultar", cancellationToken);
                var parsed = _soapParser.ParseFECompConsultarResponse(response, numeroComprobante);
                _logger.LogInformation($"[dcWsfeClient] Reintento exitoso para consulta de comprobante {numeroComprobante}");
                return parsed;
            }
            catch (Exception retryEx)
            {
                _logger.LogError($"[dcWsfeClient] Error tras reintento por token inválido: {retryEx.Message}", retryEx);
                var respuesta = CrearRespuestaValidacion("FECOMP_ERROR", $"Error tras reintento por token inválido: {retryEx.Message}");
                respuesta.NumeroComprobante = numeroComprobante;
                return respuesta;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"[dcWsfeClient] Excepción en FECompConsultar: {ex.Message}", ex);
            var respuesta = CrearRespuestaValidacion("FECOMP_ERROR", $"Error al consultar comprobante: {ex.Message}");
            respuesta.NumeroComprobante = numeroComprobante;
            return respuesta;
        }
    }

    /// <summary>
    /// Solicita la autorización de una factura electrónica (CAE)
    /// </summary>
    /// <param name="factura">Factura a autorizar.</param>
    /// <param name="cancellationToken">Token de cancelación opcional.</param>
    /// <returns>Respuesta con el CAE o detalles del rechazo.</returns>
    public async Task<dcFacturaResponse> FECAESolicitarAsync(dcFacturaRequest factura, CancellationToken cancellationToken = default)
    {
        var rejectedToken = string.Empty;
        try
        {
            _logger.LogInformation("[dcWsfeClient] Solicitando CAE para factura...");

            if (factura == null)
            {
                return CrearRespuestaValidacion("FACTURA_NULL", "No se recibieron datos de la factura a autorizar.");
            }

            if (!factura.TieneTipoComprobanteValido())
            {
                return CrearRespuestaValidacion("TIPOC_INVALID", "El Tipo de Comprobante es obligatorio y debe corresponder a un valor válido definido por AFIP.");
            }

            if (!factura.TieneNumeroComprobanteValido())
            {
                return CrearRespuestaValidacion("NROC_INVALID", "El Número de Comprobante es obligatorio y debe ser mayor a 0.");
            }

            if (!factura.TieneConceptoValido() || factura.Concepto == null)
            {
                return CrearRespuestaValidacion("CONCEPTO_MISSING", "Concepto es obligatorio y debe ser uno de los valores soportados (1=Productos, 2=Servicios, 3=Productos y Servicios).");
            }
            var concepto = (int)factura.Concepto!.Value;

            if (concepto != 1)
            {
                if (string.IsNullOrWhiteSpace(factura.FechaServicioDesde))
                {
                    return CrearRespuestaValidacion("FCHSD_REQUIRED", "FechaServicioDesde es obligatoria para comprobantes de servicios (Concepto 2 o 3).");
                }

                if (string.IsNullOrWhiteSpace(factura.FechaServicioHasta))
                {
                    return CrearRespuestaValidacion("FCHSH_REQUIRED", "FechaServicioHasta es obligatoria para comprobantes de servicios (Concepto 2 o 3).");
                }

                if (string.IsNullOrWhiteSpace(factura.FechaVencimiento))
                {
                    return CrearRespuestaValidacion("FCHVTO_REQUIRED", "FechaVencimiento es obligatoria para comprobantes de servicios (Concepto 2 o 3).");
                }
            }

            var fiscalValidation = dcFacturaFiscalValidator.Validate(factura);
            if (!fiscalValidation.IsValid)
            {
                return CrearRespuestaValidacion(
                    fiscalValidation.Code ?? "FISCAL_INVALID",
                    fiscalValidation.Message ?? "El desglose fiscal del comprobante no es consistente.");
            }

            if (string.IsNullOrWhiteSpace(factura.FechaComprobante))
            {
                return CrearRespuestaValidacion("FCHCBTE_REQUIRED", "FechaComprobante es obligatoria y debe tener formato YYYYMMDD.");
            }
            if (!DateTime.TryParseExact(factura.FechaComprobante, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
            {
                return CrearRespuestaValidacion("FCHCBTE_INVALID", "FechaComprobante no tiene el formato válido YYYYMMDD.");
            }

            var documentoValidation = dcDocumentoReceptorValidator.Validate(factura.TipoDocReceptor, factura.CuitReceptor);
            if (!documentoValidation.IsValid)
            {
                return CrearRespuestaValidacion(
                    documentoValidation.Code ?? "DOC_INVALID",
                    documentoValidation.Message ?? "El documento del receptor no es válido.");
            }

            var tipoComprobanteEnum = factura.TipoComprobante!.Value;
            var tipoComprobante = (int)tipoComprobanteEnum;
            var nroComprobante = factura.NumeroComprobante!.Value;
            var claseComprobante = ObtenerClaseComprobante(tipoComprobante);

            if (factura.EsNota() && !factura.CumpleReglaNotas10197())
            {
                return CrearRespuestaValidacion("NOTA_CBTEASOC_10197", "Notas de Débito / Crédito deben informar comprobante asociado (CbteAsoc) o periodo asociado (PeriodoAsoc) - Error 10197.");
            }

            var (token, sign) = await _authService.GetTokenAsync(cancellationToken);
            rejectedToken = token;

            if (factura.CondicionIvaReceptor == null)
            {
                _logger.LogWarning("[dcWsfeClient] No se especificó Condición IVA; se enviará vacío y AFIP determinará el resultado.");
            }

            _logger.LogDebug($"[dcWsfeClient] Tipo Comprobante: {tipoComprobante} ({tipoComprobanteEnum}) (Clase: {claseComprobante ?? "N/D"})");
            _logger.LogDebug($"[dcWsfeClient] Condición IVA enviada: {factura.CondicionIvaReceptor}");
            _logger.LogDebug($"[dcWsfeClient] CUIT Receptor: {factura.CuitReceptor}");

            try
            {
                var soapRequest = _soapBuilder.BuildSolicitarCaeRequest(token, sign, factura, nroComprobante, tipoComprobante, concepto);
                var response = await SendSoapRequestAsync(soapRequest, "http://ar.gov.afip.dif.FEV1/FECAESolicitar", cancellationToken);
                var result = _soapParser.ParseFECAESolicitarResponse(response, nroComprobante);

                result.EmissionOutcome = result.Success
                    ? dcEmissionOutcome.Authorized
                    : dcEmissionOutcome.FiscalRejected;

                if (result.Success)
                {
                    _logger.LogInformation($"[dcWsfeClient] CAE obtenido exitosamente: {result.Cae}");
                }
                else
                {
                    _logger.LogError($"[dcWsfeClient] Error al obtener CAE: {result.Mensaje}");
                }

                return result;
            }
            catch (HttpRequestException transportEx)
            {
                return await ReconcileAmbiguousEmissionAsync(
                    transportEx, nroComprobante, tipoComprobanteEnum, cancellationToken);
            }
            catch (TaskCanceledException transportEx) when (!cancellationToken.IsCancellationRequested)
            {
                return await ReconcileAmbiguousEmissionAsync(
                    transportEx, nroComprobante, tipoComprobanteEnum, CancellationToken.None);
            }
            catch (dcTokenInvalidException)
            {
                _logger.LogWarning("[dcWsfeClient] Token inválido detectado, refrescando...");
                try
                {
                    await _authService.InvalidateCacheAsync(rejectedToken, cancellationToken);
                    var newToken = await _authService.GetTokenAsync(cancellationToken);
                    var soapRequest = _soapBuilder.BuildSolicitarCaeRequest(newToken.token, newToken.sign, factura, nroComprobante, tipoComprobante, concepto);
                    var response = await SendSoapRequestAsync(soapRequest, "http://ar.gov.afip.dif.FEV1/FECAESolicitar", cancellationToken);
                    var result = _soapParser.ParseFECAESolicitarResponse(response, nroComprobante);
                    result.EmissionOutcome = result.Success
                        ? dcEmissionOutcome.Authorized
                        : dcEmissionOutcome.FiscalRejected;
                    _logger.LogInformation($"[dcWsfeClient] Reintento exitoso para solicitud de CAE");
                    return result;
                }
                catch (HttpRequestException transportEx)
                {
                    return await ReconcileAmbiguousEmissionAsync(
                        transportEx, nroComprobante, tipoComprobanteEnum, cancellationToken);
                }
                catch (TaskCanceledException transportEx) when (!cancellationToken.IsCancellationRequested)
                {
                    return await ReconcileAmbiguousEmissionAsync(
                        transportEx, nroComprobante, tipoComprobanteEnum, CancellationToken.None);
                }
                catch (Exception retryEx)
                {
                    _logger.LogError($"[dcWsfeClient] Error tras reintento por token inválido: {retryEx.Message}", retryEx);
                    return CrearRespuestaValidacion("FECAESOLICITAR_ERROR", $"Error tras reintento por token inválido: {retryEx.Message}");
                }
            }
        }
        catch (dcTokenInvalidException ex)
        {
            _logger.LogError($"[dcWsfeClient] Token inválido en FECAESolicitar: {ex.Message}", ex);
            return CrearRespuestaValidacion("FECAESOLICITAR_ERROR", $"Token inválido: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError($"[dcWsfeClient] Excepción en FECAESolicitar: {ex.Message}", ex);
            return CrearRespuestaValidacion("FECAESOLICITAR_ERROR", $"Error al solicitar CAE: {ex.Message}");
        }
    }

    private async Task<dcFacturaResponse> ReconcileAmbiguousEmissionAsync(
        Exception transportException,
        long numeroComprobante,
        dcTipoComprobante tipoComprobante,
        CancellationToken cancellationToken)
    {
        _logger.LogWarning(
            $"[dcWsfeClient] Resultado de emisión incierto por error de transporte. Reconciliando comprobante {numeroComprobante} tipo {(int)tipoComprobante} antes de cualquier reintento.");

        try
        {
            var reconciliation = await FECompConsultarAsync(
                numeroComprobante,
                tipoComprobante,
                cancellationToken);

            if (reconciliation.Success)
            {
                reconciliation.Success = true;
                reconciliation.EmissionOutcome = dcEmissionOutcome.RecoveredSuccess;
                reconciliation.Codigo = "FECAE_RECOVERED";
                reconciliation.Mensaje = "Emisión autorizada por ARCA; resultado recuperado mediante FECompConsultar tras un error de transporte.";
                _logger.LogInformation(
                    $"[dcWsfeClient] Emisión recuperada por reconciliación. Comprobante {numeroComprobante}, CAE {reconciliation.Cae}.");
                return reconciliation;
            }

            var uncertain = CrearRespuestaValidacion(
                "FECAE_OUTCOME_UNCERTAIN",
                "El resultado de la emisión es incierto. ARCA no pudo confirmar el comprobante durante la reconciliación; no se realizó un reintento automático.");
            uncertain.NumeroComprobante = numeroComprobante;
            uncertain.EmissionOutcome = dcEmissionOutcome.Uncertain;
            uncertain.Errores.Add($"Error de transporte original: {transportException.Message}");
            if (!string.IsNullOrWhiteSpace(reconciliation.Mensaje))
            {
                uncertain.Errores.Add($"Reconciliación: {reconciliation.Mensaje}");
            }
            return uncertain;
        }
        catch (Exception reconciliationEx)
        {
            var uncertain = CrearRespuestaValidacion(
                "FECAE_OUTCOME_UNCERTAIN",
                "El resultado de la emisión es incierto y la consulta de reconciliación también falló; no se realizó un reintento automático.");
            uncertain.NumeroComprobante = numeroComprobante;
            uncertain.EmissionOutcome = dcEmissionOutcome.Uncertain;
            uncertain.Errores.Add($"Error de transporte original: {transportException.Message}");
            uncertain.Errores.Add($"Error de reconciliación: {reconciliationEx.Message}");
            return uncertain;
        }
    }

    public Task<dcFacturaResponse> SolicitarCaeAsync(dcFacturaRequest factura, CancellationToken cancellationToken = default)
        => FECAESolicitarAsync(factura, cancellationToken);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        if (_ownsHttpClient)
        {
            _httpClient.Dispose();
        }

        _disposed = true;
        GC.SuppressFinalize(this);
    }

    private async Task<string> SendSoapRequestAsync(string soapRequest, string soapAction, CancellationToken cancellationToken)
    {
        var content = new StringContent(soapRequest, Encoding.UTF8, "application/soap+xml");
        content.Headers.Remove("Content-Type");
        content.Headers.TryAddWithoutValidation("Content-Type", $"application/soap+xml; charset=utf-8; action=\"{soapAction}\"");

        _logger.LogTrace($"[dcWsfeClient] Enviando SOAP Action '{soapAction}'.");
        var response = await _httpClient.PostAsync(_wsfeUrl, content, cancellationToken);
        var responseText = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError($"[dcWsfeClient] Error HTTP {(int)response.StatusCode}: {responseText}");
            try
            {
                var soap = new XmlDocument();
                soap.LoadXml(responseText);
                if (soap.SelectSingleNode("//*[local-name()='Fault']") != null)
                {
                    return responseText;
                }
            }
            catch (XmlException)
            {
                // Una respuesta no SOAP conserva el error HTTP original.
            }
            throw new dcWsfeHttpException(response.StatusCode, responseText);
        }

        return responseText;
    }

    public async Task<List<dcCondicionIvaOption>> GetCondicionesIVAReceptorAsync(int docTipo, long docNro, dcTipoComprobante tipoComprobante, CancellationToken cancellationToken = default)
        => await ObtenerCondicionesIVAReceptorInternoAsync(docTipo, docNro, (int)tipoComprobante, cancellationToken);

    private async Task<List<dcCondicionIvaOption>> ObtenerCondicionesIVAReceptorInternoAsync(int docTipo, long docNro, int tipoComprobante, CancellationToken cancellationToken)
    {
        var rejectedToken = string.Empty;
        try
        {
            _logger.LogInformation($"[dcWsfeClient] Buscando Condiciones IVA receptor (DocTipo: {docTipo}, DocNro: {docNro})...");

            var (token, sign) = await _authService.GetTokenAsync(cancellationToken);
            rejectedToken = token;
            var soapRequest = _soapBuilder.BuildCondicionIvaRequest(token, sign, tipoComprobante, docTipo, docNro);
            var response = await SendSoapRequestAsync(soapRequest, "http://ar.gov.afip.dif.FEV1/FEParamGetCondicionIvaReceptor", cancellationToken);
            var clase = ObtenerClaseComprobante(tipoComprobante);
            var opciones = _soapParser.ParseCondicionIvaResponse(response);

            if (!string.IsNullOrWhiteSpace(clase))
            {
                opciones = opciones.Where(o => o.AplicaAClase(clase)).ToList();
            }

            if (opciones.Count > 0)
            {
                _logger.LogInformation($"[dcWsfeClient] Condiciones IVA disponibles: {FormatearCondicionesIva(opciones)}");
            }
            else
            {
                _logger.LogWarning("[dcWsfeClient] AFIP no devolvió Condiciones IVA para el receptor.");
            }

            return opciones;
        }
        catch (dcTokenInvalidException)
        {
            // Token inválido detectado, invalidar cache y reintentar una vez
            _logger.LogWarning("[dcWsfeClient] Token inválido detectado, refrescando...");
            try
            {
                await _authService.InvalidateCacheAsync(rejectedToken, cancellationToken);
                var (token, sign) = await _authService.GetTokenAsync(cancellationToken);
                var soapRequest = _soapBuilder.BuildCondicionIvaRequest(token, sign, tipoComprobante, docTipo, docNro);
                var response = await SendSoapRequestAsync(soapRequest, "http://ar.gov.afip.dif.FEV1/FEParamGetCondicionIvaReceptor", cancellationToken);
                var clase = ObtenerClaseComprobante(tipoComprobante);
                var opciones = _soapParser.ParseCondicionIvaResponse(response);

                if (!string.IsNullOrWhiteSpace(clase))
                {
                    opciones = opciones.Where(o => o.AplicaAClase(clase)).ToList();
                }

                _logger.LogInformation($"[dcWsfeClient] Reintento exitoso para Condiciones IVA");
                return opciones;
            }
            catch (Exception retryEx)
            {
                _logger.LogError($"[dcWsfeClient] Error tras reintento por token inválido: {retryEx.Message}", retryEx);
                return new List<dcCondicionIvaOption>();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"[dcWsfeClient] Error al consultar Condición IVA: {ex.Message}", ex);
            return new List<dcCondicionIvaOption>();
        }
    }

    private static string FormatearCondicionesIva(IEnumerable<dcCondicionIvaOption> opciones)
        => string.Join(", ", opciones.Select(o => $"{o.Id}-{o.Descripcion} ({o.ClasesComprobante})"));

    private static string? ObtenerClaseComprobante(int tipoComprobante)
    {
        return tipoComprobante switch
        {
            1 or 2 or 3 or 4 or 5 => "A",
            6 or 7 or 8 or 9 or 10 => "B",
            11 or 12 or 13 or 15 or 201 => "C",
            51 or 52 or 53 => "M",
            _ => null
        };
    }

    private static dcFacturaResponse CrearRespuestaValidacion(string codigo, string mensaje)
    {
        var response = new dcFacturaResponse
        {
            Success = false,
            Mensaje = mensaje,
            Codigo = codigo
        };
        response.Errores.Add(mensaje);
        return response;
    }
}
