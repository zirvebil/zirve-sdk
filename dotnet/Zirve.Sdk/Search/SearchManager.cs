using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Zirve.Sdk.Config;

namespace Zirve.Sdk.Search;

public class SearchManager
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly string? _authHeader;

    public SearchManager(HttpClient httpClient, ConfigManager configManager)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        
        var cfg = configManager.Module("search");
        var protocol = cfg.GetValueOrDefault("scheme", "http");
        var host = cfg.GetValueOrDefault("host", "elasticsearch-master.zirve-infra.svc.cluster.local");
        var port = cfg.GetValueOrDefault("port", "9200");
        
        _baseUrl = $"{protocol}://{host}:{port}";

        var user = cfg.GetValueOrDefault("username", "");
        var pass = cfg.GetValueOrDefault("password", "");
        
        if (!string.IsNullOrEmpty(user) && !string.IsNullOrEmpty(pass))
        {
            _authHeader = "Basic " + Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes($"{user}:{pass}"));
        }
    }

    private async Task<JsonElement> RequestAsync(HttpMethod method, string path, object? body = null, bool isNdJson = false)
    {
        var request = new HttpRequestMessage(method, $"{_baseUrl}/{path.TrimStart('/')}");
        
        if (_authHeader != null)
        {
            request.Headers.Add("Authorization", _authHeader);
        }

        if (body != null)
        {
            if (isNdJson && body is string ndjson)
            {
                request.Content = new StringContent(ndjson, System.Text.Encoding.UTF8, "application/x-ndjson");
            }
            else
            {
                var json = body is string s ? s : JsonSerializer.Serialize(body);
                request.Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            }
        }

        var response = await _httpClient.SendAsync(request);
        
        if (!response.IsSuccessStatusCode && response.StatusCode != System.Net.HttpStatusCode.NotFound)
        {
            var err = await response.Content.ReadAsStringAsync();
            throw new Exception($"Elasticsearch Request Failed: {response.StatusCode} - {err}");
        }

        var responseStr = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<JsonElement>(responseStr);
    }

    public async Task<JsonElement> IndexAsync(string index, string? id, object document)
    {
        var path = id != null ? $"{index}/_doc/{Uri.EscapeDataString(id)}" : $"{index}/_doc";
        var method = id != null ? HttpMethod.Put : HttpMethod.Post;
        return await RequestAsync(method, path, document);
    }

    public async Task<JsonElement?> GetAsync(string index, string id)
    {
        var result = await RequestAsync(HttpMethod.Get, $"{index}/_doc/{Uri.EscapeDataString(id)}");
        
        if (result.TryGetProperty("found", out var found) && found.GetBoolean())
        {
            return result.GetProperty("_source");
        }
        
        return null;
    }

    public async Task<bool> DeleteAsync(string index, string id)
    {
        try
        {
            var result = await RequestAsync(HttpMethod.Delete, $"{index}/_doc/{Uri.EscapeDataString(id)}");
            return result.TryGetProperty("result", out var res) && res.GetString() == "deleted";
        }
        catch
        {
            return false;
        }
    }

    public async Task<List<JsonElement>> SearchAsync(string index, object queryBody)
    {
        var response = await RequestAsync(HttpMethod.Post, $"{index}/_search", queryBody);
        
        var results = new List<JsonElement>();
        if (response.TryGetProperty("hits", out var hitsObj) && hitsObj.TryGetProperty("hits", out var hitsArray))
        {
            // Elasticsearch hits is an array, map `_source` but add `_id` into the element context if using generic JSON mapping.
            // Simplified return array of source elements.
            foreach (var hit in hitsArray.EnumerateArray())
            {
                if (hit.TryGetProperty("_source", out var source))
                {
                    results.Add(source);
                }
            }
        }

        return results;
    }

    public async Task<bool> HealthAsync()
    {
        try
        {
            var result = await RequestAsync(HttpMethod.Get, "_cluster/health");
            if (result.TryGetProperty("status", out var status))
            {
                var s = status.GetString();
                return s == "green" || s == "yellow";
            }
            return false;
        }
        catch
        {
            return false;
        }
    }
}
