using dcArca.Core.Models;

namespace dcArca.Core.Services;

internal readonly record struct dcDocumentoReceptorValidation(bool IsValid, string? Code = null, string? Message = null);

internal static class dcDocumentoReceptorValidator
{
    internal static dcDocumentoReceptorValidation Validate(int tipoDocumento, long numeroDocumento)
    {
        if (!Enum.IsDefined(typeof(dcTipoDocumento), tipoDocumento))
        {
            return new(false, "TDOC_INVALID", "TipoDocReceptor debe corresponder a un tipo de documento soportado por ARCA.");
        }

        var tipo = (dcTipoDocumento)tipoDocumento;

        if (tipo == dcTipoDocumento.ConsumidorFinal)
        {
            return numeroDocumento < 0
                ? new(false, "NDOC_INVALID", "El número de documento del receptor no puede ser negativo.")
                : new(true);
        }

        if (numeroDocumento <= 0)
        {
            return new(false, "NDOC_REQUIRED", "El número de documento del receptor es obligatorio para el tipo informado.");
        }

        if (tipo == dcTipoDocumento.CUIT && !dcCuitValidator.EsValido(numeroDocumento.ToString()))
        {
            return new(false, "CUIT_INVALID", "El CUIT del receptor no tiene un formato válido o dígito verificador incorrecto.");
        }

        // Para DNI, CUIL, CDI, LE, LC, pasaporte y CUIT extranjero no se aplican
        // reglas locales adicionales que ARCA no haya definido de forma inequívoca.
        return new(true);
    }
}
