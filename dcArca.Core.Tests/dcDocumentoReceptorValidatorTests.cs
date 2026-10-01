using dcArca.Core.Models;
using dcArca.Core.Services;
using Xunit;

namespace dcArca.Core.Tests;

public class dcDocumentoReceptorValidatorTests
{
    [Fact]
    public void CuitValido_AceptaChecksumCorrecto()
    {
        var result = dcDocumentoReceptorValidator.Validate((int)dcTipoDocumento.CUIT, 20123456786);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void CuitInvalido_RechazaChecksumIncorrecto()
    {
        var result = dcDocumentoReceptorValidator.Validate((int)dcTipoDocumento.CUIT, 20123456789);

        Assert.False(result.IsValid);
        Assert.Equal("CUIT_INVALID", result.Code);
    }

    [Fact]
    public void DniValido_NoSeValidaComoCuit()
    {
        var result = dcDocumentoReceptorValidator.Validate((int)dcTipoDocumento.DNI, 30123456);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void ConsumidorFinal_PermiteDocumentoCero()
    {
        var result = dcDocumentoReceptorValidator.Validate((int)dcTipoDocumento.ConsumidorFinal, 0);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Pasaporte_ConNumeroPresente_NoSeValidaComoCuit()
    {
        var result = dcDocumentoReceptorValidator.Validate((int)dcTipoDocumento.Pasaporte, 1234567);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void TipoDesconocido_SeRechaza()
    {
        var result = dcDocumentoReceptorValidator.Validate(123, 12345678);

        Assert.False(result.IsValid);
        Assert.Equal("TDOC_INVALID", result.Code);
    }

    [Fact]
    public void DocumentoNoConsumidorFinal_SinNumero_SeRechaza()
    {
        var result = dcDocumentoReceptorValidator.Validate((int)dcTipoDocumento.DNI, 0);

        Assert.False(result.IsValid);
        Assert.Equal("NDOC_REQUIRED", result.Code);
    }
}
