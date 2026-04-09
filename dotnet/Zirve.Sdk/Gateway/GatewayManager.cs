using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Zirve.Sdk.Config;

namespace Zirve.Sdk.Gateway;

public class GatewayManager
{
    private readonly HttpClient _httpClient;
    private readonly string _url;

    public GatewayManager(HttpClient httpClient, ConfigManager configManager)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        
        var cfg = configManager.Module("gateway");
        _url = (cfg.GetValueOrDefault("url", "http://kong-kong-admin.zirve-infra.svc.cluster.local:8001") ?? "").TrimEnd('/');
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

    public async Task<JsonElement> AddServiceAsync(string name, string protocol, string host, int port, string path = "/")
    {
        return await RequestAsync(HttpMethod.Post, "services", new
        {
            name, protocol, host, port, path
        });
    }

    public async Task<JsonElement> AddRouteAsync(string serviceIdOrName, string name, string[] paths)
    {
        return await RequestAsync(HttpMethod.Post, $"services/{Uri.EscapeDataString(serviceIdOrName)}/routes", new
        {
            name, paths, strip_path = true
        });
    }

    public async Task<JsonElement> AddPluginAsync(string serviceIdOrName, string name, object config)
    {
        return await RequestAsync(HttpMethod.Post, $"services/{Uri.EscapeDataString(serviceIdOrName)}/plugins", new
        {
            name, config
        });
    }

    public async Task<bool> HealthAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_url}/status");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}
