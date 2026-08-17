using System.Security.Cryptography;

namespace WindowsDiagnosticApp.Infrastructure;

/// <summary>
/// Zufälliges Sitzungstoken, das beim Start erzeugt wird und die lokale API vor
/// Zugriffen fremder lokaler Webseiten schützt.
/// </summary>
public sealed class SessionToken
{
    public const string HeaderName = "X-Session-Token";
    public const string Placeholder = "__WDA_SESSION_TOKEN__";

    public string Value { get; }

    private SessionToken(string value)
    {
        Value = value;
    }

    public static SessionToken Create()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return new SessionToken(Convert.ToHexString(bytes).ToLowerInvariant());
    }
}
