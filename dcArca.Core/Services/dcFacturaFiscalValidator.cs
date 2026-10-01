using dcArca.Core.Models;

namespace dcArca.Core.Services;

internal readonly record struct dcFacturaFiscalValidation(bool IsValid, string? Code = null, string? Message = null);

internal static class dcFacturaFiscalValidator
{
    private const decimal Tolerance = 0.01m;

    internal static dcFacturaFiscalValidation Validate(dcFacturaRequest factura)
    {
        if (factura.ImporteNeto < 0 || factura.ImporteIva < 0 ||
            factura.ImporteNoGravado < 0 || factura.ImporteExento < 0)
        {
            return new(false, "IMP_NEGATIVE", "Los importes neto, IVA, no gravado y exento no pueden ser negativos.");
        }

        if (factura.Tributos.Any(t =>
                t.Id <= 0 ||
                string.IsNullOrWhiteSpace(t.Descripcion) ||
                t.BaseImponible < 0 ||
                t.Alicuota < 0 ||
                t.Importe < 0))
        {
            return new(false, "TRIB_INVALID", "Los tributos deben informar id, descripción e importes no negativos.");
        }

        if (string.IsNullOrWhiteSpace(factura.MonedaId))
        {
            return new(false, "MONEDA_REQUIRED", "MonedaId es obligatoria.");
        }

        if (factura.MonedaCotizacion <= 0)
        {
            return new(false, "MONEDA_COT_INVALID", "MonedaCotizacion debe ser mayor que cero.");
        }

        if (factura.Iva.Count > 0 && factura.AlicuotaIva.HasValue)
        {
            return new(false, "IVA_AMBIGUOUS", "Informe Iva detallado o AlicuotaIva simple, pero no ambos.");
        }

        var iva = GetEffectiveIva(factura);

        if (factura.ImporteIva > 0 && iva.Count == 0)
        {
            return new(false, "IVA_DETAIL_REQUIRED", "ImporteIva mayor a cero requiere informar Iva detallado o AlicuotaIva explícita.");
        }

        if (iva.Any(x => x.BaseImponible < 0 || x.Importe < 0))
        {
            return new(false, "IVA_INVALID", "Base imponible e importe de IVA no pueden ser negativos.");
        }

        var ivaTotal = iva.Sum(x => x.Importe);
        if (Math.Abs(ivaTotal - factura.ImporteIva) > Tolerance)
        {
            return new(false, "IVA_MISMATCH", "La suma del detalle de IVA debe coincidir con ImporteIva.");
        }

        if (iva.Count > 0)
        {
            var baseTotal = iva.Sum(x => x.BaseImponible);
            if (Math.Abs(baseTotal - factura.ImporteNeto) > Tolerance)
            {
                return new(false, "IVA_BASE_MISMATCH", "La suma de bases imponibles de IVA debe coincidir con ImporteNeto.");
            }
        }

        var expectedTotal =
            factura.ImporteNeto +
            factura.ImporteNoGravado +
            factura.ImporteExento +
            factura.ImporteIva +
            factura.ImporteTributos;

        if (factura.ImporteTotal <= 0 || Math.Abs(expectedTotal - factura.ImporteTotal) > Tolerance)
        {
            return new(false, "IMP_MISMATCH",
                "ImporteTotal debe coincidir con neto + no gravado + exento + IVA + tributos (tolerancia 0.01).");
        }

        return new(true);
    }

    internal static IReadOnlyList<dcFacturaRequest.IvaDetalle> GetEffectiveIva(dcFacturaRequest factura)
    {
        if (factura.Iva.Count > 0)
        {
            return factura.Iva;
        }

        if (factura.AlicuotaIva.HasValue)
        {
            return
            [
                new dcFacturaRequest.IvaDetalle
                {
                    Alicuota = factura.AlicuotaIva.Value,
                    BaseImponible = factura.ImporteNeto,
                    Importe = factura.ImporteIva
                }
            ];
        }

        return Array.Empty<dcFacturaRequest.IvaDetalle>();
    }
}
