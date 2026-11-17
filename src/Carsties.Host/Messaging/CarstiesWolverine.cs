using Carsties.Auctions.Data;
using Carsties.Bids.Data;
using Contracts;
using Wolverine;
using Wolverine.EntityFrameworkCore;
using Wolverine.Postgresql;
using Wolverine.RabbitMQ;

namespace Carsties.Host.Messaging;

/// <summary>
/// TEK Wolverine runtime (ADR-0006). Kural: bu projede <c>UseWolverine</c> ve
/// <c>PersistMessagesWithPostgresql</c> yalniz burada cagrilir; moduller kendi
/// bloklarini getirmez.
///
/// Eski iki bloktan farklar:
///  - store: bids/bids yerine auctions/auctions_rmq (tek store, atomik EF outbox);
///  - UseEntityFrameworkCoreTransactions() (Auction'dan);
///  - AlwaysMakeScheduledMessagesDurable() (Bid'den: CheckAuctionEnded);
///  - handler kesfi iki modul assembly'si icin de acik (allow-list);
///  - rabbit.VirtualHost okunur (ADR-0002: carsties vhost'u).
/// </summary>
public static class CarstiesWolverine
{
    public static IHostBuilder UseCarstiesWolverine(this IHostBuilder host, IConfiguration configuration)
    {
        var auctionsDb = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(auctionsDb))
            throw new InvalidOperationException("ConnectionStrings:DefaultConnection bos (auctions)");

        return host.UseWolverine(opts =>
        {
            // 1) Store + atomik outbox (is verisiyle ayni transaction).
            opts.PersistMessagesWithPostgresql(auctionsDb, "auctions_rmq");
            opts.UseEntityFrameworkCoreTransactions();

            // 2) Handler kesfi allow-list'tir: modul assembly'leri acikca eklenir,
            //    yoksa Rabbit'ten gelen mesajlar handler'siz kalir.
            opts.Discovery.IncludeAssembly(typeof(AuctionDbContext).Assembly);
            opts.Discovery.IncludeAssembly(typeof(BidDbContext).Assembly);

            // 3) Politikalar: iki bloktan birlesim.
            opts.Policies.UseDurableOutboxOnAllSendingEndpoints();
            opts.Policies.AlwaysMakeScheduledMessagesDurable();   // CheckAuctionEnded

            // 4) TEK RabbitMQ blogu (5 exchange / 3 binding / 4 kuyruk / 5 publish).
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
                .BindExchange("auction-finished").ToQueue("auction-auction-finished")
                .BindExchange("bid-placed").ToQueue("auction-bid-placed")
                .BindExchange("auction-created").ToQueue("bid-auction-created")
                .AutoProvision();

            // 5) Publish kurallari (explicit kural = local kopya yok, tek teslim:
            //    mesaj kuyruktan donup bir kez islenir).
            opts.PublishMessage<AuctionCreated>().ToRabbitExchange("auction-created");
            opts.PublishMessage<AuctionUpdated>().ToRabbitExchange("auction-updated");
            opts.PublishMessage<AuctionDeleted>().ToRabbitExchange("auction-deleted");
            opts.PublishMessage<BidPlaced>().ToRabbitExchange("bid-placed");
            opts.PublishMessage<AuctionFinished>().ToRabbitExchange("auction-finished");

            // 6) Dinleyiciler (kuyruk adlari servisler arasinda cakismaz).
            opts.ListenToRabbitQueue("wolverine-dead-letter-queue");
            opts.ListenToRabbitQueue("auction-auction-finished");
            opts.ListenToRabbitQueue("auction-bid-placed");
            opts.ListenToRabbitQueue("bid-auction-created");
        });
    }
}
