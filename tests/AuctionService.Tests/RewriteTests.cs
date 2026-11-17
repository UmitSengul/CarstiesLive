using System.Net;
using Carsties.Auctions.Data;
using Carsties.Auctions.DTOs;
using Carsties.Auctions.Entities;
using Microsoft.Extensions.DependencyInjection;

namespace AuctionService.Tests;

/// <summary>
/// C2: YARP GatewayService'in yerine gecen host ici rewrite'in paritesi.
/// web-app /auctions, /bids cagirir; gateway bunlari /api/*'ya cevirirdi.
/// </summary>
[Collection("auction-service")]
public class RewriteTests(AppFixture fixture) : IAsyncLifetime
{
    private readonly Auction _auction = TestData.CreateAuction();

    public async Task InitializeAsync()
    {
        using var scope = fixture.Host.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AuctionDbContext>();

        dbContext.Auctions.Add(_auction);

        await dbContext.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        using var scope = fixture.Host.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AuctionDbContext>();

        dbContext.Auctions.RemoveRange(dbContext.Auctions);
        await dbContext.SaveChangesAsync();
    }

    [Fact]
    public async Task GetAuctions_WithoutApiPrefix_ReturnsSameListAsApiRoute()
    {
        var legacyResult = await fixture.Host.Scenario(s =>
        {
            s.Get.Url("/auctions");
            s.StatusCodeShouldBe(200);
        });

        var canonicalResult = await fixture.Host.Scenario(s =>
        {
            s.Get.Url("/api/auctions");
            s.StatusCodeShouldBe(200);
        });

        var legacy = await legacyResult.ReadAsJsonAsync<List<AuctionDto>>();
        var canonical = await canonicalResult.ReadAsJsonAsync<List<AuctionDto>>();

        Assert.NotNull(legacy);
        Assert.NotNull(canonical);
        Assert.Contains(legacy!, x => x.Id == _auction.Id);

        Assert.Equal(
            canonical!.Select(x => x.Id).OrderBy(x => x, StringComparer.Ordinal).ToArray(),
            legacy!.Select(x => x.Id).OrderBy(x => x, StringComparer.Ordinal).ToArray());
    }

    [Fact]
    public async Task PostBid_WithoutApiPrefix_RequiresAuth()
    {
        // Not: web-app gercek cagrisi "/bids?auctionId=..&amount=.."; TestServer'da Alba'nin
        // POST+query birlestirmesi yol kodlamasini bozdugu icin burada govdesiz/query'siz
        // cagri dogrulanir, query'li varyant desktop smoke'ta curl ile kanitlanir.
        await fixture.Host.Scenario(s =>
        {
            s.RemoveRequestHeader("Authorization");
            s.Post.Json(new { }).ToUrl("/bids");
            s.StatusCodeShouldBe(HttpStatusCode.Unauthorized);
        });
    }
}
