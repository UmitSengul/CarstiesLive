using Carsties.Bids.Data;
using Carsties.Bids.Messages;
using Carsties.Bids.Models;
using Contracts;
using Wolverine;

namespace Carsties.Bids.Handlers;

public class AuctionCreatedHandler(IBidRepository repository)
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
        
        await repository.CreateAuctionAsync(auction);
        
        var auctionEndUtc = new DateTimeOffset(DateTime
            .SpecifyKind(auction.AuctionEnd, DateTimeKind.Utc));

        await bus.ScheduleAsync(new CheckAuctionEnded(auction.Id), auctionEndUtc);
    }
}