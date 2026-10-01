namespace dcArca.Core.Services;

/// <summary>
/// Entrada de credenciales WSAA. Token y Sign son secretos equivalentes a una credencial
/// mientras el ticket siga vigente y no deben registrarse en logs.
/// </summary>
public sealed record WsaaTokenEntry(string Token, string Sign, DateTime Expiration);

/// <summary>
/// Abstracción del almacenamiento de tickets WSAA.
/// Implementaciones persistentes deben proteger los datos en reposo según las necesidades del host.
/// </summary>
public interface IWsaaTokenStore
{
    Task<WsaaTokenEntry?> ReadAsync(string key, CancellationToken cancellationToken = default);
    Task WriteAsync(string key, WsaaTokenEntry entry, CancellationToken cancellationToken = default);
    Task RemoveAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adquiere una exclusión para operaciones read/refresh/write sobre una clave.
    /// Stores distribuidos pueden implementar aquí su mecanismo de coordinación propio.
    /// </summary>
    ValueTask<IAsyncDisposable> AcquireLockAsync(string key, CancellationToken cancellationToken = default);
}
