using System.Text.Json;
using Microsoft.Extensions.Options;
using WindowsDiagnosticApp.Models;
using WindowsDiagnosticApp.Options;
using WindowsDiagnosticApp.Services;
using Xunit;

namespace WindowsDiagnosticApp.Tests;

public sealed class DiagnosticActionCatalogTests
{
    private static DiagnosticActionCatalog CreateCatalog() =>
        new(Microsoft.Extensions.Options.Options.Create(new EventOptions()));

    private static JsonElement Json(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }

    [Fact]
    public void ValidateCall_UnknownActionId_IsRejected()
    {
        var result = CreateCatalog().ValidateCall("system.reboot", Json("{}"));
        Assert.False(result.IsValid);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public void ValidateCall_UnknownParameter_IsRejected()
    {
        var result = CreateCatalog().ValidateCall("events.query", Json("""{ "rawCommand": "shutdown /r" }"""));
        Assert.False(result.IsValid);
    }

    [Fact]
    public void ValidateCall_FreeCommandFields_AreRejected()
    {
        // Ein freier Befehl darf niemals als Parameter durchkommen.
        var result = CreateCatalog().ValidateCall("events.query", Json("""{ "command": "Get-Process | Stop-Process" }"""));
        Assert.False(result.IsValid);
    }

    [Fact]
    public void ValidateCall_DisallowedLog_IsRejected()
    {
        var result = CreateCatalog().ValidateCall("events.query", Json("""{ "logNames": ["Security"] }"""));
        Assert.False(result.IsValid);
    }

    [Fact]
    public void ValidateCall_InvalidLevel_IsRejected()
    {
        var result = CreateCatalog().ValidateCall("events.query", Json("""{ "levels": ["Verbose"] }"""));
        Assert.False(result.IsValid);
    }

    [Theory]
    [InlineData("""{ "sinceHours": 0 }""")]
    [InlineData("""{ "sinceHours": 100000 }""")]
    [InlineData("""{ "maximumResults": 0 }""")]
    [InlineData("""{ "maximumResults": 999999 }""")]
    public void ValidateCall_OutOfRange_IsRejected(string json)
    {
        var result = CreateCatalog().ValidateCall("events.query", Json(json));
        Assert.False(result.IsValid);
    }

    [Fact]
    public void ValidateCall_EmptyParameters_UsesSafeDefaults()
    {
        var result = CreateCatalog().ValidateCall("events.query", Json("{}"));
        Assert.True(result.IsValid);
        var parameters = Assert.IsType<EventsQueryParameters>(result.Parameters);
        Assert.NotEmpty(parameters.LogNames);
        Assert.NotEmpty(parameters.Levels);
        Assert.InRange(parameters.SinceHours, 1, 168);
        Assert.InRange(parameters.MaximumResults, 1, 500);
    }

    [Fact]
    public void ValidateCall_ValidFullParameters_AreAccepted()
    {
        var json = """
        { "logNames": ["System"], "providers": ["stornvme", "disk"],
          "levels": ["Error", "Warning"], "sinceHours": 24, "maximumResults": 100 }
        """;
        var result = CreateCatalog().ValidateCall("events.query", Json(json));

        Assert.True(result.IsValid);
        var parameters = Assert.IsType<EventsQueryParameters>(result.Parameters);
        Assert.Equal(new[] { "System" }, parameters.LogNames);
        Assert.Equal(new[] { "stornvme", "disk" }, parameters.Providers);
        Assert.Equal(24, parameters.SinceHours);
        Assert.Equal(100, parameters.MaximumResults);
    }

    [Fact]
    public void Catalog_ExposesEventsQueryAsReadOnlyR0()
    {
        var definition = CreateCatalog().Find("events.query");
        Assert.NotNull(definition);
        Assert.False(definition!.ChangesSystem);
        Assert.Equal(ActionRiskLevel.R0, definition.RiskLevel);
        Assert.False(definition.RequiresAdministrator);
    }

    [Theory]
    [InlineData("winget.status")]
    [InlineData("winget.sources.list")]
    [InlineData("appinstaller.status")]
    [InlineData("windowsupdate.status")]
    [InlineData("storage.summary")]
    [InlineData("network.microsoftEndpoints")]
    public void Catalog_ExposesFixedReadOnlyDiagnosticsWithoutParameters(string actionId)
    {
        var result = CreateCatalog().ValidateCall(actionId, Json("{}"));

        Assert.True(result.IsValid);
        Assert.NotNull(result.Definition);
        Assert.Equal(ActionRiskLevel.R0, result.Definition!.RiskLevel);
        Assert.False(result.Definition.ChangesSystem);
        Assert.False(result.Definition.RequiresConfirmation);
        Assert.IsType<EmptyDiagnosticParameters>(result.Parameters);
    }

    [Fact]
    public void Catalog_RejectsParametersForFixedDiagnostics()
    {
        var result = CreateCatalog().ValidateCall("winget.status", Json("{ \"command\": \"winget source reset\" }"));

        Assert.False(result.IsValid);
    }
}
