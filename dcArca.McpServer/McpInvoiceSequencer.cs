using System.Collections.Concurrent;
using dcArca.Core.Models;
using dcArca.Core.Services;

namespace dcArca.McpServer;

public sealed class McpInvoiceSequencer
{
    private readonly IdcWsfeClient _wsfe;
    private readonly dcArcaConfig _config;
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> Locks = new();

    public McpInvoiceSequencer(IdcWsfeClient wsfe, dcArcaConfig config)
    {
        _wsfe = wsfe;
        _config = config;
    }

    public async Task<dcFacturaResponse> EmitAsync(
        dcFacturaRequest factura,
        CancellationToken cancellationToken = default)
    {
        if (!factura.TipoComprobante.HasValue)
        {
            return new dcFacturaResponse
            {
                Success = false,
                Codigo = "TIPOC_INVALID",
                Mensaje = "TipoComprobante es obligatorio para emitir con numeración server-side.",
                Errores = ["TipoComprobante es obligatorio para emitir con numeración server-side."]
            };
        }

        var tipo = factura.TipoComprobante.Value;
        var key = $"{_config.Cuit}:{_config.PuntoVenta}:{(int)tipo}";
        var gate = Locks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));

        await gate.WaitAsync(cancellationToken);
        try
        {
            var ultimo = await _wsfe.FECompUltimoAutorizadoAsync(tipo, cancellationToken);
            if (!ultimo.Success)
            {
                return ultimo;
            }

            factura.NumeroComprobante = ultimo.NumeroComprobante + 1;
            return await _wsfe.FECAESolicitarAsync(factura, cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }
}
