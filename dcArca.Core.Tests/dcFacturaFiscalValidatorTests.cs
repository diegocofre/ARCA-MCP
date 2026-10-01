using dcArca.Core.Models;
using dcArca.Core.Services;
using dcArca.Core.Services.Logging;
using Xunit;

namespace dcArca.Core.Tests;

public class dcFacturaFiscalValidatorTests
{
    [Fact]
    public void CasoSimple21_ConAlicuotaExplicita_EsValido()
    {
        var factura = BaseFactura();
        factura.ImporteNeto = 100m;
        factura.ImporteIva = 21m;
        factura.ImporteTotal = 121m;
        factura.AlicuotaIva = dcAlicuotaIva.Veintiuno;

        var result = dcFacturaFiscalValidator.Validate(factura);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void IvaPositivo_SinDetalleNiAlicuota_SeRechaza()
    {
        var factura = BaseFactura();
        factura.ImporteNeto = 100m;
        factura.ImporteIva = 21m;
        factura.ImporteTotal = 121m;

        var result = dcFacturaFiscalValidator.Validate(factura);

        Assert.False(result.IsValid);
        Assert.Equal("IVA_DETAIL_REQUIRED", result.Code);
    }

    [Fact]
    public void MultiplesAlicuotas_EsValido()
    {
        var factura = BaseFactura();
        factura.ImporteNeto = 200m;
        factura.ImporteIva = 31.50m;
        factura.ImporteTotal = 231.50m;
        factura.Iva.Add(new() { Alicuota = dcAlicuotaIva.Veintiuno, BaseImponible = 100m, Importe = 21m });
        factura.Iva.Add(new() { Alicuota = dcAlicuotaIva.Diez_Cinco, BaseImponible = 100m, Importe = 10.50m });

        Assert.True(dcFacturaFiscalValidator.Validate(factura).IsValid);
    }

    [Fact]
    public void ExentoYNoGravado_SinNetoNiIva_EsValido()
    {
        var factura = BaseFactura();
        factura.ImporteNeto = 0m;
        factura.ImporteIva = 0m;
        factura.ImporteNoGravado = 50m;
        factura.ImporteExento = 100m;
        factura.ImporteTotal = 150m;

        Assert.True(dcFacturaFiscalValidator.Validate(factura).IsValid);
    }

    [Fact]
    public void Tributo_SeIncluyeEnTotal()
    {
        var factura = BaseFactura();
        factura.ImporteNeto = 100m;
        factura.ImporteIva = 21m;
        factura.AlicuotaIva = dcAlicuotaIva.Veintiuno;
        factura.Tributos.Add(new()
        {
            Id = 99,
            Descripcion = "Percepción",
            BaseImponible = 100m,
            Alicuota = 3m,
            Importe = 3m
        });
        factura.ImporteTotal = 124m;

        Assert.True(dcFacturaFiscalValidator.Validate(factura).IsValid);
        Assert.Equal(3m, factura.ImporteTributos);
    }

    [Fact]
    public void TotalInconsistente_SeRechaza()
    {
        var factura = BaseFactura();
        factura.ImporteNeto = 100m;
        factura.ImporteIva = 10.50m;
        factura.ImporteTotal = 121m;
        factura.AlicuotaIva = dcAlicuotaIva.Diez_Cinco;

        var result = dcFacturaFiscalValidator.Validate(factura);

        Assert.False(result.IsValid);
        Assert.Equal("IMP_MISMATCH", result.Code);
    }

    [Fact]
    public void MonedaYCotizacionConfigurables_SeValidan()
    {
        var factura = BaseFactura();
        factura.ImporteNeto = 100m;
        factura.ImporteIva = 0m;
        factura.ImporteTotal = 100m;
        factura.MonedaId = "DOL";
        factura.MonedaCotizacion = 1234.5678m;

        Assert.True(dcFacturaFiscalValidator.Validate(factura).IsValid);
    }

    [Fact]
    public void Builder_Representa10_5MultiplesConceptosTributosYMoneda()
    {
        var config = BuildConfig();
        var builder = new dcWsfeSoapBuilder(config);
        var factura = BaseFactura();
        factura.ImporteNeto = 200m;
        factura.ImporteIva = 31.50m;
        factura.ImporteNoGravado = 10m;
        factura.ImporteExento = 20m;
        factura.Iva.Add(new() { Alicuota = dcAlicuotaIva.Veintiuno, BaseImponible = 100m, Importe = 21m });
        factura.Iva.Add(new() { Alicuota = dcAlicuotaIva.Diez_Cinco, BaseImponible = 100m, Importe = 10.50m });
        factura.Tributos.Add(new()
        {
            Id = 99,
            Descripcion = "Percepción & tasa",
            BaseImponible = 200m,
            Alicuota = 1.5m,
            Importe = 3m
        });
        factura.ImporteTotal = 264.50m;
        factura.MonedaId = "DOL";
        factura.MonedaCotizacion = 1200.25m;

        Assert.True(dcFacturaFiscalValidator.Validate(factura).IsValid);

        var xml = builder.BuildSolicitarCaeRequest("token", "sign", factura, 1, 1, 1);

        Assert.Contains("<ar:ImpTotConc>10.00</ar:ImpTotConc>", xml);
        Assert.Contains("<ar:ImpOpEx>20.00</ar:ImpOpEx>", xml);
        Assert.Contains("<ar:ImpTrib>3.00</ar:ImpTrib>", xml);
        Assert.Contains("<ar:Id>5</ar:Id>", xml);
        Assert.Contains("<ar:Id>4</ar:Id>", xml);
        Assert.Contains("Percepción &amp; tasa", xml);
        Assert.Contains("<ar:MonId>DOL</ar:MonId>", xml);
        Assert.Contains("<ar:MonCotiz>1200.25</ar:MonCotiz>", xml);
    }

    [Fact]
    public void Parser_ConservaDetalleDeTributos()
    {
        const string xml = """
            <soap:Envelope xmlns:soap="http://www.w3.org/2003/05/soap-envelope" xmlns:ar="http://ar.gov.afip.dif.FEV1/">
              <soap:Body>
                <ar:FECompConsultarResponse>
                  <ar:FECompConsultarResult>
                    <ar:ResultGet>
                      <ar:Resultado>A</ar:Resultado>
                      <ar:CbteDesde>1</ar:CbteDesde>
                      <ar:ImpTrib>3.00</ar:ImpTrib>
                      <ar:Tributos>
                        <ar:Tributo>
                          <ar:Id>99</ar:Id>
                          <ar:Desc>Percepción</ar:Desc>
                          <ar:BaseImp>100.00</ar:BaseImp>
                          <ar:Alic>3.00</ar:Alic>
                          <ar:Importe>3.00</ar:Importe>
                        </ar:Tributo>
                      </ar:Tributos>
                    </ar:ResultGet>
                  </ar:FECompConsultarResult>
                </ar:FECompConsultarResponse>
              </soap:Body>
            </soap:Envelope>
            """;

        var parser = new dcWsfeSoapParser(NoOpAfipLogger.Instance);
        var result = parser.ParseFECompConsultarResponse(xml, 1);

        var tributo = Assert.Single(result.Tributos);
        Assert.Equal(99, tributo.Id);
        Assert.Equal("Percepción", tributo.Descripcion);
        Assert.Equal(3m, tributo.Importe);
    }

    private static dcFacturaRequest BaseFactura() => new()
    {
        TipoComprobante = dcTipoComprobante.FacturaA,
        NumeroComprobante = 1,
        Concepto = dcConcepto.Productos,
        CuitReceptor = 20123456786,
        TipoDocReceptor = (int)dcTipoDocumento.CUIT,
        FechaComprobante = "20260930"
    };

    private static dcArcaConfig BuildConfig() => new()
    {
        Cuit = "20123456786",
        CertificatePath = "no-existe.pfx",
        WsaaUrl = "https://wsaahomo.afip.gov.ar/ws/services/LoginCms",
        WsfeUrl = "https://wswhomo.afip.gov.ar/wsfev1/service.asmx",
        PadronUrl = "https://awshomo.afip.gov.ar/sr-padron/webservices/personaServiceA5",
        PuntoVenta = 1
    };
}
