/*
 * Copyright (c) 2025 Diego Cofré, DC Sistemas
 * www.diegocofre.com.ar
 *
 * Licensed under the Apache License, Version 2.0.
 * You may obtain a copy of the License at
 * http://www.apache.org/licenses/LICENSE-2.0
 */

using System;
using System.Globalization;
using System.Security;
using System.Text;
using dcArca.Core.Models;

namespace dcArca.Core.Services;

/// <summary>
/// Construye solicitudes SOAP para las operaciones del servicio WSFEv1.
/// </summary>
public sealed class dcWsfeSoapBuilder
{
    private const string SoapEnvelopeNamespace = "http://www.w3.org/2003/05/soap-envelope";
    private const string WsfeNamespace = "http://ar.gov.afip.dif.FEV1/";

    private readonly dcArcaConfig _config;

    /// <summary>
    /// Inicializa el builder con la configuración del contribuyente.
    /// </summary>
    /// <param name="config">Configuración cargada desde <c>appsettings.json</c>.</param>
    public dcWsfeSoapBuilder(dcArcaConfig config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }

    /// <summary>
    /// Genera el envelope SOAP para la operación <c>FECompUltimoAutorizado</c>.
    /// </summary>
    /// <param name="token">Token obtenido desde WSAA.</param>
    /// <param name="sign">Firma obtenida desde WSAA.</param>
    /// <param name="tipoComprobante">Tipo de comprobante requerido.</param>
    /// <returns>Envelope SOAP listo para enviar.</returns>
    public string BuildUltimoComprobanteRequest(string token, string sign, int tipoComprobante)
    {
        return $"<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n" +
               $"<soap:Envelope xmlns:soap=\"{SoapEnvelopeNamespace}\" xmlns:ar=\"{WsfeNamespace}\">\n" +
               "    <soap:Header/>\n" +
               "    <soap:Body>\n" +
               "        <ar:FECompUltimoAutorizado>\n" +
               BuildAuthBlock(token, sign) +
               $"            <ar:PtoVta>{_config.PuntoVenta}</ar:PtoVta>\n" +
               $"            <ar:CbteTipo>{tipoComprobante}</ar:CbteTipo>\n" +
               "        </ar:FECompUltimoAutorizado>\n" +
               "    </soap:Body>\n" +
               "</soap:Envelope>";
    }

    /// <summary>
    /// Genera el envelope SOAP para la operación <c>FECompConsultar</c>.
    /// </summary>
    /// <param name="token">Token obtenido desde WSAA.</param>
    /// <param name="sign">Firma obtenida desde WSAA.</param>
    /// <param name="tipoComprobante">Tipo de comprobante del comprobante a consultar.</param>
    /// <param name="numeroComprobante">Número del comprobante a consultar.</param>
    /// <returns>Envelope SOAP listo para enviar.</returns>
    public string BuildConsultarComprobanteRequest(string token, string sign, int tipoComprobante, long numeroComprobante)
    {
        return $"<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n" +
               $"<soap:Envelope xmlns:soap=\"{SoapEnvelopeNamespace}\" xmlns:ar=\"{WsfeNamespace}\">\n" +
               "    <soap:Header/>\n" +
               "    <soap:Body>\n" +
               "        <ar:FECompConsultar>\n" +
               BuildAuthBlock(token, sign) +
               "            <ar:FeCompConsReq>\n" +
               $"                <ar:PtoVta>{_config.PuntoVenta}</ar:PtoVta>\n" +
               $"                <ar:CbteTipo>{tipoComprobante}</ar:CbteTipo>\n" +
               $"                <ar:CbteNro>{numeroComprobante}</ar:CbteNro>\n" +
               "            </ar:FeCompConsReq>\n" +
               "        </ar:FECompConsultar>\n" +
               "    </soap:Body>\n" +
               "</soap:Envelope>";
    }

    /// <summary>
    /// Genera el envelope SOAP para la operación <c>FECAESolicitar</c>.
    /// </summary>
    /// <param name="token">Token obtenido desde WSAA.</param>
    /// <param name="sign">Firma obtenida desde WSAA.</param>
    /// <param name="factura">Factura a solicitar.</param>
    /// <param name="nroComprobante">Número del comprobante a autorizar.</param>
    /// <param name="tipoComprobante">Tipo de comprobante correspondiente.</param>
    /// <param name="concepto">Concepto de la operación (1=Productos, 2=Servicios, 3=Productos y Servicios).</param>
    /// <returns>Envelope SOAP listo para enviar.</returns>
    public string BuildSolicitarCaeRequest(string token, string sign, dcFacturaRequest factura, long nroComprobante, int tipoComprobante, int concepto)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        sb.AppendLine($"<soap:Envelope xmlns:soap=\"{SoapEnvelopeNamespace}\" xmlns:ar=\"{WsfeNamespace}\">");
        sb.AppendLine("    <soap:Header/>");
        sb.AppendLine("    <soap:Body>");
        sb.AppendLine("        <ar:FECAESolicitar>");
        sb.Append(BuildAuthBlock(token, sign));
        sb.AppendLine("            <ar:FeCAEReq>");
        sb.AppendLine("                <ar:FeCabReq>");
        sb.AppendLine("                    <ar:CantReg>1</ar:CantReg>");
        sb.AppendLine($"                    <ar:PtoVta>{_config.PuntoVenta}</ar:PtoVta>");
        sb.AppendLine($"                    <ar:CbteTipo>{tipoComprobante}</ar:CbteTipo>");
        sb.AppendLine("                </ar:FeCabReq>");
        sb.AppendLine("                <ar:FeDetReq>");
        sb.AppendLine("                    <ar:FECAEDetRequest>");
        sb.AppendLine($"                        <ar:Concepto>{concepto}</ar:Concepto>");
        sb.AppendLine($"                        <ar:DocTipo>{factura.TipoDocReceptor}</ar:DocTipo>");
        sb.AppendLine($"                        <ar:DocNro>{factura.CuitReceptor}</ar:DocNro>");
        sb.AppendLine($"                        <ar:CbteDesde>{nroComprobante}</ar:CbteDesde>");
        sb.AppendLine($"                        <ar:CbteHasta>{nroComprobante}</ar:CbteHasta>");
        sb.AppendLine($"                        <ar:CbteFch>{SecurityElement.Escape(factura.FechaComprobante)}</ar:CbteFch>");
        sb.AppendLine($"                        <ar:ImpTotal>{factura.ImporteTotal.ToString("F2", CultureInfo.InvariantCulture)}</ar:ImpTotal>");
        sb.AppendLine($"                        <ar:ImpTotConc>{factura.ImporteNoGravado.ToString("F2", CultureInfo.InvariantCulture)}</ar:ImpTotConc>");
        sb.AppendLine($"                        <ar:ImpNeto>{factura.ImporteNeto.ToString("F2", CultureInfo.InvariantCulture)}</ar:ImpNeto>");
        sb.AppendLine($"                        <ar:ImpOpEx>{factura.ImporteExento.ToString("F2", CultureInfo.InvariantCulture)}</ar:ImpOpEx>");
        sb.AppendLine($"                        <ar:ImpTrib>{factura.ImporteTributos.ToString("F2", CultureInfo.InvariantCulture)}</ar:ImpTrib>");
        sb.AppendLine($"                        <ar:ImpIVA>{factura.ImporteIva.ToString("F2", CultureInfo.InvariantCulture)}</ar:ImpIVA>");
        AppendServiceDates(sb, concepto, factura);
        sb.AppendLine($"                        <ar:MonId>{SecurityElement.Escape(factura.MonedaId)}</ar:MonId>");
        sb.AppendLine($"                        <ar:MonCotiz>{factura.MonedaCotizacion.ToString("0.######", CultureInfo.InvariantCulture)}</ar:MonCotiz>");
        AppendCondicionIva(sb, factura);
        AppendTributos(sb, factura);
        AppendIva(sb, factura);
        sb.AppendLine("                    </ar:FECAEDetRequest>");
        sb.AppendLine("                </ar:FeDetReq>");
        sb.AppendLine("            </ar:FeCAEReq>");
        sb.AppendLine("        </ar:FECAESolicitar>");
        sb.AppendLine("    </soap:Body>");
        sb.AppendLine("</soap:Envelope>");

        return sb.ToString();
    }

    /// <summary>
    /// Genera el envelope SOAP para la operación <c>FEParamGetCondicionIvaReceptor</c>.
    /// </summary>
    /// <param name="token">Token obtenido desde WSAA.</param>
    /// <param name="sign">Firma obtenida desde WSAA.</param>
    /// <param name="tipoComprobante">Tipo de comprobante a evaluar.</param>
    /// <param name="docTipo">Tipo de documento del receptor.</param>
    /// <param name="docNro">Número de documento del receptor.</param>
    /// <returns>Envelope SOAP listo para enviar.</returns>
    public string BuildCondicionIvaRequest(string token, string sign, int tipoComprobante, int docTipo, long docNro)
    {
        return $"<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n" +
               $"<soap:Envelope xmlns:soap=\"{SoapEnvelopeNamespace}\" xmlns:ar=\"{WsfeNamespace}\">\n" +
               "    <soap:Header/>\n" +
               "    <soap:Body>\n" +
               "        <ar:FEParamGetCondicionIvaReceptor>\n" +
               BuildAuthBlock(token, sign) +
               $"            <ar:CbteTipo>{tipoComprobante}</ar:CbteTipo>\n" +
               $"            <ar:DocTipo>{docTipo}</ar:DocTipo>\n" +
               $"            <ar:DocNro>{docNro}</ar:DocNro>\n" +
               "        </ar:FEParamGetCondicionIvaReceptor>\n" +
               "    </soap:Body>\n" +
               "</soap:Envelope>";
    }

    private string BuildAuthBlock(string token, string sign)
    {
        return "            <ar:Auth>\n" +
               $"                <ar:Token>{token}</ar:Token>\n" +
               $"                <ar:Sign>{sign}</ar:Sign>\n" +
               $"                <ar:Cuit>{_config.Cuit}</ar:Cuit>\n" +
               "            </ar:Auth>\n";
    }

    private static void AppendTributos(StringBuilder sb, dcFacturaRequest factura)
    {
        if (factura.Tributos.Count == 0)
        {
            return;
        }

        sb.AppendLine("                        <ar:Tributos>");
        foreach (var tributo in factura.Tributos)
        {
            sb.AppendLine("                            <ar:Tributo>");
            sb.AppendLine($"                                <ar:Id>{tributo.Id}</ar:Id>");
            sb.AppendLine($"                                <ar:Desc>{SecurityElement.Escape(tributo.Descripcion)}</ar:Desc>");
            sb.AppendLine($"                                <ar:BaseImp>{tributo.BaseImponible.ToString("F2", CultureInfo.InvariantCulture)}</ar:BaseImp>");
            sb.AppendLine($"                                <ar:Alic>{tributo.Alicuota.ToString("0.####", CultureInfo.InvariantCulture)}</ar:Alic>");
            sb.AppendLine($"                                <ar:Importe>{tributo.Importe.ToString("F2", CultureInfo.InvariantCulture)}</ar:Importe>");
            sb.AppendLine("                            </ar:Tributo>");
        }
        sb.AppendLine("                        </ar:Tributos>");
    }

    private static void AppendIva(StringBuilder sb, dcFacturaRequest factura)
    {
        var iva = dcFacturaFiscalValidator.GetEffectiveIva(factura);
        if (iva.Count == 0)
        {
            return;
        }

        sb.AppendLine("                        <ar:Iva>");
        foreach (var detalle in iva)
        {
            sb.AppendLine("                            <ar:AlicIva>");
            sb.AppendLine($"                                <ar:Id>{(int)detalle.Alicuota}</ar:Id>");
            sb.AppendLine($"                                <ar:BaseImp>{detalle.BaseImponible.ToString("F2", CultureInfo.InvariantCulture)}</ar:BaseImp>");
            sb.AppendLine($"                                <ar:Importe>{detalle.Importe.ToString("F2", CultureInfo.InvariantCulture)}</ar:Importe>");
            sb.AppendLine("                            </ar:AlicIva>");
        }
        sb.AppendLine("                        </ar:Iva>");
    }

    private static void AppendServiceDates(StringBuilder sb, int concepto, dcFacturaRequest factura)
    {
        if (concepto == 1)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(factura.FechaServicioDesde))
        {
            sb.AppendLine($"                        <ar:FchServDesde>{SecurityElement.Escape(factura.FechaServicioDesde)}</ar:FchServDesde>");
        }

        if (!string.IsNullOrWhiteSpace(factura.FechaServicioHasta))
        {
            sb.AppendLine($"                        <ar:FchServHasta>{SecurityElement.Escape(factura.FechaServicioHasta)}</ar:FchServHasta>");
        }

        if (!string.IsNullOrWhiteSpace(factura.FechaVencimiento))
        {
            sb.AppendLine($"                        <ar:FchVtoPago>{SecurityElement.Escape(factura.FechaVencimiento)}</ar:FchVtoPago>");
        }
    }

    private static void AppendCondicionIva(StringBuilder sb, dcFacturaRequest factura)
    {
        if (factura.CondicionIvaReceptor != null)
        {
            int id = (int)factura.CondicionIvaReceptor;
            sb.AppendLine($"                        <ar:CondicionIVAReceptorId>{id}</ar:CondicionIVAReceptorId>");
        }

        // Comprobantes asociados (Notas de crédito/débito) - Regla 10197
        if (factura.EsNota() && factura.CbteAsociadoTipo.HasValue && factura.CbteAsociadoPtoVta.HasValue && factura.CbteAsociadoNro.HasValue)
        {
            sb.AppendLine("                        <ar:CbtesAsoc>");
            sb.AppendLine("                            <ar:CbteAsoc>");
            sb.AppendLine($"                                <ar:Tipo>{factura.CbteAsociadoTipo.Value}</ar:Tipo>");
            sb.AppendLine($"                                <ar:PtoVta>{factura.CbteAsociadoPtoVta.Value}</ar:PtoVta>");
            sb.AppendLine($"                                <ar:Nro>{factura.CbteAsociadoNro.Value}</ar:Nro>");
            if (!string.IsNullOrWhiteSpace(factura.CbteAsociadoCuit))
            {
                sb.AppendLine($"                                <ar:Cuit>{SecurityElement.Escape(factura.CbteAsociadoCuit)}</ar:Cuit>");
            }
            if (!string.IsNullOrWhiteSpace(factura.CbteAsociadoFecha))
            {
                sb.AppendLine($"                                <ar:CbteFch>{SecurityElement.Escape(factura.CbteAsociadoFecha)}</ar:CbteFch>");
            }
            sb.AppendLine("                            </ar:CbteAsoc>");
            sb.AppendLine("                        </ar:CbtesAsoc>");
        }
        else if (factura.EsNota() && !string.IsNullOrWhiteSpace(factura.PeriodoAsocDesde) && !string.IsNullOrWhiteSpace(factura.PeriodoAsocHasta))
        {
            // Alternativa: periodo asociado
            sb.AppendLine("                        <ar:PeriodoAsoc>");
            sb.AppendLine($"                            <ar:FchDesde>{SecurityElement.Escape(factura.PeriodoAsocDesde)}</ar:FchDesde>");
            sb.AppendLine($"                            <ar:FchHasta>{SecurityElement.Escape(factura.PeriodoAsocHasta)}</ar:FchHasta>");
            sb.AppendLine("                        </ar:PeriodoAsoc>");
        }
    }
}
