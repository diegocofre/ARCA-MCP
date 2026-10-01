namespace dcArca.Core.Models;

/// <summary>
/// Estado semántico de una solicitud de emisión WSFE.
/// </summary>
public enum dcEmissionOutcome
{
    None = 0,
    Authorized = 1,
    FiscalRejected = 2,
    RecoveredSuccess = 3,
    Uncertain = 4
}
