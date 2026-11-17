using Carsties.Host.Modules;

var builder = WebApplication.CreateBuilder(args);

// C0: modul yok. C1'den itibaren her modul kendi Add* uzantisini buradan baglar.
builder.Services.AddCarstiesModules(builder.Configuration);

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "ok", host = "carsties-api" }));
app.MapGet("/health/ready", () => Results.Ok(new
{
    status = "ready",
    host = "carsties-api",
    modules = ModuleRegistry.Loaded
}));

app.MapCarstiesModules();

app.Run();

// Alba / WebApplicationFactory<Program> icin gorunur olmali.
public partial class Program;
