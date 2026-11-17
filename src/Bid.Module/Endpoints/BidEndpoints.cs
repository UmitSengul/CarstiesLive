using System.Security.Claims;
using Carsties.Bids.Data;
using Carsties.Bids.Models;
using Contracts;
using Mapster;
using Wolverine;

namespace Carsties.Bids.Endpoints;

public static class BidEndpoints
{
    public static void MapBidEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/bids");

        group.MapPost("/", PlaceBid).RequireAuthorization();
        group.MapGet("/{auctionId}", GetBidsForAuction);
    }

    private static async Task<IResult> PlaceBid(string auctionId, int amount, ClaimsPrincipal user,
        IBidRepository repository, IMessageBus bus, IAuctionLookup auctionLookup)
    {
        var auction = await repository.GetAuctionAsync(auctionId);

        if (auction == null)
        {
            // C2: eski gRPC cagrisi (AuctionService :81) yerine surec ici lookup.
            var snapshot = await auctionLookup.FindAsync(auctionId);

            if (snapshot == null)
            {
                return Results.NotFound();
            }

            auction = new Auction
            {
                Id = snapshot.Id,
                AuctionEnd = snapshot.AuctionEnd,
                Seller = snapshot.Seller,
                ReservePrice = snapshot.ReservePrice,
                // gRPC server'in eski semantigi: Finished = Status != Live
                Finished = snapshot.Status != "Live",
            };
        }

        if (auction.Seller == user.Identity?.Name)
        {
            return Results.BadRequest("You cannot bid on your own item");
        }

        var bid = new Bid
        {
            AuctionId = auctionId,
            Bidder = user.Identity?.Name ?? "Unknown bidder",
            Amount = amount,
        };

        if (auction.AuctionEnd < DateTime.UtcNow || auction.Finished)
        {
            bid.BidStatus = BidStatus.Finished;
        }
        else
        {
            var highBid = await repository.GetHighestBidAsync(auctionId);

            if (highBid != null && amount <= highBid.Amount)
            {
                bid.BidStatus = BidStatus.TooLow;
            }
            else
            {
                bid.BidStatus = amount >= auction.ReservePrice
                    ? BidStatus.Accepted
                    : BidStatus.AcceptedBelowReserve;
            }
        }
    
        var bidDto = await repository.InsertBidAsync(bid);
        
        await bus.PublishAsync(bidDto.Adapt<BidPlaced>());
    
        return Results.Ok(bidDto);
    }

    private static async Task<IResult> GetBidsForAuction(string auctionId, IBidRepository repository)
    {
        var bids = await repository.GetBidsForAuctionAsync(auctionId);
    
        return Results.Ok(bids);
    }
}