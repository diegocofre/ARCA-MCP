/*
 * Copyright (c) 2025 Diego Cofré Sistemas
 * www.diegocofre.com.ar
 *
 * Licensed under the Apache License, Version 2.0.
 * You may obtain a copy of the License at
 * http://www.apache.org/licenses/LICENSE-2.0
 */

namespace dcArca.Core.Models;

/// <summary>
/// Respuesta de AFIP al consultar o autorizar comprobantes
/// </summary>
public class dcFacturaResponse
{
    /// <summary>
    /// Indica si la operación fue exitosa
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Código de Autorización Electrónico
    /// </summary>
    public string Cae { get; set; } = string.Empty;

    /// <summary>
    /// Fecha de vencimiento del CAE (formato YYYYMMDD)
    /// </summary>
    public string CaeVencimiento { get; set; } = string.Empty;

    /// <summary>
    /// Número de comprobante asignado
    /// </summary>
    public long NumeroComprobante { get; set; }

    /// <summary>
    /// Resultado del procesamiento (A = Aprobado, R = Rechazado, etc.)
    /// </summary>
    public string Resultado { get; set; } = string.Empty;

    /// <summary>
    /// Mensaje de error o información adicional
    /// </summary>
    public string Mensaje { get; set; } = string.Empty;

    /// <summary>
    /// Código identificador del resultado o error.
    /// </summary>
    public string? Codigo { get; set; }

    /// <summary>
    /// Estado semántico de una operación de emisión. En consultas puede permanecer en None.
    /// </summary>
    public dcEmissionOutcome EmissionOutcome { get; set; } = dcEmissionOutcome.None;

    /// <summary>
    /// Concepto informado (1=Productos, 2=Servicios, 3=Ambos)
    /// </summary>
    public dcConcepto? Concepto { get; set; }

    /// <summary>
    /// Tipo de documento del receptor
    /// </summary>
    public dcTipoDocumento? DocTipo { get; set; }

    /// <summary>
    /// Número de documento del receptor
    /// </summary>
    public long? DocNro { get; set; }

    /// <summary>
    /// Punto de venta del comprobante
    /// </summary>
    public int? PuntoVenta { get; set; }

    /// <summary>
    /// Tipo de comprobante según AFIP
    /// </summary>
    public dcTipoComprobante? TipoComprobante { get; set; }

    /// <summary>
    /// Número inicial del comprobante
    /// </summary>
    public long? CbteDesde { get; set; }

    /// <summary>
    /// Número final del comprobante
    /// </summary>
    public long? CbteHasta { get; set; }

    /// <summary>
    /// Fecha del comprobante (YYYYMMDD)
    /// </summary>
    public string FechaComprobante { get; set; } = string.Empty;

    /// <summary>
    /// Fecha de servicio desde (YYYYMMDD)
    /// </summary>
    public string FechaServicioDesde { get; set; } = string.Empty;

    /// <summary>
    /// Fecha de servicio hasta (YYYYMMDD)
    /// </summary>
    public string FechaServicioHasta { get; set; } = string.Empty;

    /// <summary>
    /// Fecha de vencimiento de pago (YYYYMMDD)
    /// </summary>
    public string FechaVencimientoPago { get; set; } = string.Empty;

    /// <summary>
    /// Observaciones devueltas por AFIP
    /// </summary>
    public List<string> Observaciones { get; set; } = new();

    /// <summary>
    /// Errores devueltos por AFIP
    /// </summary>
    public List<string> Errores { get; set; } = new();

    /// <summary>
    /// Fecha de vencimiento del CAE (formato YYYYMMDD) - Alias de CaeVencimiento
    /// </summary>
    public string FechaVencimientoCae => CaeVencimiento;

    /// <summary>
    /// Fecha de proceso en AFIP (formato YYYYMMDD)
    /// </summary>
    public string FechaProceso { get; set; } = string.Empty;

    /// <summary>
    /// Emisión (CAE, CAI, etc.)
    /// </summary>
    public string EmisionTipo { get; set; } = string.Empty;

    /// <summary>
    /// Importe total del comprobante
    /// </summary>
    public decimal ImporteTotal { get; set; }

    /// <summary>
    /// Importe neto del comprobante
    /// </summary>
    public decimal ImporteNeto { get; set; }

    /// <summary>
    /// Importe no gravado / conceptos que no integran el neto
    /// </summary>
    public decimal ImporteNoGravado { get; set; }

    /// <summary>
    /// Importe exento
    /// </summary>
    public decimal ImporteExento { get; set; }

    /// <summary>
    /// Importe de tributos (percepciones, tasas, etc.)
    /// </summary>
    public decimal ImporteTributos { get; set; }

    /// <summary>
    /// Importe de IVA del comprobante
    /// </summary>
    public decimal ImporteIva { get; set; }

    /// <summary>
    /// Moneda informada en el comprobante
    /// </summary>
    public string MonedaId { get; set; } = string.Empty;

    /// <summary>
    /// Cotización de la moneda
    /// </summary>
    public decimal MonedaCotizacion { get; set; }

    /// <summary>
    /// Condición IVA del receptor (id)
    /// </summary>
    public dcCondicionIvaReceptor? CondicionIvaReceptor { get; set; }

    /// <summary>
    /// Detalle de alícuotas de IVA.
    /// </summary>
    public List<IvaDetalle> Iva { get; set; } = new();

    /// <summary>
    /// Detalle de tributos/percepciones devueltos por ARCA.
    /// </summary>
    public List<TributoDetalle> Tributos { get; set; } = new();

    public sealed class IvaDetalle
    {
        public dcAlicuotaIva? Alicuota { get; set; }
        public decimal BaseImponible { get; set; }
        public decimal Importe { get; set; }
    }

    public sealed class TributoDetalle
    {
        public int? Id { get; set; }
        public string Descripcion { get; set; } = string.Empty;
        public decimal BaseImponible { get; set; }
        public decimal Alicuota { get; set; }
        public decimal Importe { get; set; }
    }
}
