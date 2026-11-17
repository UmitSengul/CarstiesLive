using Carsties.Bids.Data;
using Carsties.Bids.Messages;
using Carsties.Bids.Models;
using Contracts;
using Wolverine;

namespace Carsties.Bids.Handlers;

public class AuctionCreatedHandler(IBidRepository repository, ILogger<AuctionCreatedHandler> logger)
{
    public async Task Handle(AuctionCreated message, IMessageBus bus)
    {
        var auction = new Auction
        {
            Id = message.Id,
            AuctionEnd = message.AuctionEnd,
            Seller = message.Seller,
            ReservePrice = message.ReservePrice,
        };
        
        // on conflict do nothing: ayni AuctionCreated tekrar teslim edilirse
        // (retry/redelivery) schedule'i ikinci kez olusturmayiz.
        var inserted = await repository.CreateAuctionAsync(auction);
        if (!inserted)
        {
            logger.LogWarning("AuctionCreated tekrar teslim edildi, atlandi: {AuctionId}", auction.Id);
            return;
        }

        var auctionEndUtc = new DateTimeOffset(DateTime
            .SpecifyKind(auction.AuctionEnd, DateTimeKind.Utc));

        await bus.ScheduleAsync(new CheckAuctionEnded(auction.Id), auctionEndUtc);
    }
}