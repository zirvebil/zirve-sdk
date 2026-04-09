using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Zirve.Sdk.Config;

namespace Zirve.Sdk.Registry;

public class RegistryManager
{
    private readonly HttpClient _httpClient;
    private readonly string _url;
    private readonly string _authHeader;

    public RegistryManager(HttpClient httpClient, ConfigManager configManager)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        
        var cfg = configManager.Module("registry");
        _url = (cfg.GetValueOrDefault("url", "http://harbor-core.zirve-infra.svc.cluster.local") ?? "").TrimEnd('/') + "/api/v2.0";
        
        var user = cfg.GetValueOrDefault("username", "admin");
        var pass = cfg.GetValueOrDefault("password", "Harbor12345");
        
        var rawAuth = System.Text.Encoding.ASCII.GetBytes($"{user}:{pass}");
        _authHeader = "Basic " + Convert.ToBase64String(rawAuth);
    }

    private async Task<JsonElement> RequestAsync(HttpMethod method, string path, object? body = null)
    {
        var request = new HttpRequestMessage(method, $"{_url}/{path.TrimStart('/')}");
        request.Headers.Add("Authorization", _authHeader);
        
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

    public async Task<JsonElement> ListProjectsAsync()
    {
        return await RequestAsync(HttpMethod.Get, "projects");
    }

    public async Task<JsonElement> ListImagesAsync(string projectName)
    {
        return await RequestAsync(HttpMethod.Get, $"projects/{Uri.EscapeDataString(projectName)}/repositories");
    }

    public async Task<bool> ScanImageAsync(string projectName, string repositoryName, string reference)
    {
        try
        {
            var safeRepo = Uri.EscapeDataString(repositoryName);
            await RequestAsync(HttpMethod.Post, $"projects/{Uri.EscapeDataString(projectName)}/repositories/{safeRepo}/artifacts/{Uri.EscapeDataString(reference)}/scan");
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<JsonElement> ScanReportAsync(string projectName, string repositoryName, string reference)
    {
        var safeRepo = Uri.EscapeDataString(repositoryName);
        return await RequestAsync(HttpMethod.Get, $"projects/{Uri.EscapeDataString(projectName)}/repositories/{safeRepo}/artifacts/{Uri.EscapeDataString(reference)}/additions/vulnerabilities");
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
