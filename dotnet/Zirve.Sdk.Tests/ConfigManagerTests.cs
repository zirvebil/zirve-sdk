using System;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Xunit;
using Zirve.Sdk.Config;

namespace Zirve.Sdk.Tests;

public class ConfigManagerTests
{
    [Fact]
    public void Get_ReturnsFromConfigurationFirst()
    {
        // Arrange
        var inMemorySettings = new Dictionary<string, string?>
        {
            {"Zirve:custom.setting", "config_value"},
        };

        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        var configManager = new ConfigManager(configuration);

        // Act
        var result = configManager.Get("custom.setting");

        // Assert
        Assert.Equal("config_value", result);
    }

    [Fact]
    public void Get_FallbackToEnvironmentVariables()
    {
        IConfiguration configuration = new ConfigurationBuilder().Build();
        var configManager = new ConfigManager(configuration);

        // Arrange - PG_DBNAME maps to db.dbname
        Environment.SetEnvironmentVariable("PG_DBNAME", "test_db");

        try
        {
            // Act
            var result = configManager.Get("db.dbname");

            // Assert
            Assert.Equal("test_db", result);
        }
        finally
        {
            // Cleanup
            Environment.SetEnvironmentVariable("PG_DBNAME", null);
        }
    }

    [Fact]
    public void Get_FallbackToInternalDefaults()
    {
        IConfiguration configuration = new ConfigurationBuilder().Build();
        var configManager = new ConfigManager(configuration);

        // Act
        var result = configManager.Get("db.host");

        // Assert
        Assert.Equal("postgresql.zirve-infra.svc.cluster.local", result);
    }

    [Fact]
    public void Module_ReturnsCorrectSubset()
    {
        // Arrange
        var inMemorySettings = new Dictionary<string, string?>
        {
            {"Zirve:db.custom", "custom_val"},
            {"Zirve:cache.host", "redis_local"}
        };

        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        var configManager = new ConfigManager(configuration);
        Environment.SetEnvironmentVariable("PG_DBNAME", "test_db");
        
        try
        {
            // Act
            var dbModule = configManager.Module("db");

            // Assert
            Assert.Equal("postgresql.zirve-infra.svc.cluster.local", dbModule["host"]); // default
            Assert.Equal("5432", dbModule["port"]); // default
            Assert.Equal("test_db", dbModule["dbname"]); // env override
            Assert.Equal("custom_val", dbModule["custom"]); // appsettings override
            
            Assert.False(dbModule.ContainsKey("Zirve:cache.host"));
        }
        finally
        {
            Environment.SetEnvironmentVariable("PG_DBNAME", null);
        }
    }
}
