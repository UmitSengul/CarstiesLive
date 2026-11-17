using System.Globalization;
using Carsties.Bids.Models;
using Grpc.Core;
using Grpc.Net.Client;
using GrpcAuctionService;

namespace Carsties.Bids.Services;

public class GrpcAuctionClient(IConfiguration config)
{
    public Auction? GetAuction(string auctionId)
    {
        using var channel = GrpcChannel.ForAddress(config["GrpcAuctionServiceUrl"]!);
        var client = new GrpcAuction.GrpcAuctionClient(channel);

        try
        {
            var reply = client.GetAuction(new GetAuctionRequest{AuctionId = auctionId});

            return new Auction
            {
                Id = reply.Auction.Id,
                AuctionEnd = DateTime.Parse(reply.Auction.AuctionEnd,
                    CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
                Seller = reply.Auction.Seller,
                ReservePrice = reply.Auction.ReservePrice,
                Finished = reply.Auction.Finished,
            };
        }
        catch (RpcException e) when (e.StatusCode == StatusCode.NotFound)
        {
            return null;
        }
    }
}