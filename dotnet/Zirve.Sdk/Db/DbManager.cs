using System;
using System.Data;
using System.Threading.Tasks;
using Npgsql;
using Zirve.Sdk.Config;

namespace Zirve.Sdk.Db;

/// <summary>
/// Zirve Db Manager — PostgreSQL Integration.
/// Utilizes Npgsql for pooling, tenant isolation, and connection management.
/// </summary>
public class DbManager : IAsyncDisposable
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly string _connectionString;

    public DbManager(ConfigManager configManager)
    {
        var cfg = configManager.Module("db");
        var host = cfg.GetValueOrDefault("host", "localhost");
        var port = cfg.GetValueOrDefault("port", "5432");
        var dbname = cfg.GetValueOrDefault("dbname", "zirve");
        var user = cfg.GetValueOrDefault("user", "postgres");
        var password = cfg.GetValueOrDefault("password", "");

        // Build ADO.NET Connection String with pooling logic suitable for high concurrency
        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = host,
            Port = int.TryParse(port, out var p) ? p : 5432,
            Database = dbname,
            Username = user,
            Password = password,
            Pooling = true,
            MinPoolSize = 1,
            MaxPoolSize = 100,
            Timeout = 3
        };

        _connectionString = builder.ToString();
        _dataSource = NpgsqlDataSource.Create(_connectionString);
    }

    /// <summary>
    /// Executes a single SQL statement that returns no data (e.g. INSERT, UPDATE).
    /// </summary>
    public async Task<int> ExecuteAsync(string sql, params NpgsqlParameter[] parameters)
    {
        await using var cmd = _dataSource.CreateCommand(sql);
        cmd.Parameters.AddRange(parameters);
        return await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Executes a SQL query and returns the first column of the first row.
    /// </summary>
    public async Task<T?> ScalarAsync<T>(string sql, params NpgsqlParameter[] parameters)
    {
        await using var cmd = _dataSource.CreateCommand(sql);
        cmd.Parameters.AddRange(parameters);
        var res = await cmd.ExecuteScalarAsync();
        if (res == null || res == DBNull.Value) return default;
        return (T)res;
    }

    /// <summary>
    /// Gets a fully managed DbConnection intended for passing to an ORM like Dapper.
    /// Make sure to configure the schema appropriately when multi-tenant.
    /// </summary>
    public NpgsqlConnection GetConnection()
    {
        return _dataSource.CreateConnection();
    }

    /// <summary>
    /// Runs operations inside a database transaction safely.
    /// Auto commits or rollbacks on Exception.
    /// </summary>
    public async Task<T> TransactionAsync<T>(Func<NpgsqlTransaction, NpgsqlConnection, Task<T>> callback)
    {
        await using var connection = await _dataSource.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        
        try
        {
            var result = await callback(transaction, connection);
            await transaction.CommitAsync();
            return result;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    /// <summary>
    /// Changes the PostgreSQL schema context dynamically. Useful for strictly isolated tenants.
    /// </summary>
    public async Task SetTenantAsync(NpgsqlConnection connection, string tenantId)
    {
        var safeId = string.Concat(tenantId.Split(System.IO.Path.GetInvalidFileNameChars()));
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = $"SET search_path TO tenant_{safeId.Replace("'", "")}, public";
        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Basic health check simply executing a SELECT 1 query to database.
    /// </summary>
    public async Task<bool> HealthAsync()
    {
        try
        {
            await using var cmd = _dataSource.CreateCommand("SELECT 1");
            await cmd.ExecuteScalarAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_dataSource != null)
        {
            await _dataSource.DisposeAsync();
        }
    }
}
