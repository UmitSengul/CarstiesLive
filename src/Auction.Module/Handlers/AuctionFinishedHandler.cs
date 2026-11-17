using Carsties.Auctions.Data;
using Carsties.Auctions.Entities;
using Contracts;

namespace Carsties.Auctions.Handlers;

public class AuctionFinishedHandler
{
    public async Task Handle(AuctionFinished message, AuctionDbContext dbContext)
    {
        var auction = await dbContext.Auctions.FindAsync(message.AuctionId)
            ??  throw new InvalidOperationException("Auction not found");

        if (message.ItemSold)
        {
            auction.Winner = message.Winner;
            auction.SoldAmount = message.Amount;
        }

        auction.Status = auction.SoldAmount > auction.ReservePrice
            ? Status.Finished
            : Status.ReserveNotMet;
        
        await dbContext.SaveChangesAsync();
    }
}