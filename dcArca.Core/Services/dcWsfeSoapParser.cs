/*
 * Copyright (c) 2025 Diego Cofré Sistemas
 * www.diegocofre.com.ar
 *
 * Licensed under the Apache License, Version 2.0.
 * You may obtain a copy of the License at
 * http://www.apache.org/licenses/LICENSE-2.0
 */

using System.Globalization;
using System.Net;
using System.Xml;
using dcArca.Core.Models;
using dcArca.Core.Services.Logging;

namespace dcArca.Core.Services;

/// <summary>
/// Encapsula el parseo de las respuestas SOAP del servicio WSFEv1.
/// </summary>
public sealed class dcWsfeSoapParser
{
    private const string SoapEnvelopeNamespace = "http://www.w3.org/2003/05/soap-envelope";
    private const string WsfeNamespace = "http://ar.gov.afip.dif.FEV1/";

    private readonly IAfipLogger _logger;

    /// <summary>
    /// Crea un nuevo parser para respuestas SOAP del WSFE.
    /// </summary>
    /// <param name="logger">Logger a utilizar para mensajes de diagnóstico.</param>
    public dcWsfeSoapParser(IAfipLogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Procesa la respuesta de <c>FECompUltimoAutorizado</c>.
    /// </summary>
    /// <param name="xmlResponse">Contenido XML devuelto por AFIP.</param>
    /// <param name="tipoComprobante">Tipo de comprobante consultado.</param>
    /// <returns>Respuesta estandarizada para la capa superior.</returns>
    public dcFacturaResponse ParseUltimoComprobanteResponse(string xmlResponse, int tipoComprobante)
    {
        var (xmlDoc, nsmgr) = LoadXml(xmlResponse);
        var fault = ExtractFault(xmlDoc);
        if (fault != null)
        {
            if (dcTokenFaultDetector.IsInvalidSignature(fault.Reason))
            {
                throw new dcTokenInvalidException($"Token inválido detectado: {fault.Reason}");
            }
            return BuildFaultResponse(fault, $"Error al consultar el último comprobante autorizado para tipo {tipoComprobante}.");
        }

        var node = xmlDoc.SelectSingleNode("//ar:CbteNro", nsmgr);
        if (node == null)
        {
            return BuildErrorResponse("ULTOBT_NO_DATA", "La respuesta de AFIP no incluyó el nodo CbteNro.");
        }

        if (!long.TryParse(node.InnerText, out var ultimoNumero))
        {
            return BuildErrorResponse("ULTOBT_PARSE", $"No se pudo interpretar el número de comprobante devuelto: '{node.InnerText}'.");
        }

        return new dcFacturaResponse
        {
            Success = true,
            NumeroComprobante = ultimoNumero,
            Mensaje = "Último comprobante obtenido correctamente"
        };
    }

    /// <summary>
    /// Procesa la respuesta de <c>FECAESolicitar</c>.
    /// </summary>
    /// <param name="xmlResponse">Contenido XML devuelto por AFIP.</param>
    /// <param name="nroComprobante">Número de comprobante solicitado.</param>
    /// <returns>Respuesta estandarizada para la capa superior.</returns>
    public dcFacturaResponse ParseFECAESolicitarResponse(string xmlResponse, long nroComprobante)
    {
        var (xmlDoc, nsmgr) = LoadXml(xmlResponse);
        var fault = ExtractFault(xmlDoc);
        if (fault != null)
        {
            if (dcTokenFaultDetector.IsInvalidSignature(fault.Reason))
            {
                throw new dcTokenInvalidException($"Token inválido detectado: {fault.Reason}");
            }
            var response = BuildFaultResponse(fault, "La solicitud de CAE fue rechazada por AFIP.");
            response.NumeroComprobante = nroComprobante;
            return response;
        }

        var result = new dcFacturaResponse { NumeroComprobante = nroComprobante };
        result.Resultado = xmlDoc.SelectSingleNode("//ar:FeDetResp/ar:FECAEDetResponse/ar:Resultado", nsmgr)?.InnerText
            ?? xmlDoc.SelectSingleNode("//ar:FeCabResp/ar:Resultado", nsmgr)?.InnerText
            ?? string.Empty;

        foreach (var error in ExtractMessages(xmlDoc, nsmgr, "//ar:Err"))
        {
            result.Errores.Add(error);
        }

        foreach (var obs in ExtractMessages(xmlDoc, nsmgr, "//ar:Obs"))
        {
            result.Observaciones.Add(obs);
            _logger.LogWarning($"[dcWsfeParser] Observación AFIP: {obs}");
        }

        foreach (var evt in ExtractMessages(xmlDoc, nsmgr, "//ar:Evt", prefix: "Evento "))
        {
            result.Observaciones.Add(evt);
        }

        var caeNode = xmlDoc.SelectSingleNode("//ar:CAE", nsmgr);
        var caeVtoNode = xmlDoc.SelectSingleNode("//ar:CAEFchVto", nsmgr);
        var autorizado = string.Equals(result.Resultado, "A", StringComparison.OrdinalIgnoreCase);

        if (autorizado && caeNode != null && !string.IsNullOrWhiteSpace(caeNode.InnerText))
        {
            result.Success = true;
            result.Cae = WebUtility.HtmlDecode(caeNode.InnerText);
            result.CaeVencimiento = WebUtility.HtmlDecode(caeVtoNode?.InnerText ?? string.Empty);
            result.Mensaje = "CAE obtenido exitosamente";
            _logger.LogInformation($"[dcWsfeParser] CAE recibido: {result.Cae} (vence {result.CaeVencimiento}).");
        }
        else
        {
            result.Success = false;
            var mensajeBase = !string.IsNullOrEmpty(result.Resultado)
                ? $"Solicitud rechazada por AFIP (Resultado: {result.Resultado})."
                : "No se pudo obtener el CAE.";
            var detalle = result.Errores.Count > 0 ? string.Join("; ", result.Errores) : null;
            result.Mensaje = detalle == null ? mensajeBase : $"{mensajeBase} Detalles: {detalle}";
            _logger.LogError(result.Mensaje);
        }

        return result;
    }

    /// <summary>
    /// Procesa la respuesta de <c>FECompConsultar</c>.
    /// </summary>
    /// <param name="xmlResponse">Contenido XML devuelto por AFIP.</param>
    /// <param name="nroComprobante">Número de comprobante consultado.</param>
    /// <returns>Respuesta estandarizada.</returns>
    public dcFacturaResponse ParseFECompConsultarResponse(string xmlResponse, long nroComprobante)
    {
        var (xmlDoc, nsmgr) = LoadXml(xmlResponse);
        var fault = ExtractFault(xmlDoc);
        if (fault != null)
        {
            if (dcTokenFaultDetector.IsInvalidSignature(fault.Reason))
            {
                throw new dcTokenInvalidException($"Token inválido detectado: {fault.Reason}");
            }
            var response = BuildFaultResponse(fault, "La consulta de comprobante fue rechazada por AFIP.");
            response.NumeroComprobante = nroComprobante;
            return response;
        }

        var result = new dcFacturaResponse { NumeroComprobante = nroComprobante };

        var resultadoNode = xmlDoc.SelectSingleNode("//ar:FECompConsultarResult/ar:ResultGet/ar:Resultado", nsmgr);
        result.Resultado = resultadoNode?.InnerText ?? string.Empty;

        foreach (var error in ExtractMessages(xmlDoc, nsmgr, "//ar:FECompConsultarResult/ar:ResultGet/ar:Errors/ar:Err"))
        {
            result.Errores.Add(error);
        }
        foreach (var error in ExtractMessages(xmlDoc, nsmgr, "//ar:FECompConsultarResult/ar:Errors/ar:Err"))
        {
            result.Errores.Add(error);
        }
        foreach (var error in ExtractMessages(xmlDoc, nsmgr, "//ar:Err"))
        {
            result.Errores.Add(error);
        }

        foreach (var obs in ExtractMessages(xmlDoc, nsmgr, "//ar:FECompConsultarResult/ar:ResultGet/ar:Observations/ar:Obs"))
        {
            result.Observaciones.Add(obs);
        }
        foreach (var obs in ExtractMessages(xmlDoc, nsmgr, "//ar:FECompConsultarResult/ar:Observations/ar:Obs"))
        {
            result.Observaciones.Add(obs);
        }
        foreach (var obs in ExtractMessages(xmlDoc, nsmgr, "//ar:Obs"))
        {
            result.Observaciones.Add(obs);
        }

        foreach (var evt in ExtractMessages(xmlDoc, nsmgr, "//ar:FECompConsultarResult/ar:ResultGet/ar:Events/ar:Evt", prefix: "Evento "))
        {
            result.Observaciones.Add(evt);
        }
        foreach (var evt in ExtractMessages(xmlDoc, nsmgr, "//ar:FECompConsultarResult/ar:Events/ar:Evt", prefix: "Evento "))
        {
            result.Observaciones.Add(evt);
        }
        foreach (var evt in ExtractMessages(xmlDoc, nsmgr, "//ar:Evt", prefix: "Evento "))
        {
            result.Observaciones.Add(evt);
        }

        var resultGetNode = xmlDoc.SelectSingleNode("//ar:FECompConsultarResult/ar:ResultGet", nsmgr);

        if (resultGetNode != null)
        {
            result.Success = true;
            result.Concepto = TryGetEnum<dcConcepto>(resultGetNode, nsmgr, "ar:Concepto");
            result.DocTipo = TryGetEnum<dcTipoDocumento>(resultGetNode, nsmgr, "ar:DocTipo");
            result.DocNro = TryGetLong(resultGetNode, nsmgr, "ar:DocNro");
            result.CbteDesde = TryGetLong(resultGetNode, nsmgr, "ar:CbteDesde");
            result.CbteHasta = TryGetLong(resultGetNode, nsmgr, "ar:CbteHasta");
            result.TipoComprobante = TryGetEnum<dcTipoComprobante>(resultGetNode, nsmgr, "ar:CbteTipo");
            result.PuntoVenta = TryGetInt(resultGetNode, nsmgr, "ar:PtoVta");
            result.FechaComprobante = resultGetNode.SelectSingleNode("ar:CbteFch", nsmgr)?.InnerText ?? string.Empty;
            result.FechaServicioDesde = resultGetNode.SelectSingleNode("ar:FchServDesde", nsmgr)?.InnerText ?? string.Empty;
            result.FechaServicioHasta = resultGetNode.SelectSingleNode("ar:FchServHasta", nsmgr)?.InnerText ?? string.Empty;
            result.FechaVencimientoPago = resultGetNode.SelectSingleNode("ar:FchVtoPago", nsmgr)?.InnerText ?? string.Empty;
            result.EmisionTipo = resultGetNode.SelectSingleNode("ar:EmisionTipo", nsmgr)?.InnerText ?? string.Empty;
            result.FechaProceso = resultGetNode.SelectSingleNode("ar:FchProceso", nsmgr)?.InnerText ?? string.Empty;

            result.ImporteTotal = TryGetDecimal(resultGetNode, nsmgr, "ar:ImpTotal") ?? 0m;
            result.ImporteNeto = TryGetDecimal(resultGetNode, nsmgr, "ar:ImpNeto") ?? 0m;
            result.ImporteNoGravado = TryGetDecimal(resultGetNode, nsmgr, "ar:ImpTotConc") ?? 0m;
            result.ImporteExento = TryGetDecimal(resultGetNode, nsmgr, "ar:ImpOpEx") ?? 0m;
            result.ImporteTributos = TryGetDecimal(resultGetNode, nsmgr, "ar:ImpTrib") ?? 0m;
            result.ImporteIva = TryGetDecimal(resultGetNode, nsmgr, "ar:ImpIVA") ?? 0m;
            result.MonedaId = resultGetNode.SelectSingleNode("ar:MonId", nsmgr)?.InnerText ?? string.Empty;
            result.MonedaCotizacion = TryGetDecimal(resultGetNode, nsmgr, "ar:MonCotiz") ?? 0m;
            result.CondicionIvaReceptor = TryGetEnum<dcCondicionIvaReceptor>(resultGetNode, nsmgr, "ar:CondicionIVAReceptorId");

            var caeNode = resultGetNode.SelectSingleNode("ar:CodAutorizacion", nsmgr);
            var caeVtoNode = resultGetNode.SelectSingleNode("ar:FchVto", nsmgr);

            if (caeNode != null && !string.IsNullOrWhiteSpace(caeNode.InnerText))
            {
                result.Cae = WebUtility.HtmlDecode(caeNode.InnerText);
                result.CaeVencimiento = WebUtility.HtmlDecode(caeVtoNode?.InnerText ?? string.Empty);
                result.Mensaje = "Consulta de comprobante exitosa";
                _logger.LogInformation($"[dcWsfeParser] Consulta FECompConsultar exitosa. CAE {result.Cae} (vence {result.CaeVencimiento}).");
            }
            else
            {
                result.Mensaje = string.IsNullOrWhiteSpace(result.Resultado)
                    ? "Consulta de comprobante realizada"
                    : $"Resultado: {result.Resultado}";
            }

            result.Iva.Clear();
            var ivaNodes = resultGetNode.SelectNodes("ar:Iva/ar:AlicIva", nsmgr);
            if (ivaNodes != null)
            {
                foreach (XmlNode iva in ivaNodes)
                {
                    var detalle = new dcFacturaResponse.IvaDetalle
                    {
                        Alicuota = TryGetEnum<dcAlicuotaIva>(iva, nsmgr, "ar:Id"),
                        BaseImponible = TryGetDecimal(iva, nsmgr, "ar:BaseImp") ?? 0m,
                        Importe = TryGetDecimal(iva, nsmgr, "ar:Importe") ?? 0m
                    };
                    result.Iva.Add(detalle);
                }
            }

            result.Tributos.Clear();
            var tributoNodes = resultGetNode.SelectNodes("ar:Tributos/ar:Tributo", nsmgr);
            if (tributoNodes != null)
            {
                foreach (XmlNode tributo in tributoNodes)
                {
                    result.Tributos.Add(new dcFacturaResponse.TributoDetalle
                    {
                        Id = TryGetInt(tributo, nsmgr, "ar:Id"),
                        Descripcion = tributo.SelectSingleNode("ar:Desc", nsmgr)?.InnerText ?? string.Empty,
                        BaseImponible = TryGetDecimal(tributo, nsmgr, "ar:BaseImp") ?? 0m,
                        Alicuota = TryGetDecimal(tributo, nsmgr, "ar:Alic") ?? 0m,
                        Importe = TryGetDecimal(tributo, nsmgr, "ar:Importe") ?? 0m
                    });
                }
            }
        }
        else
        {
            result.Success = false;
            result.Mensaje = result.Errores.Count > 0
                ? "Consulta rechazada: " + string.Join("; ", result.Errores)
                : "No se encontró información del comprobante consultado.";
            _logger.LogWarning($"[dcWsfeParser] Consulta FECompConsultar sin datos para comprobante {nroComprobante}: {result.Mensaje}");
        }

        return result;
    }

    private static int? TryGetInt(XmlNode parent, XmlNamespaceManager ns, string xpath)
    {
        var text = parent.SelectSingleNode(xpath, ns)?.InnerText;
        return int.TryParse(text, out var value) ? value : null;
    }

    private static long? TryGetLong(XmlNode parent, XmlNamespaceManager ns, string xpath)
    {
        var text = parent.SelectSingleNode(xpath, ns)?.InnerText;
        return long.TryParse(text, out var value) ? value : null;
    }

    private static decimal? TryGetDecimal(XmlNode parent, XmlNamespaceManager ns, string xpath)
    {
        var text = parent.SelectSingleNode(xpath, ns)?.InnerText;
        return decimal.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }

    private static TEnum? TryGetEnum<TEnum>(XmlNode parent, XmlNamespaceManager ns, string xpath)
        where TEnum : struct, Enum
    {
        var raw = TryGetInt(parent, ns, xpath);
        if (!raw.HasValue)
        {
            return null;
        }

        return Enum.IsDefined(typeof(TEnum), raw.Value)
            ? (TEnum?)Enum.ToObject(typeof(TEnum), raw.Value)
            : null;
    }

    /// <summary>
    /// Procesa la respuesta de <c>FEParamGetCondicionIvaReceptor</c>.
    /// </summary>
    /// <param name="xmlResponse">Contenido XML devuelto por AFIP.</param>
    /// <returns>Listado de opciones devueltas por el servicio.</returns>
    public List<dcCondicionIvaOption> ParseCondicionIvaResponse(string xmlResponse)
    {
        var (xmlDoc, nsmgr) = LoadXml(xmlResponse);
        var fault = ExtractFault(xmlDoc);
        if (fault != null)
        {
            if (dcTokenFaultDetector.IsInvalidSignature(fault.Reason))
            {
                throw new dcTokenInvalidException($"Token inválido detectado: {fault.Reason}");
            }
            _logger.LogError($"Consulta de Condición IVA rechazada: {fault.ToSingleLine()}");
            return new List<dcCondicionIvaOption>();
        }

        var opciones = new List<dcCondicionIvaOption>();
        var nodos = xmlDoc.SelectNodes("//ar:CondicionIvaReceptor", nsmgr);
        if (nodos != null)
        {
            foreach (XmlNode nodo in nodos)
            {
                var id = nodo.SelectSingleNode("ar:Id", nsmgr)?.InnerText ?? string.Empty;
                var desc = nodo.SelectSingleNode("ar:Desc", nsmgr)?.InnerText ?? string.Empty;
                var clases = nodo.SelectSingleNode("ar:Cmp_Clase", nsmgr)?.InnerText ?? string.Empty;

                if (!string.IsNullOrWhiteSpace(id))
                {
                    opciones.Add(new dcCondicionIvaOption
                    {
                        Id = id.Trim(),
                        Descripcion = desc.Trim(),
                        ClasesComprobante = clases.Trim()
                    });
                }
            }
        }

        return opciones;
    }

    private static (XmlDocument Xml, XmlNamespaceManager NsMgr) LoadXml(string xml)
    {
        var xmlDoc = new XmlDocument();
        xmlDoc.LoadXml(xml);

        var nsmgr = new XmlNamespaceManager(xmlDoc.NameTable);
        nsmgr.AddNamespace("soap", SoapEnvelopeNamespace);
        nsmgr.AddNamespace("ar", WsfeNamespace);

        return (xmlDoc, nsmgr);
    }

    private static SoapFaultInfo? ExtractFault(XmlDocument xmlDoc)
    {
        var faultNode = xmlDoc.SelectSingleNode("//*[local-name()='Fault']");
        if (faultNode == null)
        {
            return null;
        }

        var code = faultNode.SelectSingleNode("./faultcode")?.InnerText?.Trim()
                   ?? faultNode.SelectSingleNode("./faultcode/*")?.InnerText?.Trim();
        var reason = faultNode.SelectSingleNode("./faultstring")?.InnerText?.Trim()
                     ?? faultNode.SelectSingleNode("./faultreason/*[local-name()='Text']")?.InnerText?.Trim();
        var detailNode = faultNode.SelectSingleNode("./detail") ?? faultNode.SelectSingleNode("./Detail");
        var detail = detailNode?.InnerText?.Trim();

        return new SoapFaultInfo(code, reason, detail);
    }

    private static dcFacturaResponse BuildFaultResponse(SoapFaultInfo fault, string defaultMessage)
    {
        var mensaje = string.IsNullOrWhiteSpace(fault.Reason)
            ? defaultMessage
            : fault.Reason;

        if (!string.IsNullOrWhiteSpace(fault.Detail))
        {
            mensaje = $"{mensaje} | Detalle: {fault.Detail}";
        }

        var response = new dcFacturaResponse
        {
            Success = false,
            Mensaje = mensaje,
            Codigo = fault.Code
        };

        if (!string.IsNullOrWhiteSpace(fault.Reason))
        {
            response.Errores.Add(fault.Reason);
        }

        if (!string.IsNullOrWhiteSpace(fault.Detail))
        {
            response.Errores.Add(fault.Detail);
        }

        return response;
    }

    private static dcFacturaResponse BuildErrorResponse(string codigo, string mensaje)
    {
        var respuesta = new dcFacturaResponse
        {
            Success = false,
            Codigo = codigo,
            Mensaje = mensaje
        };
        respuesta.Errores.Add(mensaje);
        return respuesta;
    }

    private static IEnumerable<string> ExtractMessages(XmlDocument xmlDoc, XmlNamespaceManager nsmgr, string xpath, string? prefix = null)
    {
        prefix ??= string.Empty;
        var nodes = xmlDoc.SelectNodes(xpath, nsmgr);
        if (nodes == null)
        {
            yield break;
        }

        foreach (XmlNode node in nodes)
        {
            var code = node.SelectSingleNode("ar:Code", nsmgr)?.InnerText ?? node.SelectSingleNode("Code")?.InnerText ?? string.Empty;
            var message = node.SelectSingleNode("ar:Msg", nsmgr)?.InnerText ?? node.SelectSingleNode("Msg")?.InnerText ?? node.InnerText;
            var formatted = string.IsNullOrWhiteSpace(code)
                ? message.Trim()
                : $"[{prefix}{code.Trim()}] {message.Trim()}";
            yield return formatted;
        }
    }

    private sealed record SoapFaultInfo(string? Code, string? Reason, string? Detail)
    {
        public string ToSingleLine()
        {
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(Code)) parts.Add(Code!);
            if (!string.IsNullOrWhiteSpace(Reason)) parts.Add(Reason!);
            if (!string.IsNullOrWhiteSpace(Detail)) parts.Add($"Detalle: {Detail}");
            return string.Join(" | ", parts);
        }
    }
}
