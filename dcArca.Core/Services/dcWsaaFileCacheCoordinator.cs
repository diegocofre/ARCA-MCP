using System.Text;

namespace dcArca.Core.Services;

/// <summary>
/// Coordinates access to one WSAA filesystem cache across processes on the same host/filesystem.
/// </summary>
internal sealed class dcWsaaFileCacheCoordinator
{
    private readonly string _cachePath;
    private readonly string _lockPath;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(25);

    internal dcWsaaFileCacheCoordinator(string cachePath)
    {
        _cachePath = cachePath ?? throw new ArgumentNullException(nameof(cachePath));
        _lockPath = cachePath + ".lock";
    }

    internal async ValueTask<IAsyncDisposable> AcquireAsync(CancellationToken cancellationToken = default)
    {
        EnsureDirectory();

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var stream = new FileStream(
                    _lockPath,
                    FileMode.OpenOrCreate,
                    FileAccess.ReadWrite,
                    FileShare.None,
                    bufferSize: 1,
                    FileOptions.None);

                return new Releaser(stream);
            }
            catch (IOException)
            {
                await Task.Delay(RetryDelay, cancellationToken);
            }
        }
    }

    internal void WriteAllTextAtomic(string content)
    {
        EnsureDirectory();
        var tempPath = _cachePath + "." + Guid.NewGuid().ToString("N") + ".tmp";

        try
        {
            File.WriteAllText(tempPath, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            File.Move(tempPath, _cachePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    private void EnsureDirectory()
    {
        var directory = Path.GetDirectoryName(_cachePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    private sealed class Releaser : IAsyncDisposable
    {
        private FileStream? _stream;

        internal Releaser(FileStream stream) => _stream = stream;

        public ValueTask DisposeAsync()
        {
            Interlocked.Exchange(ref _stream, null)?.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
