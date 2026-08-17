using WindowsDiagnosticApp.Services;
using Xunit;

namespace WindowsDiagnosticApp.Tests;

public sealed class OllamaUrlValidatorTests
{
    [Theory]
    [InlineData("http://127.0.0.1:11434")]
    [InlineData("http://localhost:11434")]
    [InlineData("http://[::1]:11434")]
    public void Validate_AllowsLoopback(string url)
    {
        var result = OllamaUrlValidator.Validate(url, allowPrivateNetwork: false);
        Assert.True(result.IsValid);
        Assert.True(result.IsLocal);
    }

    [Theory]
    [InlineData("http://8.8.8.8")]
    [InlineData("https://example.com")]
    [InlineData("http://api.openai.com")]
    public void Validate_RejectsPublicTargets(string url)
    {
        var result = OllamaUrlValidator.Validate(url, allowPrivateNetwork: true);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validate_PrivateNetworkOnlyWhenAllowed()
    {
        Assert.False(OllamaUrlValidator.Validate("http://192.168.1.50:11434", allowPrivateNetwork: false).IsValid);

        var allowed = OllamaUrlValidator.Validate("http://192.168.1.50:11434", allowPrivateNetwork: true);
        Assert.True(allowed.IsValid);
        Assert.False(allowed.IsLocal);
    }

    [Theory]
    [InlineData("ftp://127.0.0.1")]
    [InlineData("not-a-url")]
    [InlineData("")]
    public void Validate_RejectsInvalidSchemesAndGarbage(string url)
    {
        Assert.False(OllamaUrlValidator.Validate(url, allowPrivateNetwork: true).IsValid);
    }
}
