using System;
using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Zirve.Sdk.Config;

namespace Zirve.Sdk.Trace;

public class TraceManager
{
    private readonly HttpClient _httpClient;
    private readonly string _endpoint;
    private readonly string _appName;

    public TraceManager(HttpClient httpClient, ConfigManager configManager)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        
        var cfg = configManager.Module("trace");
        _endpoint = (cfg.GetValueOrDefault("endpoint", "http://opentelemetry-collector.zirve-infra.svc.cluster.local:4318") ?? "").TrimEnd('/');
        _appName = Environment.GetEnvironmentVariable("APP_NAME") ?? "zirve-dotnet";
    }

    public async Task<string?> SendSpanAsync(string name, DateTimeOffset startTime, DateTimeOffset endTime, string traceId, string parentId = "", object? attributes = null)
    {
        if (string.IsNullOrEmpty(_endpoint)) return null;

        var spanId = Guid.NewGuid().ToString("N").Substring(0, 16);
        
        var startNano = (startTime.ToUnixTimeMilliseconds() * 1000000L).ToString();
        var endNano = (endTime.ToUnixTimeMilliseconds() * 1000000L).ToString();

        var payload = new
        {
            resourceSpans = new[]
            {
                new
                {
                    resource = new
                    {
                        attributes = new[]
                        {
                            new { key = "service.name", value = new { stringValue = _appName } },
                            new { key = "environment", value = new { stringValue = Environment.GetEnvironmentVariable("APP_ENV") ?? "production" } }
                        }
                    },
                    scopeSpans = new[]
                    {
                        new
                        {
                            spans = new[]
                            {
                                new
                                {
                                    traceId = traceId,
                                    spanId = spanId,
                                    parentSpanId = parentId,
                                    name = name,
                                    kind = 1,
                                    startTimeUnixNano = startNano,
                                    endTimeUnixNano = endNano,
                                    status = new { code = 1 }
                                }
                            }
                        }
                    }
                }
            }
        };

        try
        {
            var content = new StringContent(JsonSerializer.Serialize(payload), System.Text.Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync($"{_endpoint}/v1/traces", content);
            
            if (response.IsSuccessStatusCode) return spanId;
            return null;
        }
        catch
        {
            return null;
        }
    }

    public Task<bool> HealthAsync()
    {
        return Task.FromResult(!string.IsNullOrEmpty(_endpoint));
    }
}
