namespace WindowsDiagnosticApp.Infrastructure;

/// <summary>Wartet, bis der lokale Webserver über den Health-Endpunkt erreichbar ist.</summary>
public static class ServerReadyWaiter
{
    public static async Task<bool> WaitAsync(string baseUrl, CancellationToken cancellationToken = default)
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        var healthUrl = $"{baseUrl.TrimEnd('/')}/api/health";
        var deadline = DateTime.UtcNow.AddSeconds(20);

        while (DateTime.UtcNow < deadline && !cancellationToken.IsCancellationRequested)
        {
            try
            {
                using var response = await client.GetAsync(healthUrl, cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    return true;
                }
            }
            catch
            {
                // Server noch nicht bereit – erneut versuchen.
            }

            await Task.Delay(200, cancellationToken);
        }

        return false;
    }
}
