namespace WindowsDiagnosticApp.Infrastructure;

/// <summary>
/// Liefert das gebaute React-Frontend aus und injiziert das Sitzungstoken in die
/// index.html, damit das Frontend es bei API-Anfragen mitsenden kann.
/// </summary>
public static class StaticFrontend
{
    public static void MapFrontend(this WebApplication app, SessionToken token)
    {
        app.UseDefaultFiles();
        app.UseStaticFiles();

        app.MapFallback(async context =>
        {
            var indexPath = Path.Combine(app.Environment.WebRootPath ?? "wwwroot", "index.html");

            if (!File.Exists(indexPath))
            {
                context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                context.Response.ContentType = "text/plain; charset=utf-8";
                await context.Response.WriteAsync(
                    "Das Frontend wurde noch nicht gebaut. Bitte führen Sie zuerst den Frontend-Build aus " +
                    "(siehe README, Abschnitt Build).");
                return;
            }

            var html = await File.ReadAllTextAsync(indexPath, context.RequestAborted);
            html = html.Replace(SessionToken.Placeholder, token.Value, StringComparison.Ordinal);

            context.Response.ContentType = "text/html; charset=utf-8";
            await context.Response.WriteAsync(html, context.RequestAborted);
        });
    }
}
