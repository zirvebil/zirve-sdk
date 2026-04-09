using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Zirve.Sdk.Config;

namespace Zirve.Sdk.Queue;

public class QueueManager
{
    private readonly HttpClient _httpClient;
    private readonly string _apiBase;
    private readonly string _authHeader;

    public QueueManager(HttpClient httpClient, ConfigManager configManager)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        
        var cfg = configManager.Module("queue");
        var host = cfg.GetValueOrDefault("host", "rabbitmq.zirve-infra.svc.cluster.local");
        var apiPort = cfg.GetValueOrDefault("api_port", "15672");
        
        _apiBase = $"http://{host}:{apiPort}/api";

        var user = cfg.GetValueOrDefault("user", "guest");
        var pass = cfg.GetValueOrDefault("password", "guest");
        
        var rawAuth = System.Text.Encoding.ASCII.GetBytes($"{user}:{pass}");
        _authHeader = "Basic " + Convert.ToBase64String(rawAuth);
    }

    public async Task<bool> CreateVhostAsync(string vhost)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, $"{_apiBase}/vhosts/{Uri.EscapeDataString(vhost)}");
        request.Headers.Add("Authorization", _authHeader);
        
        var response = await _httpClient.SendAsync(request);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> PublishAsync(string vhost, string exchange, string routingKey, object payload)
    {
        var safeVhost = Uri.EscapeDataString(vhost);
        var safeExchange = Uri.EscapeDataString(string.IsNullOrEmpty(exchange) ? "amq.default" : exchange);
        
        var request = new HttpRequestMessage(HttpMethod.Post, $"{_apiBase}/exchanges/{safeVhost}/{safeExchange}/publish");
        request.Headers.Add("Authorization", _authHeader);
        
        var isString = payload is string;
        var reqPayload = new
        {
            properties = new { },
            routing_key = routingKey,
            payload = isString ? payload.ToString() : JsonSerializer.Serialize(payload),
            payload_encoding = "string"
        };
        
        request.Content = new StringContent(JsonSerializer.Serialize(reqPayload), System.Text.Encoding.UTF8, "application/json");

        var response = await _httpClient.SendAsync(request);
        if (!response.IsSuccessStatusCode) return false;

        using var jsonDoc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        return jsonDoc.RootElement.GetProperty("routed").GetBoolean();
    }

    public async Task<bool> HealthAsync()
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"{_apiBase}/overview");
            request.Headers.Add("Authorization", _authHeader);
            
            var response = await _httpClient.SendAsync(request);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}
