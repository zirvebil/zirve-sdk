using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;
using Zirve.Sdk.Config;

namespace Zirve.Sdk.Auth;

/// <summary>
/// Zirve Auth Manager — Keycloak Integration.
/// Validates tokens, checks roles, and retrieves user info via OIDC endpoints.
/// </summary>
public class AuthManager
{
    private readonly HttpClient _httpClient;
    private readonly ConfigManager _config;
    private readonly string _baseUrl;
    private readonly string _realm;

    public AuthManager(HttpClient httpClient, ConfigManager configManager)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _config = configManager ?? throw new ArgumentNullException(nameof(configManager));
        
        var cfg = _config.Module("auth");
        _baseUrl = (cfg.GetValueOrDefault("url", "http://keycloak.zirve-infra.svc.cluster.local") ?? "").TrimEnd('/');
        _realm = cfg.GetValueOrDefault("realm", "zirve") ?? "zirve";
    }

    /// <summary>
    /// Introspect an OAuth2 / OIDC access token.
    /// </summary>
    public async Task<JsonElement> VerifyTokenAsync(string token)
    {
        var cfg = _config.Module("auth");
        var clientId = cfg.GetValueOrDefault("client_id", "zirve-backend");
        var clientSecret = cfg.GetValueOrDefault("client_secret", "");

        var content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("token", token),
            new KeyValuePair<string, string>("client_id", clientId!),
            new KeyValuePair<string, string>("client_secret", clientSecret!)
        });

        var response = await _httpClient.PostAsync($"{_baseUrl}/realms/{_realm}/protocol/openid-connect/token/introspect", content);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<JsonElement>(json);
    }

    /// <summary>
    /// Check if the introspected token is active and contains the requested realm role.
    /// </summary>
    public async Task<bool> HasRoleAsync(string token, string roleName)
    {
        var claims = await VerifyTokenAsync(token);
        
        if (!claims.TryGetProperty("active", out var active) || !active.GetBoolean())
        {
            return false;
        }

        if (claims.TryGetProperty("realm_access", out var realmAccess) && 
            realmAccess.TryGetProperty("roles", out var roles) &&
            roles.ValueKind == JsonValueKind.Array)
        {
            foreach (var role in roles.EnumerateArray())
            {
                if (role.GetString() == roleName) return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Get OIDC UserInfo.
    /// </summary>
    public async Task<JsonElement> GetUserAsync(string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"{_baseUrl}/realms/{_realm}/protocol/openid-connect/userinfo");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<JsonElement>(json);
    }

    public async Task<bool> HealthAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/realms/{_realm}/.well-known/openid-configuration");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}
