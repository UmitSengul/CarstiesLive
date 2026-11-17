using Carsties.Auctions.Data;
using Carsties.Auctions.Errors;
using Carsties.Auctions.Services;
using Contracts;
using Mapster;
using Microsoft.EntityFrameworkCore;
using Wolverine.EntityFrameworkCore;

namespace Carsties.Auctions;

/// <summary>
/// Auction modulu (eski AuctionService). MVC controller'lari korunur:
/// <see cref="AddControllersExtensions.AddApplicationPart"/> olmadan
/// <c>AddControllers()</c> yalniz giris assembly'sini tarar ve /api/auctions 404 olur.
/// </summary>
public static class AuctionModule
{
    public static IServiceCollection AddAuctionModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("ConnectionStrings:DefaultConnection bos (auctions)");

        services.AddControllers().AddApplicationPart(typeof(AuctionModule).Assembly);

        // Atomik outbox: store (host'ta kurulur) ile is verisi ayni DbContext uzerinden.
        services.AddDbContextWithWolverineIntegration<AuctionDbContext>(options =>
        {
            options.UseNpgsql(connectionString);
        });

        services.AddExceptionHandler<GlobalExceptionHandler>();
        services.AddProblemDetails();

        // Surec ici sorgu sozlesmesi (Bid + Search kullanir; gRPC/HTTP yerine).
        services.AddScoped<IAuctionLookup, AuctionLookup>();

        // MappingConfig : IRegister bu assembly'de -> taranmazsa AuctionDto projeksiyonu bozulur.
        TypeAdapterConfig.GlobalSettings.Scan(typeof(AuctionModule).Assembly);

        return services;
    }

    public static WebApplication MapAuctionModule(this WebApplication app)
    {
        app.MapControllers();

        try
        {
            DbInitializer.InitDb(app);
        }
        catch (Exception e)
        {
            // Eski Program.cs ile ayni davranis: init hatasi host'u dusurmez, log'lanir.
            app.Logger.LogError(e, "Auction DB init hatasi: {Message}", e.Message);
        }

        return app;
    }
}
