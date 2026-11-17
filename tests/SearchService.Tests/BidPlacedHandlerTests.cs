using Carsties.Auctions.Data;
using Carsties.Search.Models;
using AuctionEntity = Carsties.Auctions.Entities.Auction;
using AuctionItem = Carsties.Auctions.Entities.Item;
using AuctionStatus = Carsties.Auctions.Entities.Status;
using Contracts;
using Meilisearch;
using Microsoft.Extensions.DependencyInjection;
using Wolverine.Tracking;

namespace SearchService.Tests;

[Collection("search-service")]
public class BidPlacedHandlerTests(AppFixture fixture) : IAsyncLifetime
{
    private readonly Item _item = TestData.CreateItem();
    
    public async Task InitializeAsync()
    {
        var client = fixture.Host.Services.GetRequiredService<MeilisearchClient>();
        
        var addTask = await client.Index("items")
            .AddDocumentsAsync([_item], primaryKey: "id");
        
        await client.WaitForTaskAsync(addTask.TaskUid);

        // C2: tek host'ta BidPlaced mesajini Auction modulu de isler (birlesik handler
        // zinciri); auction satiri yoksa o handler firlatir ve Search handler'i hic calismaz.
        using var scope = fixture.Host.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AuctionDbContext>();

        dbContext.Auctions.Add(new AuctionEntity
        {
            Id = _item.Id,
            Seller = _item.Seller,
            ReservePrice = _item.ReservePrice,
            Status = AuctionStatus.Live,
            AuctionEnd = _item.AuctionEnd,
            Item = new AuctionItem
            {
                Make = _item.Make,
                Model = _item.Model,
                Color = _item.Color,
                Description = _item.Description,
                Year = _item.Year,
                Mileage = _item.Mileage,
                ImageUrl = _item.ImageUrl,
            }
        });

        await dbContext.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        var client = fixture.Host.Services.GetRequiredService<MeilisearchClient>();
        
        var deleteTask = await client.Index("items").DeleteAllDocumentsAsync();
        await client.WaitForTaskAsync(deleteTask.TaskUid);

        using var scope = fixture.Host.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AuctionDbContext>();
        dbContext.Auctions.RemoveRange(dbContext.Auctions);
        await dbContext.SaveChangesAsync();
    }

    [Fact]
    public async Task Handle_WhenAcceptedBudHigherThanCurrent_UpdatesCurrentBid()
    {
        var message = new BidPlaced
        {
            Id = Guid.NewGuid().ToString(),
            AuctionId = _item.Id,
            Bidder = "bob",
            BidTime = DateTime.UtcNow,
            Amount = 150,
            BidStatus = "Accepted"
        };

        await fixture.Host.InvokeMessageAndWaitAsync(message);
        
        var client = fixture.Host.Services.GetRequiredService<MeilisearchClient>();
        
        var item = await client.Index("items").GetDocumentAsync<Item>(_item.Id);
        
        Assert.Equal(150, item.CurrentHighBid);
    }
}