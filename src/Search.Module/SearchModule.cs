using Carsties.Search.Data;
using Carsties.Search.Endpoints;
using Mapster;
using Meilisearch;

namespace Carsties.Search;

/// <summary>
/// Search modulu (eski SearchService). Meilisearch baglantisi ve iki GET endpoint'i.
/// Auction verisi artik HTTP (AuctionSvcUrl) yerine surec ici <see cref="Contracts.IAuctionLookup"/>.
/// </summary>
public static class SearchModule
{
    public static bool AddSearchModule(
        this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        var url = configuration["Meilisearch:Url"];

        if (string.IsNullOrWhiteSpace(url))
        {
            if (!environment.IsEnvironment("Test"))
                throw new InvalidOperationException("Meilisearch:Url bos: Search modulu icin zorunlu");

            // Test: Meilisearch gerektirmeyen fixture'lar (or. AuctionService.Tests) icin
            // modul kaydedilmez; SearchService.Tests Meili container'i verdigi icin kaydedilir.
            return false;
        }

        services.AddSingleton(new MeilisearchClient(url, configuration["Meilisearch:ApiKey"]));
        TypeAdapterConfig.GlobalSettings.Scan(typeof(SearchModule).Assembly);

        return true;
    }

    public static WebApplication MapSearchModule(this WebApplication app)
    {
        if (string.IsNullOrWhiteSpace(app.Configuration["Meilisearch:Url"]))
        {
            app.Logger.LogWarning("Search modulu devre disi: Meilisearch:Url yok");
            return app;
        }

        app.MapGet("/api/search", SearchEndpoints.GetSearchResults);
        app.MapGet("/api/search/{id}", SearchEndpoints.GetAuctionById);

        try
        {
            // Eski Program.cs ile ayni: ilk istekten once indeks ayarlari (await edilirdi).
            DbInitializer.ConfigureIndex(app).GetAwaiter().GetResult();
        }
        catch (Exception e)
        {
            app.Logger.LogError(e, "Search index init hatasi: {Message}", e.Message);
        }

        if (!app.Environment.IsEnvironment("Test"))
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await DbInitializer.FetchMissingAuctions(app);
                }
                catch (Exception e)
                {
                    app.Logger.LogError(e, "Search backfill hatasi: {Message}", e.Message);
                }
            });
        }

        return app;
    }
}
