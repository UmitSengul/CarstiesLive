using Alba;
using Alba.Security;
using Microsoft.AspNetCore.Hosting;
using Testcontainers.PostgreSql;
using Wolverine;

namespace AuctionService.Tests;

public class AppFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgreSqlContainer = 
        new PostgreSqlBuilder("postgres:18")
            .WithDatabase("auctions")
            .WithUsername("postgres")
            .WithPassword("postgrespw")
            .Build();

    public IAlbaHost Host { get; set; } = null!;
    
    public async Task InitializeAsync()
    {
        await _postgreSqlContainer.StartAsync();

        Host = await AlbaHost.For<Program>(builder =>
        {
            builder.UseEnvironment("Test");

            var auctionsDb = _postgreSqlContainer.GetConnectionString();

            // C1: ayni host'ta Bid.Module de var; Bid DbInitializer (DbUp) startup'ta
            // calisir ve ConnectionStrings:BidDbConnection yoksa firlatir. Ayni container,
            // ayri veritabani ("bids") — ayni DB olsaydi EF "Auctions" ile Dapper
            // "auctions" tablolari karisirdi.
            var bidsDb = WithDatabase(auctionsDb, "bids");

            builder.UseSetting("ConnectionStrings:DefaultConnection", auctionsDb);
            builder.UseSetting("ConnectionStrings:BidDbConnection", bidsDb);

            builder.ConfigureServices(services =>
            {
                services.RunWolverineInSoloMode();
                services.DisableAllExternalWolverineTransports();
            });
        }, new JwtSecurityStub());
    }

    private static string WithDatabase(string connectionString, string database)
    {
        var parts = connectionString
            .Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Where(p => !p.StartsWith("Database=", StringComparison.OrdinalIgnoreCase));

        return string.Join(';', parts.Append($"Database={database}"));
    }

    public async Task DisposeAsync()
    {
        await Host.StopAsync();
        await Host.DisposeAsync();
        await _postgreSqlContainer.DisposeAsync();
    }
}

[CollectionDefinition("auction-service")]
public class AuctionServiceCollection : ICollectionFixture<AppFixture>;