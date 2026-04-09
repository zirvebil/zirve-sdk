using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Zirve.Sdk.Extensions;
using Zirve.Sdk.Config;
using Zirve.Sdk.Db;
using Zirve.Sdk.Log;
using Zirve.Sdk.Registry;

namespace Zirve.Sdk.Tests;

public class ZirveClientTests
{
    [Fact]
    public void AddZirve_RegistersAllServicesAppropriately()
    {
        // Arrange
        var services = new ServiceCollection();
        IConfiguration configuration = new ConfigurationBuilder().Build();
        
        // Zirve Config reads from generic IConfiguration
        services.AddSingleton(configuration);
        
        // Add Zirve SDK
        services.AddZirve();

        var serviceProvider = services.BuildServiceProvider();

        // Act & Assert
        
        // 1. Assert Singletons
        var config1 = serviceProvider.GetRequiredService<ConfigManager>();
        var config2 = serviceProvider.GetRequiredService<ConfigManager>();
        Assert.Same(config1, config2);

        var db1 = serviceProvider.GetRequiredService<DbManager>();
        var db2 = serviceProvider.GetRequiredService<DbManager>();
        Assert.Same(db1, db2);

        // 2. Assert HttpClients (Transient/Scoped handlers with typed factory)
        var log = serviceProvider.GetRequiredService<LogManager>();
        Assert.NotNull(log);

        var registry = serviceProvider.GetRequiredService<RegistryManager>();
        Assert.NotNull(registry);

        // 3. Assert Facade
        using var scope = serviceProvider.CreateScope();
        var client = scope.ServiceProvider.GetRequiredService<ZirveClient>();
        Assert.NotNull(client);
        Assert.NotNull(client.Config);
        Assert.Same(config1, client.Config);
        
        // Modules reachable
        Assert.NotNull(client.Db);
        Assert.NotNull(client.Registry);
        Assert.NotNull(client.Deploy);
    }
}
