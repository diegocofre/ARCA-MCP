using System.Security.Claims;

namespace dcArca.McpServer;

internal static class ArcaScopeAuthorization
{
    internal static bool HasScope(ClaimsPrincipal user, string requiredScope)
        => user.FindAll("scope").Any(claim =>
            claim.Value
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Contains(requiredScope, StringComparer.Ordinal));
}
