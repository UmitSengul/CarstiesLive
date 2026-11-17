using Meilisearch;
using Microsoft.Extensions.DependencyInjection;
using Carsties.Search.Models;

namespace SearchService.Tests;

/// <summary>
/// C2: /search -> /api/search rewrite paritesi (web-app /search cagirir).
/// </summary>
[Collection("search-service")]
public class RewriteSearchTests(AppFixture fixture) : IAsyncLifetime
{
    private readonly Item _item = TestData.CreateItem(x =>
    {
        x.Make = "Zeta";
        x.Model = "Delta";
    });

    public async Task InitializeAsync()
    {
        var client = fixture.Host.Services.GetRequiredService<MeilisearchClient>();

        var addTask = await client.Index("items")
            .AddDocumentsAsync([_item], primaryKey: "id");

        await client.WaitForTaskAsync(addTask.TaskUid);
    }

    public async Task DisposeAsync()
    {
        var client = fixture.Host.Services.GetRequiredService<MeilisearchClient>();

        var deleteTask = await client.Index("items").DeleteAllDocumentsAsync();
        await client.WaitForTaskAsync(deleteTask.TaskUid);
    }

    private sealed record SearchResponse(List<Item> Results, int PageCount, int TotalCount);

    [Fact]
    public async Task GetSearch_WithoutApiPrefix_ReturnsSameResultAsApiRoute()
    {
        var legacyResult = await fixture.Host.Scenario(s =>
        {
            s.Get.Url("/search?searchTerm=zet");
            s.StatusCodeShouldBe(200);
        });

        var canonicalResult = await fixture.Host.Scenario(s =>
        {
            s.Get.Url("/api/search?searchTerm=zet");
            s.StatusCodeShouldBe(200);
        });

        var legacy = await legacyResult.ReadAsJsonAsync<SearchResponse>();
        var canonical = await canonicalResult.ReadAsJsonAsync<SearchResponse>();

        Assert.NotNull(legacy);
        Assert.NotNull(canonical);
        Assert.Equal(_item.Id, Assert.Single(legacy!.Results).Id);
        Assert.Equal(_item.Id, Assert.Single(canonical!.Results).Id);
    }
}
