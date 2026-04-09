using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Zirve.Sdk.Config;

namespace Zirve.Sdk.Dashboard;

public class DashboardManager
{
    private readonly HttpClient _httpClient;
    private readonly string _url;
    private readonly string _token;

    public DashboardManager(HttpClient httpClient, ConfigManager configManager)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        
        var cfg = configManager.Module("dashboard");
        _url = (cfg.GetValueOrDefault("url", "http://grafana.zirve-infra.svc.cluster.local") ?? "").TrimEnd('/') + "/api";
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

    public async Task<JsonElement> SearchDashboardsAsync(string query = "")
    {
        return await RequestAsync(HttpMethod.Get, $"search?query={Uri.EscapeDataString(query)}&type=dash-db");
    }

    public async Task<JsonElement> GetDashboardAsync(string uid)
    {
        var res = await RequestAsync(HttpMethod.Get, $"dashboards/uid/{Uri.EscapeDataString(uid)}");
        if (res.ValueKind == JsonValueKind.Object && res.TryGetProperty("dashboard", out var dashboard))
        {
            return dashboard;
        }
        return res;
    }

    public async Task<JsonElement> ImportDashboardAsync(object dashboardJson, int folderId = 0, bool overwrite = true)
    {
        return await RequestAsync(HttpMethod.Post, "dashboards/db", new
        {
            dashboard = dashboardJson,
            folderId,
            overwrite
        });
    }

    public async Task<JsonElement> ListDataSourcesAsync()
    {
        return await RequestAsync(HttpMethod.Get, "datasources");
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
