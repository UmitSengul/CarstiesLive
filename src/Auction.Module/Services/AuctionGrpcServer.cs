using Carsties.Auctions.Data;
using Grpc.Core;
using GrpcAuctionService;

namespace Carsties.Auctions.Services;

public class AuctionGrpcServer(AuctionDbContext dbContext) : GrpcAuction.GrpcAuctionBase
{
    public override async Task<GrpcAuctionResponse> GetAuction(GetAuctionRequest request, ServerCallContext context)
    {
        var auction = await dbContext.Auctions.FindAsync(request.AuctionId)
                      ?? throw new RpcException(new Status(StatusCode.NotFound, "Auction not found"));

        return new GrpcAuctionResponse
        {
            Auction = new GrpcAuctionModel
            {
                Id = auction.Id,
                AuctionEnd = auction.AuctionEnd.ToString("o"),
                Seller = auction.Seller,
                ReservePrice = auction.ReservePrice,
                Finished = auction.Status != Entities.Status.Live
            }
        };
    }
}