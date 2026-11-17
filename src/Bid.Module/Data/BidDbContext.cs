using System.Data;
using System.Data.Common;
using Npgsql;

namespace Carsties.Bids.Data;

/// <summary>
/// Dapper baglantisi. DI'da <b>scoped</b> kayitlidir: her istek/handler kendi
/// NpgsqlConnection'ini alir ve scope sonunda bu sinif tarafindan kapatilir.
/// (Singleton idi; birlesik host'ta esZamanli handler'lar ayni baglantiya
/// yazinca NpgsqlOperationInProgressException aliniyordu.)
/// </summary>
public class BidDbContext : IDisposable
{
    public BidDbContext(IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("BidDbConnection");
        DbConnection = new NpgsqlConnection(connectionString);
    }

    public IDbConnection DbConnection { get; }

    // Scoped servis: DI scope sonunda cagrilir. Baglanti acik kalmis olsa da
    // NpgsqlConnection.Dispose havuzdan iade eder; GC'ye birakilmaz.
    public void Dispose()
    {
        DbConnection?.Dispose();
    }
}
