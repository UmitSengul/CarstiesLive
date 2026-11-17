using Contracts;
using Microsoft.AspNetCore.SignalR;
using Carsties.Notifications.Hubs;

namespace Carsties.Notifications.Handlers;

public class AuctionCreatedHandler(IHubContext<NotificationHub> hubContext)
{
    public async Task Handle(AuctionCreated message)
    {
        Console.WriteLine($"=== Auction created message received ===");
        
        await hubContext.Clients.All.SendAsync("AuctionCreated", message);
    }
}