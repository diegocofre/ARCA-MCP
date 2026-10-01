using dcArca.Core.Services;
using Xunit;

namespace dcArca.Core.Tests;

public class dcWsaaTokenStoreTests
{
    [Fact]
    public async Task MemoryStore_RoundTripEInvalidacion()
    {
        var store = new MemoryWsaaTokenStore();
        var entry = new WsaaTokenEntry("token-secreto", "sign-secreto", DateTime.UtcNow.AddHours(1));

        await store.WriteAsync("clave", entry);

        Assert.Equal(entry, await store.ReadAsync("clave"));

        await store.RemoveAsync("clave");
        Assert.Null(await store.ReadAsync("clave"));
    }

    [Fact]
    public async Task FileSystemStore_RoundTrip()
    {
        var directory = TempDirectory();
        try
        {
            var store = new FileSystemWsaaTokenStore(directory);
            var entry = new WsaaTokenEntry("token-secreto", "sign-secreto", DateTime.UtcNow.AddHours(1));

            await using (await store.AcquireLockAsync("20123456786_wsfe"))
            {
                await store.WriteAsync("20123456786_wsfe", entry);
            }

            Assert.Equal(entry, await store.ReadAsync("20123456786_wsfe"));
        }
        finally
        {
            Cleanup(directory);
        }
    }

    [Fact]
    public async Task AuthService_DescartaEntradaExpirada()
    {
        var directory = TempDirectory();
        var store = new FileSystemWsaaTokenStore(directory);
        const string cuit = "20123456786";
        const string key = cuit + "_wsfe";

        try
        {
            await store.WriteAsync(key, new WsaaTokenEntry("vencido", "firma", DateTime.UtcNow.AddMinutes(-1)));
            var auth = new dcArcaAuthService(
                "https://example.test/wsaa",
                Path.Combine(directory, "certificado-inexistente.pfx"),
                "",
                cuit,
                tokenStore: store);

            await Assert.ThrowsAnyAsync<Exception>(() => auth.GetTokenAsync());

            Assert.Null(await store.ReadAsync(key));
        }
        finally
        {
            Cleanup(directory);
        }
    }

    [Fact]
    public async Task FileSystemStore_UsaPermisosMinimosEnUnix()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var directory = TempDirectory();
        try
        {
            var store = new FileSystemWsaaTokenStore(directory);
            const string key = "20123456786_wsfe";
            await store.WriteAsync(key, new WsaaTokenEntry("token", "sign", DateTime.UtcNow.AddHours(1)));

            var file = Path.Combine(directory, "wsaa_token_20123456786_wsfe.json");
            var fileMode = File.GetUnixFileMode(file);
            var directoryMode = File.GetUnixFileMode(directory);

            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, fileMode);
            Assert.Equal(
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute,
                directoryMode);
        }
        finally
        {
            Cleanup(directory);
        }
    }

    private static string TempDirectory()
        => Path.Combine(Path.GetTempPath(), "dcArca-tests", Guid.NewGuid().ToString("N"));

    private static void Cleanup(string directory)
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
