using Contracts;

namespace Carsties.Auctions.Handlers;

public class DeadLetterHandler(ILogger<DeadLetterHandler> logger)
{
    private const string DeadLetterQueue = "wolverine-dead-letter-queue";

    public Task Handle(AuctionCreated message, Wolverine.Envelope envelope)
    {
        // Wolverine 6.x bir mesaj tipinin TUM handler'larini tek zincirde calistirir;
        // bu yuzden normal akista da cagrilir. Amac yalniz DLQ'ya dusen mesaji gormek.
        if ((envelope.Destination?.ToString() ?? string.Empty).Contains(DeadLetterQueue) != true)
            return Task.CompletedTask;

        logger.LogError("Dead letter handler triggered for {AuctionId} {Make} {Model}",
            message.Id, message.Make, message.Model);

        return Task.CompletedTask;
    }
}