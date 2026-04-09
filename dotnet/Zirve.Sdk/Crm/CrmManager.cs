using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Zirve.Sdk.Config;

namespace Zirve.Sdk.Crm;

public class CrmManager
{
    private readonly HttpClient _httpClient;
    private readonly ConfigManager _config;
    private readonly string _url;
    private readonly string _db;
    private int? _uid;
    private int _rpcId = 0;

    public CrmManager(HttpClient httpClient, ConfigManager configManager)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _config = configManager ?? throw new ArgumentNullException(nameof(configManager));
        
        var cfg = _config.Module("crm");
        _url = (cfg.GetValueOrDefault("url", "http://odoo.zirve-infra.svc.cluster.local:8069") ?? "").TrimEnd('/');
        _db = cfg.GetValueOrDefault("database", "zirve") ?? "zirve";
    }

    private async Task<JsonElement> RpcAsync(string method, object paramsBody)
    {
        _rpcId++;
        
        var payload = new
        {
            jsonrpc = "2.0",
            method = method,
            @params = paramsBody,
            id = _rpcId
        };

        var request = new HttpRequestMessage(HttpMethod.Post, $"{_url}/jsonrpc")
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), System.Text.Encoding.UTF8, "application/json")
        };

        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        
        if (doc.RootElement.TryGetProperty("error", out var err))
        {
            throw new Exception($"Odoo RPC Error: {err.GetRawText()}");
        }

        return doc.RootElement.GetProperty("result").Clone();
    }

    private async Task<int> AuthenticateAsync()
    {
        if (_uid.HasValue) return _uid.Value;

        var cfg = _config.Module("crm");
        var user = cfg.GetValueOrDefault("username", "admin");
        var password = cfg.GetValueOrDefault("password", "admin");

        var result = await RpcAsync("call", new
        {
            service = "common",
            method = "authenticate",
            args = new object[] { _db, user!, password!, new { } }
        });

        if (result.ValueKind == JsonValueKind.False || result.ValueKind == JsonValueKind.Null)
        {
            throw new Exception("Odoo Authentication failed.");
        }

        _uid = result.GetInt32();
        return _uid.Value;
    }

    private async Task<JsonElement> ExecuteKwAsync(string model, string method, object[] args, Dictionary<string, object>? kwargs = null)
    {
        var uid = await AuthenticateAsync();
        var password = _config.Module("crm").GetValueOrDefault("password", "admin");

        return await RpcAsync("call", new
        {
            service = "object",
            method = "execute_kw",
            args = new object[] { _db, uid, password!, model, method, args, kwargs ?? new Dictionary<string, object>() }
        });
    }

    public async Task<int> CreateContactAsync(Dictionary<string, object> contactData)
    {
        var result = await ExecuteKwAsync("res.partner", "create", new[] { contactData });
        return result.GetInt32();
    }

    public async Task<int> SyncCustomerAsync(string id, string name, string email, bool isCompany = true)
    {
        var searchResult = await ExecuteKwAsync("res.partner", "search", new object[]
        {
            new object[] { new object[] { "ref", "=", id } }
        });

        var partnerData = new Dictionary<string, object>
        {
            { "name", name },
            { "email", email },
            { "ref", id },
            { "is_company", isCompany }
        };

        if (searchResult.ValueKind == JsonValueKind.Array && searchResult.GetArrayLength() > 0)
        {
            var existingId = searchResult[0].GetInt32();
            await ExecuteKwAsync("res.partner", "write", new object[] { new[] { existingId }, partnerData });
            return existingId;
        }

        return await CreateContactAsync(partnerData);
    }

    public async Task<bool> HealthAsync()
    {
        try
        {
            var result = await RpcAsync("call", new
            {
                service = "common",
                method = "version",
                args = Array.Empty<object>()
            });
            return result.ValueKind != JsonValueKind.Null;
        }
        catch
        {
            return false;
        }
    }
}
