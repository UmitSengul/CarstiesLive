using Carsties.Bids.DTOs;
using Carsties.Bids.Models;

namespace Carsties.Bids.Data;

public interface IBidRepository
{
    Task<Auction?> GetAuctionAsync(string auctionId);
    Task<Bid?> GetHighestBidAsync(string auctionId);
    Task<BidDto> InsertBidAsync(Bid bid);
    Task<IEnumerable<BidDto>> GetBidsForAuctionAsync(string auctionId);
    Task<bool> CreateAuctionAsync(Auction auction);
    Task<bool> MarkAuctionFinishedAsync(string auctionId);
    Task<IEnumerable<Auction>> GetUnfinishedAuctionsAsync();
    Task<Bid?> GetWinningBidAsync(string auctionId);
}