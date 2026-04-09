using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Zirve.Sdk.Config;

namespace Zirve.Sdk.Quality;

public class QualityManager
{
    private readonly HttpClient _httpClient;
    private readonly string _url;
    private readonly string _token;

    public QualityManager(HttpClient httpClient, ConfigManager configManager)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        
        var cfg = configManager.Module("quality");
        _url = (cfg.GetValueOrDefault("url", "http://sonarqube-sonarqube.zirve-infra.svc.cluster.local:9000") ?? "").TrimEnd('/') + "/api";
        _token = cfg.GetValueOrDefault("token", "") ?? "";
    }

    private async Task<JsonElement> RequestAsync(HttpMethod method, string path)
    {
        var request = new HttpRequestMessage(method, $"{_url}/{path.TrimStart('/')}");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _token);

        var response = await _httpClient.SendAsync(request);
        
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return default;

        response.EnsureSuccessStatusCode();

        if (response.StatusCode == System.Net.HttpStatusCode.NoContent || response.Content.Headers.ContentLength == 0)
            return default;

        using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        return doc.RootElement.Clone();
    }

    public async Task<string> GetQualityGateAsync(string projectKey)
    {
        var res = await RequestAsync(HttpMethod.Get, $"qualitygates/project_status?projectKey={Uri.EscapeDataString(projectKey)}");
        if (res.ValueKind == JsonValueKind.Object && 
            res.TryGetProperty("projectStatus", out var projectStatus) && 
            projectStatus.TryGetProperty("status", out var status))
        {
            return status.GetString() ?? "UNKNOWN";
        }
        return "UNKNOWN";
    }

    public async Task<bool> CheckPassedAsync(string projectKey)
    {
        var status = await GetQualityGateAsync(projectKey);
        return status == "OK";
    }

    public async Task<bool> HealthAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_url}/system/health");
            if (response.IsSuccessStatusCode)
            {
                using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
                if (doc.RootElement.TryGetProperty("health", out var health))
                {
                    return health.GetString() == "GREEN";
                }
            }
            return false;
        }
        catch
        {
            return false;
        }
    }
}
