using System;
using System.Text.Json;
using System.Threading.Tasks;
using StackExchange.Redis;
using Zirve.Sdk.Config;

namespace Zirve.Sdk.Cache;

/// <summary>
/// Zirve Cache Manager — Redis.
/// Uses StackExchange.Redis multiplexer, JSON serialization, and distributes locks.
/// </summary>
public class CacheManager : IAsyncDisposable
{
    private readonly Lazy<Task<ConnectionMultiplexer>> _redisConn;
    private readonly string _prefix;

    public CacheManager(ConfigManager configManager)
    {
        var cfg = configManager.Module("cache");
        var host = cfg.GetValueOrDefault("host", "localhost");
        var port = cfg.GetValueOrDefault("port", "6379");
        var password = cfg.GetValueOrDefault("password", "");
        
        _prefix = cfg.GetValueOrDefault("prefix", "zirve:") ?? "zirve:";

        var options = new ConfigurationOptions
        {
            EndPoints = { $"{host}:{port}" },
            Password = password,
            AbortOnConnectFail = false,
            ConnectRetry = 3
        };

        // Lazy load redis to avoid unneeded initial overhead
        _redisConn = new Lazy<Task<ConnectionMultiplexer>>(() => ConnectionMultiplexer.ConnectAsync(options));
    }

    private async Task<IDatabase> GetDbAsync()
    {
        var multiplexer = await _redisConn.Value;
        return multiplexer.GetDatabase();
    }

    private RedisKey MakeKey(string key) => $"{_prefix}{key}";

    /// <summary>
    /// Check if key exists.
    /// </summary>
    public async Task<bool> HasAsync(string key)
    {
        var db = await GetDbAsync();
        return await db.KeyExistsAsync(MakeKey(key));
    }

    /// <summary>
    /// Retrieve data from cache and safely deserialize to Type.
    /// </summary>
    public async Task<T?> GetAsync<T>(string key)
    {
        var db = await GetDbAsync();
        var value = await db.StringGetAsync(MakeKey(key));
        
        if (!value.HasValue) return default;
        
        try
        {
            return JsonSerializer.Deserialize<T>(value!);
        }
        catch
        {
            if (typeof(T) == typeof(string))
            {
                return (T)(object)value.ToString()!;
            }
            return default;
        }
    }

    /// <summary>
    /// Serializes value and stores it into the cache.
    /// </summary>
    public async Task SetAsync<T>(string key, T value, TimeSpan? expiry = null)
    {
        var db = await GetDbAsync();
        var serialized = value is string str ? str : JsonSerializer.Serialize(value);
        await db.StringSetAsync(MakeKey(key), serialized, expiry ?? TimeSpan.FromHours(1));
    }

    /// <summary>
    /// Remove an item from the cache.
    /// </summary>
    public async Task ForgetAsync(string key)
    {
        var db = await GetDbAsync();
        await db.KeyDeleteAsync(MakeKey(key));
    }

    /// <summary>
    /// Retrieve a value, if miss, run callback, lock, and store callback's return.
    /// </summary>
    public async Task<T> RememberAsync<T>(string key, TimeSpan expiry, Func<Task<T>> callback)
    {
        var existing = await GetAsync<T>(key);
        if (existing != null)
        {
            return existing;
        }

        var result = await callback();
        if (result != null)
        {
            await SetAsync(key, result, expiry);
        }

        return result;
    }

    /// <summary>
    /// Execute a distributed lock using Redis sets. Returns true if acquired.
    /// </summary>
    public async Task<bool> LockAsync(string key, string ownerId, TimeSpan expiry)
    {
        var db = await GetDbAsync();
        return await db.StringSetAsync(MakeKey($"lock:{key}"), ownerId, expiry, When.NotExists);
    }

    /// <summary>
    /// Release the active distributed lock, comparing with the expected owner identifier.
    /// </summary>
    public async Task<bool> UnlockAsync(string key, string ownerId)
    {
        var db = await GetDbAsync();
        var realKey = MakeKey($"lock:{key}");

        // Lua script to safely unlock ONLY if owner id matches.
        var script = @"
            if redis.call('get', KEYS[1]) == ARGV[1] then
                return redis.call('del', KEYS[1])
            else
                return 0
            end";

        var result = await db.ScriptEvaluateAsync(script, new RedisKey[] { realKey }, new RedisValue[] { ownerId });
        return (int)result == 1;
    }

    public async Task<bool> HealthAsync()
    {
        try
        {
            var db = await GetDbAsync();
            var ping = await db.PingAsync();
            return ping.TotalMilliseconds > 0;
        }
        catch
        {
            return false;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_redisConn.IsValueCreated)
        {
            var conn = await _redisConn.Value;
            await conn.DisposeAsync();
        }
    }
}
