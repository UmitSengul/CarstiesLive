using Contracts;
using Meilisearch;

namespace Carsties.Search.Handlers;

public class AuctionUpdatedHandler
{
    public async Task Handle(AuctionUpdated message, MeilisearchClient client)
    {
        await client.Index("items").UpdateDocumentsAsync([message]);
    }
}