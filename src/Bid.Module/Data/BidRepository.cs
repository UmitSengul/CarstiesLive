using Carsties.Bids.DTOs;
using Carsties.Bids.Models;
using Dapper;
using Mapster;

namespace Carsties.Bids.Data;

public class BidRepository(BidDbContext dbContext) : IBidRepository
{
    public async Task<Auction?> GetAuctionAsync(string auctionId)
    {
        return await dbContext.DbConnection.QuerySingleOrDefaultAsync<Auction>(
            "select * from auctions where id = @auctionId", new { auctionId }
        );
    }

    public async Task<Bid?> GetHighestBidAsync(string auctionId)
    {
        return await dbContext.DbConnection.QuerySingleOrDefaultAsync<Bid>(
            """
            select * from bids
            where auctionid = @auctionId and bidstatus in (@Accepted, @AcceptedBelowReserve)
            order by amount desc
            limit 1
            """, new
            {
                auctionId,
                Accepted = (int)BidStatus.Accepted,
                AcceptedBelowReserve = (int)BidStatus.AcceptedBelowReserve
            }
        );
    }

    public async Task<BidDto> InsertBidAsync(Bid bid)
    {
        await dbContext.DbConnection.ExecuteAsync(
            """
            insert into bids (id, auctionid, bidder, bidtime, amount, bidstatus)
            values (@Id, @AuctionId, @Bidder, @BidTime, @Amount, @BidStatus)
            """, bid
        );
        return bid.Adapt<BidDto>();
    }

    public async Task<IEnumerable<BidDto>> GetBidsForAuctionAsync(string auctionId)
    {
        var bids = await dbContext.DbConnection.QueryAsync<Bid>(
            """
            select * from bids
            where auctionid = @auctionId
            order by bidtime desc
            """, new { auctionId }
        );
        
        return bids.Adapt<IEnumerable<BidDto>>();
    }

    /// <summary>
    /// Idempotent insert: Wolverine at-least-once teslim eder; ayni AuctionCreated
    /// tekrar gelirse PK ihlali yerine 0 doner (retry -> DLQ onlenir).
    /// </summary>
    public async Task<bool> CreateAuctionAsync(Auction auction)
    {
        var rows = await dbContext.DbConnection.ExecuteAsync(
            """
            insert into auctions (id, auctionend, seller, reserveprice)
            values (@Id, @AuctionEnd, @Seller, @ReservePrice)
            on conflict (id) do nothing
            """, auction
        );

        return rows > 0;
    }

    /// <summary>
    /// Kosullu update: yalniz finished=false -> true gecisinde 1 doner. Ayni auction
    /// icin duplike CheckAuctionEnded (backfill + eski schedule) tek kez publish eder.
    /// </summary>
    public async Task<bool> MarkAuctionFinishedAsync(string auctionId)
    {
        var rows = await dbContext.DbConnection.ExecuteAsync(
            """
            update auctions set finished = true where id = @auctionId and finished = false
            """, new { auctionId }
        );

        return rows > 0;
    }

    /// <summary>Acilis backfill'i icin: bitmemis auction'lar.</summary>
    public async Task<IEnumerable<Auction>> GetUnfinishedAuctionsAsync()
    {
        return await dbContext.DbConnection.QueryAsync<Auction>(
            "select id, auctionend, seller, reserveprice, finished from auctions where finished = false"
        );
    }

    public async Task<Bid?> GetWinningBidAsync(string auctionId)
    {
        return await dbContext.DbConnection.QuerySingleOrDefaultAsync<Bid>(
            """
            select * from bids
            where auctionid = @auctionId and bidstatus = @Accepted
            order by amount desc
            limit 1
            """, new
            {
                auctionId,
                Accepted = (int)BidStatus.Accepted
            }
        );
    }
}