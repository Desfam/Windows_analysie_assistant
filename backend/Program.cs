using System.Text.Json.Serialization;
using WindowsDiagnosticApp.Endpoints;
using WindowsDiagnosticApp.Infrastructure;
using WindowsDiagnosticApp.Options;
using WindowsDiagnosticApp.Services;

const string Url = "http://127.0.0.1:5187";
const string MutexName = @"Global\WindowsDiagnosticApp_SingleInstance_5187";

// Im Testbetrieb werden Single-Instance-Sperre und Browserstart deaktiviert.
var isTestHost = Environment.GetEnvironmentVariable("WDA_TEST_HOST") == "1";

Mutex? mutex = null;
if (!isTestHost)
{
    // Single-Instance-Mechanismus: Läuft bereits eine Instanz, wird nur der Browser
    // erneut geöffnet und diese zweite Instanz sauber beendet.
    mutex = new Mutex(initiallyOwned: true, MutexName, out var isNewInstance);
    if (!isNewInstance)
    {
        BrowserLauncher.Open(Url);
        mutex.Dispose();
        return;
    }
}

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.UseUrls(Url);

builder.Services.Configure<ThresholdOptions>(builder.Configuration.GetSection(ThresholdOptions.SectionName));
builder.Services.Configure<EventOptions>(builder.Configuration.GetSection(EventOptions.SectionName));

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.Never;
});

var sessionToken = SessionToken.Create();
builder.Services.AddSingleton(sessionToken);

builder.Services.AddSingleton<HealthEvaluator>();
builder.Services.AddSingleton<KnownEventCatalog>();
builder.Services.AddSingleton<EventGrouper>();
builder.Services.AddSingleton<EventQueryParser>();
builder.Services.AddSingleton<ISystemInfoService, SystemInfoService>();
builder.Services.AddSingleton<IEventLogService, EventLogService>();

var app = builder.Build();

app.UseMiddleware<GlobalExceptionMiddleware>();
app.UseMiddleware<SessionTokenMiddleware>();

app.MapApiEndpoints();
app.MapFrontend(sessionToken);

app.Lifetime.ApplicationStarted.Register(() =>
{
    if (isTestHost)
    {
        return;
    }

    _ = Task.Run(async () =>
    {
        if (await ServerReadyWaiter.WaitAsync(Url))
        {
            BrowserLauncher.Open(Url);
        }
    });
});

app.Logger.LogInformation("Windows Diagnose Assistent gestartet unter {Url}", Url);

app.Run();
mutex?.Dispose();

/// <summary>Einstiegspunkt sichtbar machen, damit Tests die Anwendung starten können.</summary>
public partial class Program;
