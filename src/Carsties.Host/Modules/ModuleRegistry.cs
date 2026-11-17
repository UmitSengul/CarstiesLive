namespace Carsties.Host.Modules;

/// <summary>
/// Modul kayit noktasi. C0'da bos; her dilimde bir modul eklenir
/// (Auction, Bid, Search, Notification).
/// Kural: tek Wolverine runtime (store: auctions / auctions_rmq — ADR-0006),
/// bu yuzden UseWolverine cagrisi yalniz burada yapilir.
/// </summary>
public static class ModuleRegistry
{
    public static string[] Loaded => [];

    public static IServiceCollection AddCarstiesModules(
        this IServiceCollection services,
        IConfiguration configuration) => services;

    public static WebApplication MapCarstiesModules(this WebApplication app) => app;
}
