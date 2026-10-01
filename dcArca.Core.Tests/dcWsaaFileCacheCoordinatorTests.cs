using dcArca.Core.Services;
using Xunit;

namespace dcArca.Core.Tests;

public class dcWsaaFileCacheCoordinatorTests
{
    [Fact]
    public async Task MismaClave_DosInstancias_SoloUnaRenovacionSimulada()
    {
        var path = TempCachePath();
        try
        {
            var first = new dcWsaaFileCacheCoordinator(path);
            var second = new dcWsaaFileCacheCoordinator(path);
            var refreshCount = 0;

            async Task<string> GetOrRefreshAsync(dcWsaaFileCacheCoordinator coordinator)
            {
                await using var lease = await coordinator.AcquireAsync();

                if (File.Exists(path))
                {
                    return await File.ReadAllTextAsync(path);
                }

                Interlocked.Increment(ref refreshCount);
                await Task.Delay(75);
                coordinator.WriteAllTextAtomic("token-renovado");
                return "token-renovado";
            }

            var results = await Task.WhenAll(GetOrRefreshAsync(first), GetOrRefreshAsync(second));

            Assert.Equal(1, refreshCount);
            Assert.All(results, value => Assert.Equal("token-renovado", value));
        }
        finally
        {
            Cleanup(path);
        }
    }

    [Fact]
    public async Task SegundaInstancia_ReutilizaValorEscritoPorLaPrimera()
    {
        var path = TempCachePath();
        try
        {
            var first = new dcWsaaFileCacheCoordinator(path);
            var second = new dcWsaaFileCacheCoordinator(path);

            await using (await first.AcquireAsync())
            {
                first.WriteAllTextAtomic("primero");
            }

            await using (await second.AcquireAsync())
            {
                Assert.Equal("primero", await File.ReadAllTextAsync(path));
            }
        }
        finally
        {
            Cleanup(path);
        }
    }

    [Fact]
    public async Task ClavesDiferentes_NoSeBloqueanEntreSi()
    {
        var path1 = TempCachePath();
        var path2 = TempCachePath();

        try
        {
            var first = new dcWsaaFileCacheCoordinator(path1);
            var second = new dcWsaaFileCacheCoordinator(path2);

            await using var lease1 = await first.AcquireAsync();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
            await using var lease2 = await second.AcquireAsync(cts.Token);
        }
        finally
        {
            Cleanup(path1);
            Cleanup(path2);
        }
    }

    [Fact]
    public async Task TemporalIncompleto_NoReemplazaCacheValido()
    {
        var path = TempCachePath();
        try
        {
            var coordinator = new dcWsaaFileCacheCoordinator(path);
            await using (await coordinator.AcquireAsync())
            {
                coordinator.WriteAllTextAtomic("cache-valido");
            }

            var interruptedTemp = path + ".interrumpido.tmp";
            await File.WriteAllTextAsync(interruptedTemp, "parcial");

            Assert.Equal("cache-valido", await File.ReadAllTextAsync(path));
        }
        finally
        {
            Cleanup(path);
        }
    }

    private static string TempCachePath()
    {
        var directory = Path.Combine(Path.GetTempPath(), "dcArca-tests", Guid.NewGuid().ToString("N"));
        return Path.Combine(directory, "wsaa_token.json");
    }

    private static void Cleanup(string cachePath)
    {
        var directory = Path.GetDirectoryName(cachePath);
        if (directory != null && Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
