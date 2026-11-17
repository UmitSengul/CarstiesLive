using Contracts;
using Microsoft.AspNetCore.SignalR;
using Carsties.Notifications.Hubs;

namespace Carsties.Notifications.Handlers;

public class AuctionFinishedHandler(IHubContext<NotificationHub> hubContext)
{
    public async Task Handle(AuctionFinished message)
    {
        Console.WriteLine($"=== Auction finished message received ===");
        
        await hubContext.Clients.All.SendAsync("AuctionFinished", message);
    }
}