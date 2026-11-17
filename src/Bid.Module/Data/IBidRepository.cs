using Carsties.Bids.DTOs;
using Carsties.Bids.Models;

namespace Carsties.Bids.Data;

public interface IBidRepository
{
    Task<Auction?> GetAuctionAsync(string auctionId);
    Task<Bid?> GetHighestBidAsync(string auctionId);
    Task<BidDto> InsertBidAsync(Bid bid);
    Task<IEnumerable<BidDto>> GetBidsForAuctionAsync(string auctionId);
    Task<Auction> CreateAuctionAsync(Auction auction);
    Task MarkAuctionFinishedAsync(string auctionId);
    Task<Bid?> GetWinningBidAsync(string auctionId);
}