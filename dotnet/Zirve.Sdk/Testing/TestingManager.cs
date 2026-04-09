using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Zirve.Sdk.Config;

namespace Zirve.Sdk.Testing;

public class TestingManager
{
    private readonly HttpClient _httpClient;
    private readonly string _url;

    public TestingManager(HttpClient httpClient, ConfigManager configManager)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        
        var cfg = configManager.Module("testing");
        _url = (cfg.GetValueOrDefault("url", "http://keploy.zirve-infra.svc.cluster.local:6789") ?? "").TrimEnd('/') + "/api";
    }

    private async Task<JsonElement> RequestAsync(HttpMethod method, string path, object? body = null)
    {
        var request = new HttpRequestMessage(method, $"{_url}/{path.TrimStart('/')}");
        
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

    public async Task<JsonElement> ListTestSetsAsync(string app)
    {
        return await RequestAsync(HttpMethod.Get, $"test-sets?app={Uri.EscapeDataString(app)}");
    }

    public async Task<JsonElement> GetTestRunAsync(string id)
    {
        return await RequestAsync(HttpMethod.Get, $"test-run/{Uri.EscapeDataString(id)}");
    }

    public async Task<bool> HealthAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_url}/healthz");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}
