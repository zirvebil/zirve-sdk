using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Zirve.Sdk.Config;

namespace Zirve.Sdk.Metrics;

public class MetricsManager
{
    private readonly HttpClient _httpClient;
    private readonly string _url;
    private readonly string _jobName;

    public MetricsManager(HttpClient httpClient, ConfigManager configManager)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        
        var cfg = configManager.Module("metrics");
        _url = (cfg.GetValueOrDefault("url", "http://prometheus-server.zirve-infra.svc.cluster.local:9090") ?? "").TrimEnd('/');
        _jobName = Environment.GetEnvironmentVariable("APP_NAME") ?? "zirve-dotnet";
    }

    public async Task<bool> PushAsync(List<Dictionary<string, object>> metrics)
    {
        if (metrics.Count == 0) return true;

        var body = "";
        foreach (var m in metrics)
        {
            if (!m.TryGetValue("name", out var n) || !m.TryGetValue("value", out var v)) continue;
            
            var name = n.ToString()!;
            var val = v.ToString()!; // Will automatically use invariant culture if stringified correctly previous step
            var type = m.TryGetValue("type", out var t) ? t.ToString() : "gauge";
            var help = m.TryGetValue("help", out var h) ? h.ToString() : "Zirve SDK Metric";

            body += $"# HELP {name} {help}\n";
            body += $"# TYPE {name} {type}\n";
            body += $"{name} {val}\n";
        }

        try
        {
            var content = new StringContent(body, System.Text.Encoding.UTF8, "text/plain");
            // Usually pushgateway uses different port but fallback URL logic relies on configuration
            // assuming configured specifically for Pushgateway
            var response = await _httpClient.PostAsync($"{_url}/metrics/job/{Uri.EscapeDataString(_jobName)}", content);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public Task<bool> HealthAsync()
    {
        return Task.FromResult(!string.IsNullOrEmpty(_url));
    }
}
