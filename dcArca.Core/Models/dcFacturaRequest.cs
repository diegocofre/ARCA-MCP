/*
 * Copyright (c) 2025 Diego Cofré, DC Sistemas
 * www.diegocofre.com.ar
 *
 * Licensed under the Apache License, Version 2.0.
 * You may obtain a copy of the License at
 * http://www.apache.org/licenses/LICENSE-2.0
 */

namespace dcArca.Core.Models;

/// <summary>
/// Datos de entrada para solicitar autorización de una factura
/// </summary>
public class dcFacturaRequest
{
    /// <summary>
    /// Tipo de comprobante a autorizar
    /// </summary>
    public dcTipoComprobante? TipoComprobante { get; set; }

    /// <summary>
    /// Número de comprobante a autorizar (CbteDesde/CbteHasta)
    /// </summary>
    public long? NumeroComprobante { get; set; }

    /// <summary>
    /// Concepto de la factura: Productos (1), Servicios (2), Productos y Servicios (3)
    /// </summary>
    public dcConcepto? Concepto { get; set; }

    /// <summary>
    /// CUIT del cliente (receptor de la factura)
    /// </summary>
    public long CuitReceptor { get; set; }

    /// <summary>
    /// Valida que el CUIT tenga 11 dígitos y un dígito verificador correcto
    /// </summary>
    public bool ValidarCuit() => dcCuitValidator.EsValido(CuitReceptor.ToString());

    /// <summary>
    /// Tipo de documento del receptor (80 = CUIT, 96 = DNI, etc.)
    /// </summary>
    public int TipoDocReceptor { get; set; } = 80;

    /// <summary>
    /// Condición frente al IVA del receptor (obligatoria para RG 5616)
    /// </summary>
    public dcCondicionIvaReceptor? CondicionIvaReceptor { get; set; }

    /// <summary>
    /// Importe neto gravado (sin IVA)
    /// </summary>
    public decimal ImporteNeto { get; set; }

    /// <summary>
    /// Importe de IVA
    /// </summary>
    public decimal ImporteIva { get; set; }

    /// <summary>
    /// Importe total de la factura.
    /// Debe coincidir con neto + no gravado + exento + IVA + tributos.
    /// </summary>
    public decimal ImporteTotal { get; set; }

    /// <summary>
    /// Importe no gravado / conceptos que no integran el neto gravado.
    /// </summary>
    public decimal ImporteNoGravado { get; set; }

    /// <summary>
    /// Importe exento de IVA.
    /// </summary>
    public decimal ImporteExento { get; set; }

    /// <summary>
    /// Alícuota explícita para el caso simple de una sola línea de IVA.
    /// Si se usa <see cref="Iva"/>, debe dejarse en null.
    /// </summary>
    public dcAlicuotaIva? AlicuotaIva { get; set; }

    /// <summary>
    /// Detalle explícito de IVA. Permite cero, una o múltiples alícuotas.
    /// </summary>
    public List<IvaDetalle> Iva { get; set; } = new();

    /// <summary>
    /// Tributos/percepciones informados en la operación.
    /// </summary>
    public List<TributoDetalle> Tributos { get; set; } = new();

    /// <summary>
    /// Total de tributos derivado del detalle.
    /// </summary>
    public decimal ImporteTributos => Tributos.Sum(t => t.Importe);

    /// <summary>
    /// Código de moneda WSFE. PES por defecto por compatibilidad.
    /// </summary>
    public string MonedaId { get; set; } = "PES";

    /// <summary>
    /// Cotización de la moneda. 1 por defecto para PES.
    /// </summary>
    public decimal MonedaCotizacion { get; set; } = 1m;

    /// <summary>
    /// Fecha del comprobante (formato YYYYMMDD)
    /// </summary>
    public string FechaComprobante { get; set; } = DateTime.Now.ToString("yyyyMMdd");

    /// <summary>
    /// Fecha de vencimiento del servicio (para concepto 2 o 3)
    /// </summary>
    public string? FechaVencimiento { get; set; }

    /// <summary>
    /// Fecha de servicio desde (para concepto 2 o 3)
    /// </summary>
    public string? FechaServicioDesde { get; set; }

    /// <summary>
    /// Fecha de servicio hasta (para concepto 2 o 3)
    /// </summary>
    public string? FechaServicioHasta { get; set; }

    /// <summary>
    /// Tipo de comprobante asociado (para Notas de Débito/Crédito que ajustan una factura). Campo <ar:CbteAsoc><Tipo>
    /// </summary>
    public int? CbteAsociadoTipo { get; set; }

    /// <summary>
    /// Punto de venta del comprobante asociado. Campo <ar:CbteAsoc><PtoVta>
    /// </summary>
    public int? CbteAsociadoPtoVta { get; set; }

    /// <summary>
    /// Número del comprobante asociado. Campo <ar:CbteAsoc><Nro>
    /// </summary>
    public long? CbteAsociadoNro { get; set; }

    /// <summary>
    /// CUIT del emisor del comprobante asociado (opcional). Campo <ar:CbteAsoc><Cuit>
    /// </summary>
    public string? CbteAsociadoCuit { get; set; }

    /// <summary>
    /// Fecha del comprobante asociado (opcional, formato YYYYMMDD). Campo <ar:CbteAsoc><CbteFch>
    /// </summary>
    public string? CbteAsociadoFecha { get; set; }

    /// <summary>
    /// Periodo asociado desde (estructura <PeriodoAsoc><FchDesde>) alternativa al CbteAsoc para notas. Formato YYYYMMDD.
    /// </summary>
    public string? PeriodoAsocDesde { get; set; }

    /// <summary>
    /// Periodo asociado hasta (estructura <PeriodoAsoc><FchHasta>) alternativa al CbteAsoc para notas. Formato YYYYMMDD.
    /// </summary>
    public string? PeriodoAsocHasta { get; set; }

    /// <summary>
    /// Indica si el tipo de comprobante fue informado y es válido
    /// </summary>
    public bool TieneTipoComprobanteValido() =>
        TipoComprobante.HasValue && Enum.IsDefined(typeof(dcTipoComprobante), TipoComprobante.Value);

    /// <summary>
    /// Indica si el número de comprobante fue informado y es válido (> 0)
    /// </summary>
    public bool TieneNumeroComprobanteValido() => NumeroComprobante.HasValue && NumeroComprobante.Value > 0;

    /// <summary>
    /// Indica si el concepto fue informado y es válido
    /// </summary>
    public bool TieneConceptoValido() => Concepto.HasValue && Enum.IsDefined(typeof(dcConcepto), Concepto.Value);

    /// <summary>
    /// Indica si el comprobante es una Nota de Crédito / Débito (A, B, C, M)
    /// </summary>
    public bool EsNota() => TipoComprobante is dcTipoComprobante.NotaDebitoA or dcTipoComprobante.NotaCreditoA
                                or dcTipoComprobante.NotaDebitoB or dcTipoComprobante.NotaCreditoB
                                or dcTipoComprobante.NotaDebitoC or dcTipoComprobante.NotaCreditoC
                                or dcTipoComprobante.NotaDebitoM or dcTipoComprobante.NotaCreditoM;

    /// <summary>
    /// Valida que se cumpla la regla 10197: notas de crédito / débito deben informar CbteAsoc o PeriodoAsoc.
    /// </summary>
    public bool CumpleReglaNotas10197()
    {
        if (!EsNota()) return true; // Solo aplica a notas.
        bool tieneCbte = CbteAsociadoTipo.HasValue && CbteAsociadoPtoVta.HasValue && CbteAsociadoNro.HasValue;
        bool tienePeriodo = !string.IsNullOrWhiteSpace(PeriodoAsocDesde) && !string.IsNullOrWhiteSpace(PeriodoAsocHasta);
        return tieneCbte || tienePeriodo;
    }

    public sealed class IvaDetalle
    {
        public dcAlicuotaIva Alicuota { get; set; }
        public decimal BaseImponible { get; set; }
        public decimal Importe { get; set; }
    }

    public sealed class TributoDetalle
    {
        public int Id { get; set; }
        public string Descripcion { get; set; } = string.Empty;
        public decimal BaseImponible { get; set; }
        public decimal Alicuota { get; set; }
        public decimal Importe { get; set; }
    }
}
