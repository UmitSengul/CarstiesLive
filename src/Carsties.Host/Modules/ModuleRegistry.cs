using Carsties.Auctions;
using Carsties.Bids;

namespace Carsties.Host.Modules;

/// <summary>
/// Modul kayit noktasi. Her dilimde bir modul eklenir (C1: Auction + Bid,
/// C2: Search + Notification).
/// Kural: tek Wolverine runtime (store: auctions / auctions_rmq — ADR-0006);
/// <c>UseWolverine</c> yalniz <see cref="Messaging.CarstiesWolverine"/> icinde cagrilir.
/// </summary>
public static class ModuleRegistry
{
    public static string[] Loaded => ["auction", "bid"];

    public static IServiceCollection AddCarstiesModules(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddAuctionModule(configuration);
        services.AddBidModule(configuration);

        return services;
    }

    public static WebApplication MapCarstiesModules(this WebApplication app)
    {
        app.MapAuctionModule();
        app.MapBidModule();

        return app;
    }
}
