using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Zirve.Sdk.Config;

namespace Zirve.Sdk.Ingress;

public class IngressManager
{
    private readonly HttpClient _httpClient;
    private readonly string _url;

    public IngressManager(HttpClient httpClient, ConfigManager configManager)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        
        var cfg = configManager.Module("ingress");
        _url = (cfg.GetValueOrDefault("url", "http://traefik.kube-system.svc.cluster.local:8080") ?? "").TrimEnd('/');
    }

    public async Task<JsonElement> GetRoutesAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_url}/api/http/routers");
            if (response.IsSuccessStatusCode)
            {
                using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
                return doc.RootElement.Clone();
            }
        }
        catch { }
        return default;
    }

    public async Task<JsonElement> GetMiddlewaresAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_url}/api/http/middlewares");
            if (response.IsSuccessStatusCode)
            {
                using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
                return doc.RootElement.Clone();
            }
        }
        catch { }
        return default;
    }

    public async Task<bool> HealthAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_url}/ping");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}
