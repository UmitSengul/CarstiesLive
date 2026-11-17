using Carsties.Auctions.Data;
using Contracts;
using Mapster;
using Microsoft.EntityFrameworkCore;

namespace Carsties.Auctions.Services;

/// <summary>
/// <see cref="IAuctionLookup"/> implementasyonu — kendi <see cref="AuctionDbContext"/>'i uzerinden.
/// Bid'in eski gRPC cagrisi ve Search'un eski HTTP cagrisi (AuctionSvcUrl) bununla degisti.
/// </summary>
public sealed class AuctionLookup(AuctionDbContext dbContext) : IAuctionLookup
{
    public Task<AuctionSnapshot?> FindAsync(string auctionId, CancellationToken cancellationToken = default)
    {
        return dbContext.Auctions
            .AsNoTracking()
            .Where(x => x.Id == auctionId)
            .ProjectToType<AuctionSnapshot>()
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AuctionSnapshot>> GetUpdatedSinceAsync(
        DateTimeOffset? since, CancellationToken cancellationToken = default)
    {
        var query = dbContext.Auctions.AsNoTracking();

        if (since is { } sinceValue)
        {
            var sinceUtc = sinceValue.UtcDateTime;
            query = query.Where(x => x.UpdatedAt > sinceUtc);
        }

        return await query.ProjectToType<AuctionSnapshot>().ToListAsync(cancellationToken);
    }
}
