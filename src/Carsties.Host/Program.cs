using Carsties.Bids.Backfill;
using Carsties.Host.Compat;
using Carsties.Host.Messaging;
using Carsties.Host.Modules;
using Microsoft.AspNetCore.Authentication.JwtBearer;

var builder = WebApplication.CreateBuilder(args);
var configuration = builder.Configuration;

// CORS/SignalR: tarayici app.<domain> -> api.<domain> (cross-origin, credentials).
// Eksikse fail-fast: sessizce CORS'suz acilmak SignalR'i bozar.
var clientApp = configuration["ClientApp"];
if (string.IsNullOrWhiteSpace(clientApp))
    throw new InvalidOperationException(
        "ClientApp bos: CORS/SignalR origin'i icin zorunlu (or. https://carsties.example.com)");

// Moduller: Auction (MVC + EF outbox), Bid (Dapper), Search (Meilisearch), Notification (SignalR).
var handlerAssemblies = builder.Services.AddCarstiesModules(configuration, builder.Environment);

builder.Services.AddCors(options => options.AddPolicy(ModuleRegistry.CorsPolicy, policy => policy
    .WithOrigins(clientApp)
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials()));

// Tek JWT dogrulama: iki servis de ayni ayarlari kullaniyordu (Authority=IdentityServiceUrl).
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = configuration["IdentityServiceUrl"];
        options.RequireHttpsMetadata = false;
        options.TokenValidationParameters.ValidateAudience = false;
        options.TokenValidationParameters.NameClaimType = "username";
    });
builder.Services.AddAuthorization();

// TEK Wolverine runtime (store: auctions / auctions_rmq — ADR-0006).
builder.Host.UseCarstiesWolverine(configuration, handlerAssemblies);

var app = builder.Build();

// Middleware sirasi (acik ve deterministik):
// rewrite -> routing -> CORS -> auth. UseRouting'i acikca cagirmak, minimal hosting'in
// otomatik routing'ini (rewrite'tan ONCE calisip /bids gibi yollari 404/500 yapmasini) devre disi birakir.
app.UseExceptionHandler();
app.UseGatewayRewrites();
app.UseRouting();
app.UseCors(ModuleRegistry.CorsPolicy);
app.UseAuthentication();
app.UseAuthorization();

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
