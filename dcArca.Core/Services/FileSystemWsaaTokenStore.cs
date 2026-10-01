using System.Text.Json;

namespace dcArca.Core.Services;

/// <summary>
/// Store filesystem explícito para tickets WSAA.
/// Usa lock inter-proceso y escritura atómica. En Unix intenta aplicar permisos 0700/0600.
/// </summary>
public sealed class FileSystemWsaaTokenStore : IWsaaTokenStore
{
    private readonly string _directory;

    public FileSystemWsaaTokenStore(string? directory = null)
    {
        _directory = string.IsNullOrWhiteSpace(directory)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "dcArca")
            : Path.GetFullPath(directory);

        EnsureDirectory();
    }

    public async Task<WsaaTokenEntry?> ReadAsync(string key, CancellationToken cancellationToken = default)
    {
        var path = GetCachePath(key);
        if (!File.Exists(path))
        {
            return null;
        }

        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return await JsonSerializer.DeserializeAsync<WsaaTokenEntry>(stream, cancellationToken: cancellationToken);
    }

    public Task WriteAsync(string key, WsaaTokenEntry entry, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = GetCachePath(key);
        var json = JsonSerializer.Serialize(entry);
        var coordinator = new dcWsaaFileCacheCoordinator(path);
        coordinator.WriteAllTextAtomic(json);
        TryHardenFile(path);
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = GetCachePath(key);
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    public ValueTask<IAsyncDisposable> AcquireLockAsync(string key, CancellationToken cancellationToken = default)
        => new dcWsaaFileCacheCoordinator(GetCachePath(key)).AcquireAsync(cancellationToken);

    private string GetCachePath(string key)
        => Path.Combine(_directory, $"wsaa_token_{SanitizeKey(key)}.json");

    private static string SanitizeKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("La clave del store WSAA no puede estar vacía.", nameof(key));
        }

        var invalid = Path.GetInvalidFileNameChars();
        return new string(key.Select(ch => invalid.Contains(ch) || ch is '/' or '\\' ? '_' : ch).ToArray());
    }

    private void EnsureDirectory()
    {
        Directory.CreateDirectory(_directory);
        if (!OperatingSystem.IsWindows())
        {
            try
            {
                File.SetUnixFileMode(
                    _directory,
                    UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            }
            catch (Exception) when (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
            {
                // Filesystems que no soportan Unix modes quedan bajo los permisos/ACL del host.
            }
        }
    }

    private static void TryHardenFile(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        try
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
        catch (Exception) when (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            // El host debe aplicar ACL/permisos equivalentes si el filesystem no soporta Unix modes.
        }
    }
}
