using dcArca.Core.Models;
using dcArca.Core.Services;
using dcArca.McpServer;
using Xunit;

namespace dcArca.McpServer.Tests;

public class McpInvoiceSequencerTests
{
    [Fact]
    public async Task MismaClave_EmisionesConcurrentes_RecibenNumerosConsecutivos()
    {
        var fake = new FakeWsfeClient();
        var sequencer = new McpInvoiceSequencer(fake, Config(1));

        var first = sequencer.EmitAsync(Request(dcTipoComprobante.FacturaB));
        var second = sequencer.EmitAsync(Request(dcTipoComprobante.FacturaB));

        var results = await Task.WhenAll(first, second);

        Assert.Equal([1L, 2L], results.Select(r => r.NumeroComprobante).OrderBy(x => x).ToArray());
        Assert.Equal([1L, 2L], fake.EmittedNumbers.OrderBy(x => x).ToArray());
    }

    [Fact]
    public async Task TiposDiferentes_NoCompartenLock()
    {
        var fake = new FakeWsfeClient(delayMs: 100);
        var sequencer = new McpInvoiceSequencer(fake, Config(1));

        var a = sequencer.EmitAsync(Request(dcTipoComprobante.FacturaA));
        var b = sequencer.EmitAsync(Request(dcTipoComprobante.FacturaB));

        await Task.WhenAll(a, b);

        Assert.True(fake.MaxConcurrentOperations >= 2);
    }

    [Fact]
    public async Task PuntosDeVentaDiferentes_NoCompartenLock()
    {
        var fake = new FakeWsfeClient(delayMs: 100);
        var one = new McpInvoiceSequencer(fake, Config(1));
        var two = new McpInvoiceSequencer(fake, Config(2));

        await Task.WhenAll(
            one.EmitAsync(Request(dcTipoComprobante.FacturaB)),
            two.EmitAsync(Request(dcTipoComprobante.FacturaB)));

        Assert.True(fake.MaxConcurrentOperations >= 2);
    }

    [Fact]
    public async Task Excepcion_LiberaLock()
    {
        var fake = new FakeWsfeClient { ThrowOnNextIssue = true };
        var sequencer = new McpInvoiceSequencer(fake, Config(1));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sequencer.EmitAsync(Request(dcTipoComprobante.FacturaB)));

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        var result = await sequencer.EmitAsync(Request(dcTipoComprobante.FacturaB), cts.Token);

        Assert.True(result.Success);
    }

    private static dcFacturaRequest Request(dcTipoComprobante tipo) => new()
    {
        TipoComprobante = tipo,
        Concepto = dcConcepto.Productos,
        CuitReceptor = 20123456786,
        TipoDocReceptor = (int)dcTipoDocumento.CUIT,
        CondicionIvaReceptor = dcCondicionIvaReceptor.ResponsableInscripto,
        ImporteNeto = 100m,
        ImporteIva = 21m,
        ImporteTotal = 121m,
        AlicuotaIva = dcAlicuotaIva.Veintiuno,
        FechaComprobante = "20260930"
    };

    private static dcArcaConfig Config(int puntoVenta) => new()
    {
        Cuit = "20123456786",
        PuntoVenta = puntoVenta
    };

    private sealed class FakeWsfeClient : IdcWsfeClient
    {
        private readonly int _delayMs;
        private long _lastNumber;
        private int _activeOperations;
        private int _maxConcurrentOperations;

        internal FakeWsfeClient(int delayMs = 0) => _delayMs = delayMs;

        internal List<long> EmittedNumbers { get; } = new();
        internal int MaxConcurrentOperations => _maxConcurrentOperations;
        internal bool ThrowOnNextIssue { get; set; }

        public async Task<dcFacturaResponse> FECompUltimoAutorizadoAsync(dcTipoComprobante tipoComprobante, CancellationToken cancellationToken = default)
        {
            var active = Interlocked.Increment(ref _activeOperations);
            UpdateMax(active);
            try
            {
                if (_delayMs > 0) await Task.Delay(_delayMs, cancellationToken);
                return new dcFacturaResponse { Success = true, NumeroComprobante = Interlocked.Read(ref _lastNumber) };
            }
            finally
            {
                Interlocked.Decrement(ref _activeOperations);
            }
        }

        public async Task<dcFacturaResponse> FECAESolicitarAsync(dcFacturaRequest factura, CancellationToken cancellationToken = default)
        {
            if (ThrowOnNextIssue)
            {
                ThrowOnNextIssue = false;
                throw new InvalidOperationException("simulated");
            }

            if (_delayMs > 0) await Task.Delay(_delayMs, cancellationToken);
            var number = factura.NumeroComprobante!.Value;
            Interlocked.Exchange(ref _lastNumber, Math.Max(Interlocked.Read(ref _lastNumber), number));
            lock (EmittedNumbers) EmittedNumbers.Add(number);
            return new dcFacturaResponse { Success = true, NumeroComprobante = number };
        }

        public Task<dcFacturaResponse> SolicitarCaeAsync(dcFacturaRequest factura, CancellationToken cancellationToken = default)
            => FECAESolicitarAsync(factura, cancellationToken);

        public Task<dcFacturaResponse> FECompConsultarAsync(long numeroComprobante, dcTipoComprobante tipoComprobante, CancellationToken cancellationToken = default)
            => Task.FromResult(new dcFacturaResponse());

        public Task<List<dcCondicionIvaOption>> GetCondicionesIVAReceptorAsync(int docTipo, long docNro, dcTipoComprobante tipoComprobante, CancellationToken cancellationToken = default)
            => Task.FromResult(new List<dcCondicionIvaOption>());

        private void UpdateMax(int active)
        {
            int snapshot;
            do
            {
                snapshot = _maxConcurrentOperations;
                if (active <= snapshot) return;
            } while (Interlocked.CompareExchange(ref _maxConcurrentOperations, active, snapshot) != snapshot);
        }
    }
}
