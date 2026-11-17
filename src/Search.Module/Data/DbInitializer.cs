using Carsties.Search.Models;
using Contracts;
using Mapster;
using Meilisearch;

namespace Carsties.Search.Data;

public class DbInitializer
{
    private const string IndexUid = "items";

    public static async Task ConfigureIndex(WebApplication app)
    {
        var client = app.Services.GetRequiredService<MeilisearchClient>();
        var index = client.Index(IndexUid);
        
        var settingsTask = await index.UpdateSettingsAsync(new Settings
        {
            SearchableAttributes = ["make", "model", "description"],
            FilterableAttributes = ["seller", "winner", "status", "auctionEnd"],
            SortableAttributes = ["auctionEnd", "currentHighBid", "createdAt", "updatedAt", "make", "model"]
        });
        await client.WaitForTaskAsync(settingsTask.TaskUid);
    }

    /// <summary>
    /// Indeks backfill'i. C2: AuctionSvcUrl'e HTTP yerine surec ici IAuctionLookup
    /// (eski AuctionSvcHttpClient kaldirildi); son guncellenen kayittan sonrasini alir.
    /// </summary>
    public static async Task FetchMissingAuctions(WebApplication app)
    {
        var client = app.Services.GetRequiredService<MeilisearchClient>();
        var index = client.Index(IndexUid);

        var search = await index.SearchAsync<Item>(string.Empty, new SearchQuery
        {
            Sort = ["updatedAt:desc"],
            Limit = 1,
        });

        DateTimeOffset? lastUpdated = search.Hits.FirstOrDefault() is { } newest
            ? new DateTimeOffset(DateTime.SpecifyKind(newest.UpdatedAt, DateTimeKind.Utc))
            : null;

        using var scope = app.Services.CreateScope();
        var lookup = scope.ServiceProvider.GetRequiredService<IAuctionLookup>();

        var items = (await lookup.GetUpdatedSinceAsync(lastUpdated)).Adapt<List<Item>>();

        if (items.Count == 0)
        {
            app.Logger.LogInformation("Search index AuctionService ile guncel");
            return;
        }

        var addTask = await index.AddDocumentsAsync(items, primaryKey: "id");
        await client.WaitForTaskAsync(addTask.TaskUid);

        app.Logger.LogInformation("Search index {Count} auction ile dolduruldu", items.Count);
    }
}