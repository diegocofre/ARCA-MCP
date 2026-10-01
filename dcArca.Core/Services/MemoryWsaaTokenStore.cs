using System.Collections.Concurrent;

namespace dcArca.Core.Services;

/// <summary>
/// Cache WSAA en memoria. Es el store por defecto y no persiste token/sign en disco.
/// </summary>
public sealed class MemoryWsaaTokenStore : IWsaaTokenStore
{
    private readonly ConcurrentDictionary<string, WsaaTokenEntry> _entries = new();

    public static MemoryWsaaTokenStore Shared { get; } = new();

    public Task<WsaaTokenEntry?> ReadAsync(string key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _entries.TryGetValue(key, out var entry);
        return Task.FromResult<WsaaTokenEntry?>(entry);
    }

    public Task WriteAsync(string key, WsaaTokenEntry entry, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _entries[key] = entry;
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _entries.TryRemove(key, out _);
        return Task.CompletedTask;
    }

    public ValueTask<IAsyncDisposable> AcquireLockAsync(string key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult<IAsyncDisposable>(NoOpLease.Instance);
    }

    private sealed class NoOpLease : IAsyncDisposable
    {
        internal static NoOpLease Instance { get; } = new();
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
