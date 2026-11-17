using Carsties.Bids.Data;
using Carsties.Bids.Messages;
using Wolverine;

namespace Carsties.Bids.Backfill;

/// <summary>
/// Cutover backfill'i (ADR-0006 §Gecis-2).
///
/// BidService'in Wolverine store'u <c>bids</c> DB'sindeydi; store artik
/// <c>auctions/auctions_rmq</c>. Eski store'daki zamanlanmis
/// <see cref="CheckAuctionEnded"/> kayitlari yeni host'a gorunmez, bu yuzden
/// <c>bids.auctions where finished = false</c> icin acilista yeniden planlanir.
///
/// Idempotent: <see cref="Handlers.CheckAuctionEndedHandler"/> finished ise cikar ve
/// <c>MarkAuctionFinishedAsync</c> kosullu UPDATE ile yalniz bir kez gecis yapar;
/// crash sonrasi kalan kayitlar da ayni yolla onarilir.
///
/// Zamanlama: Wolverine runtime'i publish'i ancak IHost tamamen basladiktan sonra
/// kabul eder (<c>IHostApplicationLifetime.ApplicationStarted</c>); bu yuzden is
/// StartAsync icinde degil, o sinyalden sonra kosar. /health/ready durumu gosterir.
/// </summary>
public sealed class AuctionEndBackfill(
    IServiceScopeFactory scopeFactory,
    IHostApplicationLifetime lifetime,
    ILogger<AuctionEndBackfill> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        lifetime.ApplicationStarted.Register(() => _ = RunAsync());
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task RunAsync()
    {
        BackfillState.Status = "running";

        try
        {
            using var scope = scopeFactory.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IBidRepository>();
            var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();

            var now = DateTimeOffset.UtcNow;
            var immediate = 0;
            var scheduled = 0;

            foreach (var auction in await repository.GetUnfinishedAuctionsAsync())
            {
                // Eski AuctionCreatedHandler ile ayni donusum (parite).
                var due = new DateTimeOffset(DateTime.SpecifyKind(auction.AuctionEnd, DateTimeKind.Utc));

                if (due <= now)
                {
                    await bus.PublishAsync(new CheckAuctionEnded(auction.Id));
                    immediate++;
                }
                else
                {
                    await bus.ScheduleAsync(new CheckAuctionEnded(auction.Id), due);
                    scheduled++;
                }
            }

            BackfillState.LastRunUtc = DateTimeOffset.UtcNow;
            BackfillState.Immediate = immediate;
            BackfillState.Scheduled = scheduled;
            BackfillState.Error = null;
            BackfillState.Status = "done";

            logger.LogInformation(
                "CheckAuctionEnded backfill tamam: {Immediate} hemen, {Scheduled} zamanlandi (toplam {Total})",
                immediate, scheduled, immediate + scheduled);
        }
        catch (Exception e)
        {
            // Backfill hatasi host'u dusurmez; /health/ready uzerinden gorunur.
            BackfillState.Status = "failed";
            BackfillState.Error = e.Message;
            logger.LogError(e, "CheckAuctionEnded backfill hatasi: {Message}", e.Message);
        }
    }
}

/// <summary>Backfill sonucu — /health/ready icin.</summary>
public static class BackfillState
{
    public static string Status { get; internal set; } = "pending";
    public static DateTimeOffset? LastRunUtc { get; internal set; }
    public static int Immediate { get; internal set; }
    public static int Scheduled { get; internal set; }
    public static string? Error { get; internal set; }
}
