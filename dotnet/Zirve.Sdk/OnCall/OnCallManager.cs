using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Zirve.Sdk.Config;

namespace Zirve.Sdk.OnCall;

public class OnCallManager
{
    private readonly HttpClient _httpClient;
    private readonly string _url;
    private readonly string _token;

    public OnCallManager(HttpClient httpClient, ConfigManager configManager)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        
        var cfg = configManager.Module("oncall");
        _url = (cfg.GetValueOrDefault("url", "http://grafana-oncall-engine.zirve-infra.svc.cluster.local:8080") ?? "").TrimEnd('/') + "/api/v1";
        _token = cfg.GetValueOrDefault("token", "") ?? "";
    }

    private async Task<JsonElement> RequestAsync(HttpMethod method, string path, object? body = null)
    {
        var request = new HttpRequestMessage(method, $"{_url}/{path.TrimStart('/')}");
        request.Headers.Add("Authorization", $"Token {_token}");
        
        if (body != null)
        {
            var json = body is string s ? s : JsonSerializer.Serialize(body);
            request.Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        }

        var response = await _httpClient.SendAsync(request);
        
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return default;

        response.EnsureSuccessStatusCode();

        if (response.StatusCode == System.Net.HttpStatusCode.NoContent || response.Content.Headers.ContentLength == 0)
            return default;

        using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        return doc.RootElement.Clone();
    }

    public async Task<bool> CreateAlertAsync(string integrationUrl, string title, string message, string state = "alerting")
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Post, integrationUrl)
            {
                Content = new StringContent(JsonSerializer.Serialize(new
                {
                    title,
                    message,
                    state
                }), System.Text.Encoding.UTF8, "application/json")
            };
            
            var response = await _httpClient.SendAsync(request);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<JsonElement> ListIncidentsAsync(string state = "triggered")
    {
        var res = await RequestAsync(HttpMethod.Get, $"alert_groups?state={Uri.EscapeDataString(state)}");
        if (res.ValueKind == JsonValueKind.Object && res.TryGetProperty("results", out var results))
        {
            return results;
        }
        return res;
    }

    public async Task<bool> HealthAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_url}/health");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}
