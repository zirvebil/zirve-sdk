using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Zirve.Sdk.Config;

namespace Zirve.Sdk.Billing;

public class BillingManager
{
    private readonly HttpClient _httpClient;
    private readonly string _url;

    public BillingManager(HttpClient httpClient, ConfigManager configManager)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        
        var cfg = configManager.Module("billing");
        _url = (cfg.GetValueOrDefault("url", "http://lago-api.zirve-infra.svc.cluster.local") ?? "").TrimEnd('/') + "/api/v1";
        
        var key = cfg.GetValueOrDefault("api_key", "");
        if (!string.IsNullOrEmpty(key))
        {
            _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", key);
        }
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
        
        if (!response.IsSuccessStatusCode && response.StatusCode != System.Net.HttpStatusCode.NotFound)
        {
            var err = await response.Content.ReadAsStringAsync();
            throw new Exception($"Lago Billing API Error [{response.StatusCode}]: {err}");
        }

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return default;

        var jsonStr = await response.Content.ReadAsStringAsync();
        return string.IsNullOrEmpty(jsonStr) ? default : JsonSerializer.Deserialize<JsonElement>(jsonStr);
    }

    public async Task<JsonElement> CreateCustomerAsync(object customer)
    {
        return await RequestAsync(HttpMethod.Post, "customers", new { customer });
    }

    public async Task<JsonElement> CreateSubscriptionAsync(object subscription)
    {
        return await RequestAsync(HttpMethod.Post, "subscriptions", new { subscription });
    }

    public async Task<JsonElement> AddEventAsync(object evt)
    {
        return await RequestAsync(HttpMethod.Post, "events", new { @event = evt });
    }

    public async Task<JsonElement> GetInvoiceAsync(string id)
    {
        return await RequestAsync(HttpMethod.Get, $"invoices/{Uri.EscapeDataString(id)}");
    }

    public async Task<bool> HealthAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_url}/organizations");
            // If we get 401 Unauthorized it means API is up but token is bad, which still passes health check for reachability
            return response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.Unauthorized;
        }
        catch
        {
            return false;
        }
    }
}
