using Carsties.Bids.Data;
using Carsties.Bids.Messages;
using Contracts;
using Wolverine;

namespace Carsties.Bids.Handlers;

public class CheckAuctionEndedHandler(IBidRepository repository, IMessageBus bus)
{
    public async Task Handle(CheckAuctionEnded message)
    {
        var auction = await repository.GetAuctionAsync(message.AuctionId);

        if (auction == null || auction.Finished) return;

        // Kosullu update: duplike schedule/teslimat tek kez publish eder (idempotent).
        if (!await repository.MarkAuctionFinishedAsync(auction.Id)) return;

        var winningBid = await repository.GetWinningBidAsync(auction.Id);

        await bus.PublishAsync(new AuctionFinished
        {
            ItemSold = winningBid != null,
            AuctionId = auction.Id,
            Winner = winningBid?.Bidder,
            Amount = winningBid?.Amount ?? 0,
            Seller = auction.Seller,
        });
    }
}