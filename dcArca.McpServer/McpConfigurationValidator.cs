namespace dcArca.McpServer;

public static class McpConfigurationValidator
{
    public static void ValidateJwt(string authority, string audience, bool isDevelopment)
    {
        if (!Uri.TryCreate(authority, UriKind.Absolute, out var authorityUri))
        {
            throw new InvalidOperationException("Jwt:Authority debe ser una URI absoluta.");
        }

        if (!isDevelopment && authorityUri.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidOperationException("Jwt:Authority debe usar HTTPS fuera del ambiente Development.");
        }

        if (string.IsNullOrWhiteSpace(audience))
        {
            throw new InvalidOperationException("Jwt:Audience no puede estar vacío.");
        }
    }
}
