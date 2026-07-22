using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("RegistrationDb");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));

// HttpClient lets this service call the Event Service over HTTP.
// The base address comes from configuration so it differs local vs in-cluster.
var eventServiceUrl = builder.Configuration["EventServiceUrl"] ?? "http://localhost:5148";
builder.Services.AddHttpClient("events", client =>
{
    client.BaseAddress = new Uri(eventServiceUrl);
});

var app = builder.Build();

// On startup: create the Registrations table if needed.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
}

// Save a registration, then ask the Event Service to reduce that event's seats
app.MapPost("/api/register", async (RegistrationRequest req, AppDbContext db, IHttpClientFactory httpFactory) =>
{
    // 1) ask the Event Service to reduce seats (it owns the seat count)
    var client = httpFactory.CreateClient("events");
    var reduceResponse = await client.PostAsJsonAsync(
        $"/api/events/{req.EventId}/reduce-seats", new { count = req.TicketCount });

    if (!reduceResponse.IsSuccessStatusCode)
    {
        var reason = await reduceResponse.Content.ReadAsStringAsync();
        return Results.BadRequest($"Could not reserve seats: {reason}");
    }

    // 2) seats reserved successfully — now save the registration
    var reg = new Registration
    {
        EventId = req.EventId,
        Name = req.Name,
        Email = req.Email,
        TicketCount = req.TicketCount,
        Timestamp = DateTime.UtcNow
    };
    db.Registrations.Add(reg);
    await db.SaveChangesAsync();

    // include what the Event Service told us about remaining seats
    var result = await reduceResponse.Content.ReadAsStringAsync();
    return Results.Ok(new { registration = reg, eventServiceResponse = result });
});

// See all registrations
app.MapGet("/api/register", async (AppDbContext db) => await db.Registrations.ToListAsync());

app.Run();

class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
    public DbSet<Registration> Registrations => Set<Registration>();
}

class Registration
{
    public int Id { get; set; }
    public int EventId { get; set; }
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public int TicketCount { get; set; }
    public DateTime Timestamp { get; set; }
}

record RegistrationRequest(int EventId, string Name, string Email, int TicketCount);