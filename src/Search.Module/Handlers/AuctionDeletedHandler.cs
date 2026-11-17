using Contracts;
using Meilisearch;

namespace Carsties.Search.Handlers;

public class AuctionDeletedHandler
{
    public async Task Handle(AuctionDeleted message, MeilisearchClient client)
    {
        await client.Index("items").DeleteOneDocumentAsync(message.Id);
    }
}