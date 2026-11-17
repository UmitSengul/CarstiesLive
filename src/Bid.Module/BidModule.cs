using Carsties.Bids.Backfill;
using Carsties.Bids.Data;
using Carsties.Bids.Endpoints;
using Mapster;

namespace Carsties.Bids;

/// <summary>
/// Bid modulu (eski BidService). Dapper + bids DB'si aynen kalir;
/// Wolverine store'u ve handler'lari host'a baglidir (ADR-0006).
/// </summary>
public static class BidModule
{
    public static IServiceCollection AddBidModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("BidDbConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("ConnectionStrings:BidDbConnection bos (bids)");

        // Scoped: her handler/istek kendi NpgsqlConnection'ini alir. Singleton idi;
        // birlesik host'ta esZamanli handler'lar ayni baglantiyi paylasip
        // NpgsqlOperationInProgressException ('A command is already in progress') aliyordu.
        services.AddScoped<BidDbContext>();
        services.AddScoped<IBidRepository, BidRepository>();

        // Cutover backfill'i: bids DB'sindeki eski zamanlanmis CheckAuctionEnded kayitlari
        // yeni store'da yok -> acilista yeniden planlanir (idempotent).
        services.AddHostedService<AuctionEndBackfill>();

        TypeAdapterConfig.GlobalSettings.Scan(typeof(BidModule).Assembly);

        return services;
    }

    public static WebApplication MapBidModule(this WebApplication app)
    {
        app.MapBidEndpoints();

        try
        {
            DbInitializer.InitDb(app);
        }
        catch (Exception e)
        {
            app.Logger.LogError(e, "Bid DB init hatasi: {Message}", e.Message);
        }

        return app;
    }
}
