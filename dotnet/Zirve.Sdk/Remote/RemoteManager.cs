using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Zirve.Sdk.Config;

namespace Zirve.Sdk.Remote;

public class RemoteManager
{
    private readonly HttpClient _httpClient;
    private readonly ConfigManager _config;
    private readonly string _url;
    private string? _token;

    public RemoteManager(HttpClient httpClient, ConfigManager configManager)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _config = configManager ?? throw new ArgumentNullException(nameof(configManager));
        
        var cfg = _config.Module("remote");
        _url = (cfg.GetValueOrDefault("url", "http://guacamole.zirve-infra.svc.cluster.local:8080") ?? "").TrimEnd('/') + "/api";
    }

    private async Task<string> GetTokenAsync()
    {
        if (_token != null) return _token;

        var cfg = _config.Module("remote");
        var user = cfg.GetValueOrDefault("username", "guacadmin");
        var password = cfg.GetValueOrDefault("password", "guacadmin");

        var content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("username", user!),
            new KeyValuePair<string, string>("password", password!)
        });

        var response = await _httpClient.PostAsync($"{_url}/tokens", content);
        response.EnsureSuccessStatusCode();

        using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        _token = doc.RootElement.GetProperty("authToken").GetString();
        
        return _token ?? throw new Exception("Failed to get Guacamole Auth Token");
    }

    private async Task<JsonElement> RequestAsync(HttpMethod method, string path, object? body = null)
    {
        var token = await GetTokenAsync();
        var uriBuilder = new UriBuilder($"{_url}/{path.TrimStart('/')}");
        uriBuilder.Query = $"token={Uri.EscapeDataString(token)}";

        var request = new HttpRequestMessage(method, uriBuilder.ToString());

        if (body != null)
        {
            var json = body is string s ? s : JsonSerializer.Serialize(body);
            request.Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        }

        var response = await _httpClient.SendAsync(request);
        
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return default;
        }
        
        response.EnsureSuccessStatusCode();
        
        if (response.StatusCode == System.Net.HttpStatusCode.NoContent || response.Content.Headers.ContentLength == 0)
        {
            return default;
        }

        using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        return doc.RootElement.Clone();
    }

    public async Task<string> CreateConnectionAsync(string sourceId, string name, string protocol, Dictionary<string, string> parameters)
    {
        var payload = new
        {
            parentIdentifier = sourceId,
            name = name,
            protocol = protocol,
            parameters = parameters
        };

        var result = await RequestAsync(HttpMethod.Post, "session/data/postgresql/connections", payload);
        return result.GetProperty("identifier").GetString() ?? "";
    }

    public async Task<bool> HealthAsync()
    {
        try
        {
            // Pinging the token endpoint without creds is a good readiness check
            var response = await _httpClient.PostAsync($"{_url}/tokens", new StringContent(""));
            return response.StatusCode != System.Net.HttpStatusCode.ServiceUnavailable &&
                   response.StatusCode != System.Net.HttpStatusCode.NotFound;
        }
        catch
        {
            return false;
        }
    }
}
