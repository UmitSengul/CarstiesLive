using Microsoft.AspNetCore.Rewrite;

namespace Carsties.Host.Compat;

/// <summary>
/// YARP <c>GatewayService</c>'in yerine gecici uyum katmani (C2).
///
/// web-app (server-side BFF) <c>/auctions</c>, <c>/bids</c>, <c>/search</c> cagiriyor;
/// gateway bunlari <c>/api/*</c> yoluna ceviriyordu. Ayni davranis burada, tek surecte:
///   /auctions/{**}, /bids/{**}, /search/{**} -> /api/...
///
/// - Metot ayrimi yok: GET/POST/PUT/DELETE ayni sekilde cevrilir; yetki controller/endpoint
///   tarafindan uygulanir (gateway'in AuthorizationPolicy=default davranisiyla esdeger).
/// - Query string korunur (rewriter dokunmaz).
/// - <c>/notifications</c> (SignalR) ve <c>/health</c> cevrilmez.
///
/// NOT: RewriteRule'a giden yol bas "/" ICERMEZ.
/// </summary>
public static class GatewayRewrites
{
    public static IApplicationBuilder UseGatewayRewrites(this IApplicationBuilder app)
    {
        return app.UseRewriter(new RewriteOptions()
            .AddRewrite(@"^auctions(/.*)?$", "api/auctions$1", skipRemainingRules: true)
            // Bid endpoint'leri MapGroup("/api/bids") + MapPost("/") -> "/api/bids/" ister;
            // web-app cagrisi "/bids?auctionId=..&amount=.." (sondaki slash yok).
            .AddRewrite(@"^bids$", "api/bids/", skipRemainingRules: true)
            .AddRewrite(@"^bids(/.*)$", "api/bids$1", skipRemainingRules: true)
            .AddRewrite(@"^search(/.*)?$", "api/search$1", skipRemainingRules: true));
    }
}
