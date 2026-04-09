using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Zirve.Sdk.Config;

namespace Zirve.Sdk.Deploy;

public class DeployManager
{
    private readonly HttpClient _httpClient;
    private readonly string _url;
    private readonly string _token;

    public DeployManager(HttpClient httpClient, ConfigManager configManager)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        
        var cfg = configManager.Module("deploy");
        _url = (cfg.GetValueOrDefault("url", "http://argocd-server.argocd.svc.cluster.local") ?? "").TrimEnd('/') + "/api/v1";
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

    public async Task<JsonElement> ListApplicationsAsync()
    {
        var res = await RequestAsync(HttpMethod.Get, "applications");
        if (res.ValueKind == JsonValueKind.Object && res.TryGetProperty("items", out var items))
        {
            return items;
        }
        return res;
    }

    public async Task<JsonElement> SyncAsync(string applicationName)
    {
        return await RequestAsync(HttpMethod.Post, $"applications/{Uri.EscapeDataString(applicationName)}/sync");
    }

    public async Task<JsonElement> StatusAsync(string applicationName)
    {
        return await RequestAsync(HttpMethod.Get, $"applications/{Uri.EscapeDataString(applicationName)}");
    }

    public async Task<bool> HealthAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_url}/version");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}
