using System.Net;
using System.Net.Sockets;

namespace WindowsDiagnosticApp.Services;

public sealed record UrlValidationResult(bool IsValid, bool IsLocal, string? Error, string? NormalizedUrl);

/// <summary>
/// Validiert die Ollama-Basisadresse serverseitig und verhindert SSRF gegen
/// beliebige externe Ziele. Erlaubt sind Loopback und – falls aktiviert – private
/// Netzwerkbereiche. Öffentliche Ziele werden immer abgelehnt.
/// </summary>
public static class OllamaUrlValidator
{
    public static UrlValidationResult Validate(string? url, bool allowPrivateNetwork)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return new UrlValidationResult(false, false, "Es wurde keine Adresse angegeben.", null);
        }

        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri))
        {
            return new UrlValidationResult(false, false, "Die Adresse ist ungültig.", null);
        }

        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
        {
            return new UrlValidationResult(false, false, "Nur http- oder https-Adressen sind erlaubt.", null);
        }

        var host = uri.Host;
        var isLoopback = host is "localhost" or "127.0.0.1" or "::1";

        if (isLoopback)
        {
            return new UrlValidationResult(true, true, null, Normalize(uri));
        }

        if (IPAddress.TryParse(host, out var ip))
        {
            if (IPAddress.IsLoopback(ip))
            {
                return new UrlValidationResult(true, true, null, Normalize(uri));
            }

            if (IsPrivate(ip))
            {
                return allowPrivateNetwork
                    ? new UrlValidationResult(true, false, null, Normalize(uri))
                    : new UrlValidationResult(false, false,
                        "Adressen im privaten Netzwerk sind derzeit nicht erlaubt.", null);
            }

            return new UrlValidationResult(false, false,
                "Nur lokale oder private Adressen sind erlaubt. Öffentliche Ziele sind gesperrt.", null);
        }

        // Nicht auflösbarer Hostname (kein Loopback, keine IP) → abgelehnt.
        return new UrlValidationResult(false, false,
            "Nur lokale Adressen (127.0.0.1, localhost) oder private IP-Adressen sind erlaubt.", null);
    }

    private static string Normalize(Uri uri) => uri.GetLeftPart(UriPartial.Authority);

    private static bool IsPrivate(IPAddress ip)
    {
        if (ip.AddressFamily == AddressFamily.InterNetworkV6)
        {
            return ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal;
        }

        var bytes = ip.GetAddressBytes();
        return bytes[0] switch
        {
            10 => true,
            172 => bytes[1] >= 16 && bytes[1] <= 31,
            192 => bytes[1] == 168,
            169 => bytes[1] == 254,
            _ => false
        };
    }
}
