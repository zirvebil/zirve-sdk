using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Zirve.Sdk.Config;

namespace Zirve.Sdk.Cluster;

public class ClusterManager
{
    private readonly HttpClient _httpClient;
    private readonly string _url;
    private readonly string _token;

    public ClusterManager(HttpClient httpClient, ConfigManager configManager)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        
        var cfg = configManager.Module("cluster");
        _url = (cfg.GetValueOrDefault("url", "http://rancher.cattle-system.svc.cluster.local") ?? "").TrimEnd('/') + "/v3";
        _token = cfg.GetValueOrDefault("token", "") ?? "";
    }

    private async Task<JsonElement> RequestAsync(HttpMethod method, string path, object? body = null)
    {
        var request = new HttpRequestMessage(method, $"{_url}/{path.TrimStart('/')}");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _token);
        
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

    public async Task<JsonElement> ListClustersAsync()
    {
        var res = await RequestAsync(HttpMethod.Get, "clusters");
        if (res.ValueKind == JsonValueKind.Object && res.TryGetProperty("data", out var data))
        {
            return data;
        }
        return res;
    }

    public async Task<JsonElement> GetClusterAsync(string clusterId)
    {
        return await RequestAsync(HttpMethod.Get, $"clusters/{Uri.EscapeDataString(clusterId)}");
    }

    public async Task<bool> IsHealthyAsync(string clusterId)
    {
        var cluster = await GetClusterAsync(clusterId);
        if (cluster.ValueKind == JsonValueKind.Object && cluster.TryGetProperty("state", out var state))
        {
            return state.GetString() == "active";
        }
        return false;
    }

    public async Task<bool> HealthAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync(_url);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}
