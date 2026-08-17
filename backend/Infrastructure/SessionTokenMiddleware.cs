namespace WindowsDiagnosticApp.Infrastructure;

/// <summary>
/// Schützt die API vor Zugriffen fremder lokaler Webseiten: erzwingt das
/// Sitzungstoken und akzeptiert nur lokale Host-Header.
/// </summary>
public sealed class SessionTokenMiddleware
{
    private readonly RequestDelegate _next;
    private readonly SessionToken _token;
    private readonly bool _enforce;

    public SessionTokenMiddleware(RequestDelegate next, SessionToken token, IWebHostEnvironment environment)
    {
        _next = next;
        _token = token;
        // Im Entwicklungsmodus (Vite-Dev-Server) wird das Token nicht erzwungen,
        // damit die getrennte Frontend-Entwicklung ohne Token-Injektion funktioniert.
        _enforce = !environment.IsDevelopment();
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path;

        if (!_enforce || !path.StartsWithSegments("/api"))
        {
            await _next(context);
            return;
        }

        // Der Health-Endpunkt wird für die Bereitschaftsprüfung ohne Token benötigt.
        if (path.StartsWithSegments("/api/health"))
        {
            await _next(context);
            return;
        }

        if (!IsLocalHost(context) || !HasValidToken(context))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(
                "{\"error\":\"Ungültiges oder fehlendes Sitzungstoken.\"}");
            return;
        }

        await _next(context);
    }

    private bool HasValidToken(HttpContext context)
    {
        if (!context.Request.Headers.TryGetValue(SessionToken.HeaderName, out var provided))
        {
            return false;
        }

        var value = provided.ToString();
        return !string.IsNullOrEmpty(value) &&
               CryptographicEquals(value, _token.Value);
    }

    private static bool IsLocalHost(HttpContext context)
    {
        var host = context.Request.Host.Host;
        return host is "127.0.0.1" or "localhost" or "::1";
    }

    private static bool CryptographicEquals(string a, string b)
    {
        if (a.Length != b.Length)
        {
            return false;
        }

        var result = 0;
        for (var i = 0; i < a.Length; i++)
        {
            result |= a[i] ^ b[i];
        }

        return result == 0;
    }
}
