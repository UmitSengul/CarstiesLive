namespace Contracts;

/// <summary>
/// Auction modulunun diger modullere actigi salt-okunur auction gorunumu.
/// (C2: Bid'in gRPC cagrisi ve Search'un HTTP cagrisi yerine surec ici kullanilir.)
/// </summary>
public class AuctionSnapshot
{
    public required string Id { get; set; }
    public int ReservePrice { get; set; }
    public required string Seller { get; set; }
    public string? Winner { get; set; }
    public int? SoldAmount { get; set; }
    public int? CurrentHighBid { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime AuctionEnd { get; set; }
    public required string Status { get; set; }
    public required string Make { get; set; }
    public required string Model { get; set; }
    public required string Color { get; set; }
    public required string Description { get; set; }
    public int Year { get; set; }
    public int Mileage { get; set; }
    public required string ImageUrl { get; set; }
}

/// <summary>Auction modulunun surec ici sorgu sozlesmesi (modul sinirlari icinde).</summary>
public interface IAuctionLookup
{
    Task<AuctionSnapshot?> FindAsync(string auctionId, CancellationToken cancellationToken = default);

    /// <summary>Search indeks backfill'i: verilen zamandan sonra guncellenen auction'lar (null = hepsi).</summary>
    Task<IReadOnlyList<AuctionSnapshot>> GetUpdatedSinceAsync(
        DateTimeOffset? since, CancellationToken cancellationToken = default);
}
