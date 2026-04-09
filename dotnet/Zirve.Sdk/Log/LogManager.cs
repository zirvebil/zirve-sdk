using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Zirve.Sdk.Config;

namespace Zirve.Sdk.Log;

public class LogManager : IAsyncDisposable
{
    private readonly HttpClient _httpClient;
    private readonly string _url;
    private readonly string _appName;
    private readonly ConcurrentQueue<Dictionary<string, string>> _buffer = new();
    private readonly Timer _timer;
    private bool _isDisposing;

    public LogManager(HttpClient httpClient, ConfigManager configManager)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        
        var cfg = configManager.Module("log");
        _url = (cfg.GetValueOrDefault("url", "http://loki.zirve-infra.svc.cluster.local:3100") ?? "").TrimEnd('/');
        _appName = Environment.GetEnvironmentVariable("APP_NAME") ?? "zirve-dotnet";

        // Push logs every 3 seconds
        _timer = new Timer(FlushCallback, null, TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(3));
    }

    private void FlushCallback(object? state)
    {
        _ = FlushAsync();
    }

    private async Task FlushAsync()
    {
        if (_buffer.IsEmpty || _isDisposing) return;

        var entries = new List<object>();
        while (_buffer.TryDequeue(out var log))
        {
            var ts = log["timestamp"];
            log.Remove("timestamp");
            
            entries.Add(new[] { ts, JsonSerializer.Serialize(log) });
        }

        if (entries.Count == 0) return;

        var payload = new
        {
            streams = new[]
            {
                new
                {
                    stream = new { app = _appName, env = Environment.GetEnvironmentVariable("APP_ENV") ?? "production" },
                    values = entries
                }
            }
        };

        try
        {
            var content = new StringContent(JsonSerializer.Serialize(payload), System.Text.Encoding.UTF8, "application/json");
            await _httpClient.PostAsync($"{_url}/loki/api/v1/push", content);
        }
        catch
        {
            // Logging failure should not crash the app. Silently drop or write to stdout.
            Console.WriteLine("Warning: Zirve SDK failed to push logs to Loki.");
        }
    }

    public void Info(string message, Dictionary<string, object>? context = null) => Write("info", message, context);
    public void Error(string message, Dictionary<string, object>? context = null) => Write("error", message, context);
    public void Warn(string message, Dictionary<string, object>? context = null) => Write("warn", message, context);
    public void Debug(string message, Dictionary<string, object>? context = null) => Write("debug", message, context);

    private void Write(string level, string message, Dictionary<string, object>? context)
    {
        if (_isDisposing) return;

        // Nanosecond timestamp string
        var ts = (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1000000L).ToString();
        var ctxStr = context != null ? JsonSerializer.Serialize(context) : "{}";

        _buffer.Enqueue(new Dictionary<string, string>
        {
            { "timestamp", ts },
            { "level", level },
            { "message", message },
            { "context", ctxStr }
        });
    }

    public async Task<bool> HealthAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_url}/ready");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async ValueTask DisposeAsync()
    {
        _isDisposing = true;
        _timer?.Dispose();
        await FlushAsync();
    }
}
