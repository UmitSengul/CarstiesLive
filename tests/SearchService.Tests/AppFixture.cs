using Alba;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.AspNetCore.Hosting;
using Testcontainers.PostgreSql;
using Wolverine;

namespace SearchService.Tests;

public class AppFixture : IAsyncLifetime
{
    private readonly IContainer _meilisearchContainer
        = new ContainerBuilder("getmeili/meilisearch:v1.53.1")
            .WithPortBinding(7700, true)
            .WithEnvironment("MEILI_MASTER_KEY", "masterkey")
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilHttpRequestIsSucceeded(request =>
                    request.ForPath("/health").ForPort(7700)))
            .Build();

    // C2: host artik tum modulleri acar; Auction/Bid startup'i (EF Migrate + DbUp + backfill)
    // icin Postgres gerekir.
    private readonly PostgreSqlContainer _postgreSqlContainer =
        new PostgreSqlBuilder("postgres:18")
            .WithDatabase("auctions")
            .WithUsername("postgres")
            .WithPassword("postgrespw")
            .Build();

    public IAlbaHost Host { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _meilisearchContainer.StartAsync();
        await _postgreSqlContainer.StartAsync();

        var meilisearchUrl =
            $"http://{_meilisearchContainer.Hostname}:{_meilisearchContainer.GetMappedPublicPort(7700)}";

        Host = await AlbaHost.For<Program>(builder =>
        {
            builder.UseEnvironment("Test");
            builder.UseSetting("Meilisearch:Url", meilisearchUrl);
            builder.UseSetting("Meilisearch:ApiKey", "masterkey");

            var auctionsDb = _postgreSqlContainer.GetConnectionString();
            builder.UseSetting("ConnectionStrings:DefaultConnection", auctionsDb);
            builder.UseSetting("ConnectionStrings:BidDbConnection", WithDatabase(auctionsDb, "bids"));

            // C2: CORS/SignalR origin'i (Program fail-fast eder).
            builder.UseSetting("ClientApp", "http://localhost:3000");

            builder.ConfigureServices(services =>
            {
                services.RunWolverineInSoloMode();
                services.DisableAllExternalWolverineTransports();
            });
        });

        // C2: Meilisearch:Url verildigi icin Search modulu de kayitli olmali.
        var result = await Host.Scenario(s =>
        {
            s.Get.Url("/health/ready");
            s.StatusCodeShouldBe(200);
        });

        var payload = await result.ReadAsJsonAsync<ReadyPayload>();
        Assert.NotNull(payload);
        Assert.Equal(
            new[] { "auction", "bid", "notification", "search" },
            payload!.Modules.OrderBy(x => x, StringComparer.Ordinal).ToArray());
    }

    public async Task DisposeAsync()
    {
        await Host.StopAsync();
        await Host.DisposeAsync();
        await _postgreSqlContainer.DisposeAsync();
        await _meilisearchContainer.DisposeAsync();
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

[CollectionDefinition("search-service")]
public class SearchServiceCollection : ICollectionFixture<AppFixture>;
