using Carsties.Notifications.Hubs;

namespace Carsties.Notifications;

/// <summary>
/// Notification modulu (eski NotificationService). SignalR hub'i host'ta yasar;
/// yol eskisiyle ayni: <c>/notifications</c>. CORS politikasi host'tan verilir
/// (eski GatewayService'in customPolicy'si yerine).
/// </summary>
public static class NotificationModule
{
    public static IServiceCollection AddNotificationModule(this IServiceCollection services)
    {
        services.AddSignalR();
        return services;
    }

    public static WebApplication MapNotificationModule(this WebApplication app, string corsPolicy)
    {
        app.MapHub<NotificationHub>("/notifications").RequireCors(corsPolicy);
        return app;
    }
}
