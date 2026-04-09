using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Zirve.Sdk.Config;

namespace Zirve.Sdk.Analytics;

public class AnalyticsManager
{
    private readonly HttpClient _httpClient;
    private readonly string _url;
    private readonly string _username;
    private readonly string _password;
    private readonly string _database;

    public AnalyticsManager(HttpClient httpClient, ConfigManager configManager)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        
        var cfg = configManager.Module("analytics");
        var protocol = cfg.GetValueOrDefault("scheme", "http");
        var host = cfg.GetValueOrDefault("host", "clickhouse.zirve-infra.svc.cluster.local");
        var port = cfg.GetValueOrDefault("port", "8123");
        
        _url = $"{protocol}://{host}:{port}/";
        _username = cfg.GetValueOrDefault("username", "default") ?? "default";
        _password = cfg.GetValueOrDefault("password", "") ?? "";
        _database = cfg.GetValueOrDefault("database", "default") ?? "default";
    }

    private void AddHeaders(HttpRequestMessage request)
    {
        if (!string.IsNullOrEmpty(_username)) request.Headers.Add("X-ClickHouse-User", _username);
        if (!string.IsNullOrEmpty(_password)) request.Headers.Add("X-ClickHouse-Key", _password);
        if (!string.IsNullOrEmpty(_database)) request.Headers.Add("X-ClickHouse-Database", _database);
    }

    public async Task<JsonElement> QueryAsync(string sql, Dictionary<string, object>? paramsMap = null)
    {
        var finalQuery = sql;
        if (!finalQuery.ToUpper().Contains("FORMAT JSON"))
        {
            finalQuery += " FORMAT JSON";
        }

        if (paramsMap != null)
        {
            foreach (var kvp in paramsMap)
            {
                var placeholder = "{" + kvp.Key + "}";
                // Minimalistic safe value replacement. ClickHouse HTTP API handles format params natively,
                // but for simplicity mirroring TS behavior here.
                var safeValue = kvp.Value is string s ? $"'{s.Replace("'", "''")}'" : kvp.Value.ToString();
                finalQuery = finalQuery.Replace(placeholder, safeValue);
            }
        }

        var request = new HttpRequestMessage(HttpMethod.Post, _url)
        {
            Content = new StringContent(finalQuery, System.Text.Encoding.UTF8, "text/plain")
        };
        
        AddHeaders(request);

        var response = await _httpClient.SendAsync(request);
        
        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync();
            throw new Exception($"ClickHouse query failed: {response.StatusCode} - {err}");
        }

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("data").Clone(); // Return data array
    }

    public async Task<bool> ExecuteAsync(string sql)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, _url)
        {
            Content = new StringContent(sql, System.Text.Encoding.UTF8, "text/plain")
        };
        
        AddHeaders(request);
        
        var response = await _httpClient.SendAsync(request);
        
        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync();
            throw new Exception($"ClickHouse execute failed: {response.StatusCode} - {err}");
        }

        return true;
    }

    public async Task<bool> InsertAsync(string table, Dictionary<string, object> data)
    {
        var keys = string.Join(", ", data.Keys);
        
        var valuesList = new List<string>();
        foreach (var val in data.Values)
        {
            if (val is string s)
            {
                valuesList.Add($"'{s.Replace("'", "''")}'");
            }
            else if (val is DateTime dt)
            {
                valuesList.Add($"'{dt:yyyy-MM-dd HH:mm:ss}'");
            }
            else
            {
                valuesList.Add(val?.ToString() ?? "NULL");
            }
        }
        
        var sql = $"INSERT INTO {table} ({keys}) VALUES ({string.Join(", ", valuesList)})";
        return await ExecuteAsync(sql);
    }

    public async Task<bool> HealthAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_url}ping");
            var content = await response.Content.ReadAsStringAsync();
            return content.Trim() == "Ok.";
        }
        catch
        {
            return false;
        }
    }
}
