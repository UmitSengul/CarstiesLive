using Carsties.Host.Messaging;
using Carsties.Host.Modules;
using Carsties.Bids.Backfill;
using Microsoft.AspNetCore.Authentication.JwtBearer;

var builder = WebApplication.CreateBuilder(args);

// Moduller: Auction (MVC + gRPC + EF outbox), Bid (Dapper + minimal API).
builder.Services.AddCarstiesModules(builder.Configuration);

// Tek JWT dogrulama: iki servis de ayni ayarlari kullaniyordu (Authority=IdentityServiceUrl).
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = builder.Configuration["IdentityServiceUrl"];
        options.RequireHttpsMetadata = false;
        options.TokenValidationParameters.ValidateAudience = false;
        options.TokenValidationParameters.NameClaimType = "username";
    });
builder.Services.AddAuthorization();

// TEK Wolverine runtime (store: auctions / auctions_rmq — ADR-0006).
builder.Host.UseCarstiesWolverine(builder.Configuration);

var app = builder.Build();

// Middleware: eski AuctionService ile ayni sira (endpoint'lerden once).
app.UseExceptionHandler();

app.MapGet("/health", () => Results.Ok(new { status = "ok", host = "carsties-api" }));
app.MapGet("/health/ready", () => Results.Ok(new
{
    status = BackfillState.Error is not null ? "degraded"
        : BackfillState.Status == "done" ? "ready" : "starting",
    host = "carsties-api",
    modules = ModuleRegistry.Loaded,
    backfill = new
    {
        status = BackfillState.Status,
        lastRunUtc = BackfillState.LastRunUtc,
        immediate = BackfillState.Immediate,
        scheduled = BackfillState.Scheduled,
        error = BackfillState.Error
    }
}));

app.MapCarstiesModules();

app.Run();

// Alba / WebApplicationFactory<Program> icin gorunur olmali.
public partial class Program;
