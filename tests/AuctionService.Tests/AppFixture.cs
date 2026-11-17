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

            // C2: CORS/SignalR origin'i (Program fail-fast eder).
            builder.UseSetting("ClientApp", "http://localhost:3000");

            builder.ConfigureServices(services =>
            {
                services.RunWolverineInSoloMode();
                services.DisableAllExternalWolverineTransports();
            });
        }, new JwtSecurityStub());

        // C2: Meilisearch:Url bu fixture'da yok -> Search modulu kaydedilmez.
        await AssertModulesAsync(Host, "auction", "bid", "notification");
    }

    public async Task DisposeAsync()
    {
        await Host.StopAsync();
        await Host.DisposeAsync();
        await _postgreSqlContainer.DisposeAsync();
    }

    /// <summary>/health/ready modul kumesini tam dogrular (sessizce eksik modul kalmasin).</summary>
    internal static async Task AssertModulesAsync(IAlbaHost host, params string[] expected)
    {
        var result = await host.Scenario(s =>
        {
            s.Get.Url("/health/ready");
            s.StatusCodeShouldBe(200);
        });

        var payload = await result.ReadAsJsonAsync<ReadyPayload>();
        Assert.NotNull(payload);

        Assert.Equal(
            expected.OrderBy(x => x, StringComparer.Ordinal).ToArray(),
            payload!.Modules.OrderBy(x => x, StringComparer.Ordinal).ToArray());
    }

    private static string WithDatabase(string connectionString, string database)
    {
        var parts = connectionString
            .Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Where(p => !p.StartsWith("Database=", StringComparison.OrdinalIgnoreCase));

        return string.Join(';', parts.Append($"Database={database}"));
    }

    public sealed record ReadyPayload(string Status, string[] Modules);
}

[CollectionDefinition("auction-service")]
public class AuctionServiceCollection : ICollectionFixture<AppFixture>;
