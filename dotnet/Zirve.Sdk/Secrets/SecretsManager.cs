using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;
using Zirve.Sdk.Config;
using System.Collections.Concurrent;

namespace Zirve.Sdk.Secrets;

public class SecretsManager
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly string _token;
    private readonly string _projectId;

    private readonly ConcurrentDictionary<string, (string Value, DateTime ExpiresAt)> _cache = new();
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    public SecretsManager(HttpClient httpClient, ConfigManager configManager)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        
        var cfg = configManager.Module("secrets");
        _baseUrl = (cfg.GetValueOrDefault("url", "http://infisical.zirve-infra.svc.cluster.local") ?? "").TrimEnd('/');
        _token = cfg.GetValueOrDefault("token", "") ?? "";
        _projectId = cfg.GetValueOrDefault("project_id", "") ?? "";
    }

    public async Task<string?> GetAsync(string secretName, string environment = "dev", string path = "/")
    {
        var cacheKey = $"{environment}:{path}:{secretName}";

        if (_cache.TryGetValue(cacheKey, out var cached) && cached.ExpiresAt > DateTime.UtcNow)
        {
            return cached.Value;
        }

        if (string.IsNullOrEmpty(_token) || string.IsNullOrEmpty(_projectId))
        {
            throw new InvalidOperationException("Infisical token and project ID must be configured.");
        }

        var uri = $"{_baseUrl}/api/v3/secrets/raw/{secretName}?workspaceId={_projectId}&environment={environment}&secretPath={Uri.EscapeDataString(path)}";
        var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _token);

        var response = await _httpClient.SendAsync(request);

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        using var jsonDoc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var value = jsonDoc.RootElement.GetProperty("secret").GetProperty("secretValue").GetString();

        if (value != null)
        {
            _cache[cacheKey] = (value, DateTime.UtcNow.Add(CacheTtl));
        }

        return value;
    }

    public async Task<Dictionary<string, string>> ListAsync(string environment = "dev", string path = "/")
    {
        var uri = $"{_baseUrl}/api/v3/secrets/raw?workspaceId={_projectId}&environment={environment}&secretPath={Uri.EscapeDataString(path)}";
        var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _token);

        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        using var jsonDoc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var secrets = jsonDoc.RootElement.GetProperty("secrets").EnumerateArray();

        var result = new Dictionary<string, string>();
        foreach (var secret in secrets)
        {
            var key = secret.GetProperty("secretKey").GetString();
            var val = secret.GetProperty("secretValue").GetString();
            if (key != null && val != null)
            {
                result[key] = val;
            }
        }

        return result;
    }

    public async Task<bool> HealthAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/api/v1/health");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}
