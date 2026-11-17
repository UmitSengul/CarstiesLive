using Contracts;
using Wolverine;
using Wolverine.ErrorHandling;
using Wolverine.EntityFrameworkCore;
using Wolverine.Postgresql;
using Wolverine.RabbitMQ;

namespace Carsties.Host.Messaging;

/// <summary>
/// TEK Wolverine runtime (ADR-0006). <c>UseWolverine</c> ve <c>PersistMessagesWithPostgresql</c>
/// yalniz burada cagrilir.
///
/// C2 — kuyruk topolojisi (ONEMLI): Wolverine 6.x bir mesaj tipinin TUM handler'larini tek
/// zincirde calistirir. Bu yuzden birlesik host'ta mesaj tipi basina TEK kuyruk dinlenir
/// (mesaj basina Bid + Search + Notification handler'lari bir kez calisir). Ayni exchange'e
/// bagli ikinci bir kuyruk dinlemek ayni isi ikinci kez yapmak olurdu.
///   auction-created  -> bid-auction-created            (Bid + Search + Notification)
///   bid-placed       -> auction-bid-placed             (Auction + Search + Notification)
///   auction-finished -> auction-auction-finished       (Auction + Search + Notification)
///   auction-updated  -> search-auction-updated         (Search)
///   auction-deleted  -> search-auction-deleted         (Search)
///   (wolverine-dead-letter-queue)
/// </summary>
public static class CarstiesWolverine
{
    public static IHostBuilder UseCarstiesWolverine(
        this IHostBuilder host, IConfiguration configuration, IReadOnlyList<System.Reflection.Assembly> handlerAssemblies)
    {
        var auctionsDb = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(auctionsDb))
            throw new InvalidOperationException("ConnectionStrings:DefaultConnection bos (auctions)");

        return host.UseWolverine(opts =>
        {
            // 1) Store + atomik outbox (is verisiyle ayni transaction).
            opts.PersistMessagesWithPostgresql(auctionsDb, "auctions_rmq");
            opts.UseEntityFrameworkCoreTransactions();

            // 2) Handler kesfi allow-list'tir: kayitli modullerin assembly'leri acikca eklenir.
            //    (Search kapaliysa assembly'si listede yok -> handler'i da kesfedilmez.)
            foreach (var assembly in handlerAssemblies)
            {
                opts.Discovery.IncludeAssembly(assembly);
            }

            // 3) Politikalar: C1 birlesimi + Search'un gecici hata politikasi.
            opts.Policies.UseDurableOutboxOnAllSendingEndpoints();
            opts.Policies.AlwaysMakeScheduledMessagesDurable();   // CheckAuctionEnded

            opts.OnException<Carsties.Search.Handlers.TransientSearchException>()
                .RetryWithCooldown(
                    TimeSpan.FromMilliseconds(500),
                    TimeSpan.FromMilliseconds(500),
                    TimeSpan.FromSeconds(1))
                .Then.MoveToErrorQueue();

            // 4) TEK RabbitMQ blogu.
            opts.UseRabbitMq(rabbit =>
                {
                    rabbit.HostName = configuration["RabbitMQ:Host"] ?? "localhost";
                    rabbit.VirtualHost = configuration["RabbitMQ:VirtualHost"] ?? "/";
                    rabbit.UserName = configuration["RabbitMQ:Username"] ?? "guest";
                    rabbit.Password = configuration["RabbitMQ:Password"] ?? "guest";
                })
                .DeclareExchange("auction-created", ex => ex.ExchangeType = ExchangeType.Fanout)
                .DeclareExchange("auction-updated", ex => ex.ExchangeType = ExchangeType.Fanout)
                .DeclareExchange("auction-deleted", ex => ex.ExchangeType = ExchangeType.Fanout)
                .DeclareExchange("auction-finished", ex => ex.ExchangeType = ExchangeType.Fanout)
                .DeclareExchange("bid-placed", ex => ex.ExchangeType = ExchangeType.Fanout)
                .BindExchange("auction-created").ToQueue("bid-auction-created")
                .BindExchange("auction-finished").ToQueue("auction-auction-finished")
                .BindExchange("bid-placed").ToQueue("auction-bid-placed")
                .BindExchange("auction-updated").ToQueue("search-auction-updated")
                .BindExchange("auction-deleted").ToQueue("search-auction-deleted")
                .AutoProvision();

            // 5) Publish kurallari (explicit kural = publish aninda local kopya yok).
            opts.PublishMessage<AuctionCreated>().ToRabbitExchange("auction-created");
            opts.PublishMessage<AuctionUpdated>().ToRabbitExchange("auction-updated");
            opts.PublishMessage<AuctionDeleted>().ToRabbitExchange("auction-deleted");
            opts.PublishMessage<BidPlaced>().ToRabbitExchange("bid-placed");
            opts.PublishMessage<AuctionFinished>().ToRabbitExchange("auction-finished");

            // 6) Dinleyiciler: mesaj tipi basina tek kuyruk (yukaridaki topoloji notu).
            opts.ListenToRabbitQueue("wolverine-dead-letter-queue");
            opts.ListenToRabbitQueue("bid-auction-created");
            opts.ListenToRabbitQueue("auction-bid-placed");
            opts.ListenToRabbitQueue("auction-auction-finished");
            opts.ListenToRabbitQueue("search-auction-updated");
            opts.ListenToRabbitQueue("search-auction-deleted");
        });
    }
}
