using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Zirve.Sdk.Config;

namespace Zirve.Sdk.Error;

public class ErrorManager
{
    private readonly HttpClient _httpClient;
    private readonly string _dsn;
    private readonly string? _url;
    private readonly string? _key;
    private readonly string? _projectId;

    public ErrorManager(HttpClient httpClient, ConfigManager configManager)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        
        var cfg = configManager.Module("error");
        _dsn = cfg.GetValueOrDefault("dsn", "") ?? "";

        if (!string.IsNullOrEmpty(_dsn) && _dsn.StartsWith("http"))
        {
            var uri = new Uri(_dsn);
            _url = $"{uri.Scheme}://{uri.Host}:{uri.Port}";
            _key = uri.UserInfo;
            _projectId = uri.AbsolutePath.TrimStart('/');
        }
    }

    public async Task<string?> CaptureExceptionAsync(Exception ex, object? context = null)
    {
        return await CaptureAsync("error", ex.Message, ex.StackTrace ?? "", context);
    }

    public async Task<string?> CaptureMessageAsync(string message, string level = "info", object? context = null)
    {
        return await CaptureAsync(level, message, Environment.StackTrace, context);
    }

    private async Task<string?> CaptureAsync(string level, string message, string stackTrace, object? context)
    {
        if (string.IsNullOrEmpty(_url) || string.IsNullOrEmpty(_key) || string.IsNullOrEmpty(_projectId))
        {
            return null; // Silent skip if DSN is missing
        }

        var eventId = Guid.NewGuid().ToString("N");
        var timestamp = DateTime.UtcNow.ToString("o");

        var payload = new
        {
            event_id = eventId,
            timestamp = timestamp,
            platform = "csharp",
            level = level,
            environment = Environment.GetEnvironmentVariable("APP_ENV") ?? "production",
            server_name = Environment.MachineName,
            message = message,
            extra = context ?? new { },
            exception = new
            {
                values = new[]
                {
                    new
                    {
                        type = level == "error" ? "Exception" : "LogMessage",
                        value = message,
                        stacktrace = new
                        {
                            frames = ParseStackTrace(stackTrace)
                        }
                    }
                }
            }
        };

        var request = new HttpRequestMessage(HttpMethod.Post, $"{_url}/api/{_projectId}/store/")
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), System.Text.Encoding.UTF8, "application/json")
        };
        
        request.Headers.Add("X-Sentry-Auth", $"Sentry sentry_version=7, sentry_key={_key}, sentry_client=zirve-dotnet/0.1.0");

        try
        {
            var response = await _httpClient.SendAsync(request);
            if (response.IsSuccessStatusCode)
            {
                return eventId;
            }
            return null;
        }
        catch
        {
            return null;
        }
    }

    private object[] ParseStackTrace(string trace)
    {
        // Simple manual split parsing minimal properties.
        var lines = trace.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var frames = new System.Collections.Generic.List<object>();
        
        foreach (var line in lines)
        {
            frames.Add(new
            {
                filename = "unknown",
                function = line.Trim(),
                lineno = 0
            });
        }
        
        frames.Reverse(); // Sentry expects oldest first
        return frames.ToArray();
    }

    public Task<bool> HealthAsync()
    {
        return Task.FromResult(!string.IsNullOrEmpty(_dsn));
    }
}
