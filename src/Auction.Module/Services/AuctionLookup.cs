using Carsties.Auctions.Data;
using Carsties.Auctions.Entities;
using Contracts;
using Microsoft.EntityFrameworkCore;

namespace Carsties.Auctions.Services;

/// <summary>
/// <see cref="IAuctionLookup"/> implementasyonu — kendi <see cref="AuctionDbContext"/>'i uzerinden.
/// Bid'in eski gRPC cagrisi ve Search'un eski HTTP cagrisi (AuctionSvcUrl) bununla degisti.
/// </summary>
/// <remarks>
/// 2026-11-19: DUZELTME. Aciklayici alanlar (Make/Model/Color/Description/Year/Mileage/ImageUrl)
/// <c>Items</c> tablosunda, <see cref="Auction.Item"/> navigasyonunda tutulur. Onceki surum
/// yalniz <c>Auctions</c>'i projekte ettigi icin bu alanlar null donuyordu; Search indeksi
/// null yaziyor, ana sayfada ne arac adi ne gorsel gorunuyordu.
/// Bu yuzden <c>Item</c> include edilip snapshot elle doldurulur.
/// </remarks>
public sealed class AuctionLookup(AuctionDbContext dbContext) : IAuctionLookup
{
    public async Task<AuctionSnapshot?> FindAsync(
        string auctionId, CancellationToken cancellationToken = default)
    {
        var auction = await dbContext.Auctions
            .AsNoTracking()
            .Include(x => x.Item)
            .FirstOrDefaultAsync(x => x.Id == auctionId, cancellationToken);

        return auction is null ? null : ToSnapshot(auction);
    }

    public async Task<IReadOnlyList<AuctionSnapshot>> GetUpdatedSinceAsync(
        DateTimeOffset? since, CancellationToken cancellationToken = default)
    {
        var query = dbContext.Auctions
            .AsNoTracking()
            .Include(x => x.Item)
            .AsQueryable();

        if (since is { } sinceValue)
        {
            var sinceUtc = sinceValue.UtcDateTime;
            query = query.Where(x => x.UpdatedAt > sinceUtc);
        }

        var rows = await query.ToListAsync(cancellationToken);
        return rows.Select(ToSnapshot).ToList();
    }

    private static AuctionSnapshot ToSnapshot(Auction auction) => new()
    {
        Id = auction.Id,
        ReservePrice = auction.ReservePrice,
        Seller = auction.Seller,
        Winner = auction.Winner,
        SoldAmount = auction.SoldAmount,
        CurrentHighBid = auction.CurrentHighBid,
        CreatedAt = auction.CreatedAt,
        UpdatedAt = auction.UpdatedAt,
        AuctionEnd = auction.AuctionEnd,
        Status = auction.Status.ToString(),
        Make = auction.Item.Make,
        Model = auction.Item.Model,
        Color = auction.Item.Color,
        Description = auction.Item.Description,
        Year = auction.Item.Year,
        Mileage = auction.Item.Mileage,
        ImageUrl = auction.Item.ImageUrl,
    };
}
