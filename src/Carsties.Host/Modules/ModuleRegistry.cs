using Carsties.Auctions;
using Carsties.Bids;
using System.Reflection;
using Carsties.Notifications;
using Carsties.Search;

namespace Carsties.Host.Modules;

/// <summary>
/// Modul kayit noktasi (C2: Auction + Bid + Search + Notification).
/// Kural: tek Wolverine runtime (store: auctions / auctions_rmq — ADR-0006);
/// <c>UseWolverine</c> yalniz <see cref="Messaging.CarstiesWolverine"/> icinde cagrilir.
///
/// Search modulu Meilisearch:Url olmadan kaydedilmez (Test'te AuctionService.Tests
/// fixture'i Meili container'i vermez). /health/ready bu listeyi dondurur; fixture'lar
/// kumeyi tam olarak assert eder.
/// </summary>
public static class ModuleRegistry
{
    public const string CorsPolicy = "carsties-app";

    private static string[] _loaded = [];

    public static string[] Loaded => _loaded;

    /// <summary>
    /// Modulleri kaydeder ve Wolverine'in handler kesfi icin assembly listesini dondurur.
    /// Search modulu kapaliysa assembly'si de VERILMEZ: aksi halde Wolverine Search
    /// handler'larini kesfeder, MeilisearchClient cozulemez ve handler codegen'i patlar.
    /// </summary>
    public static Assembly[] AddCarstiesModules(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var loaded = new List<string>();
        var handlerAssemblies = new List<Assembly>();

        services.AddAuctionModule(configuration);
        loaded.Add("auction");
        handlerAssemblies.Add(typeof(AuctionModule).Assembly);

        services.AddBidModule(configuration);
        loaded.Add("bid");
        handlerAssemblies.Add(typeof(BidModule).Assembly);

        if (services.AddSearchModule(configuration, environment))
        {
            loaded.Add("search");
            handlerAssemblies.Add(typeof(SearchModule).Assembly);
        }

        services.AddNotificationModule();
        loaded.Add("notification");
        handlerAssemblies.Add(typeof(NotificationModule).Assembly);

        _loaded = [.. loaded];

        return [.. handlerAssemblies];
    }

    public static WebApplication MapCarstiesModules(this WebApplication app)
    {
        app.MapAuctionModule();
        app.MapBidModule();
        app.MapSearchModule();
        app.MapNotificationModule(CorsPolicy);

        return app;
    }
}
