using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("EventDb");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));

var app = builder.Build();

const int LowSeatThreshold = 10;

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();

    if (!db.Events.Any())
    {
        db.Events.AddRange(
            new Event { Title = "Cloud Computing Summit", Venue = "Colombo Convention Centre",
                        StartsAt = DateTime.Parse("2026-09-15T09:00:00"), TicketPrice = 4500m,
                        Capacity = 200, SeatsAvailable = 187 },
            new Event { Title = "AI & Data Science Expo", Venue = "Hilton Grand Ballroom",
                        StartsAt = DateTime.Parse("2026-10-02T10:00:00"), TicketPrice = 6000m,
                        Capacity = 150, SeatsAvailable = 12 }
        );
        db.SaveChanges();
    }
}

// Return all events
app.MapGet("/api/events", async (AppDbContext db) => await db.Events.ToListAsync());

// Return one event by ID
app.MapGet("/api/events/{id}", async (int id, AppDbContext db) =>
    await db.Events.FindAsync(id) is Event ev ? Results.Ok(ev) : Results.NotFound());

// Reduce seats for an event — called by the Registration Service
app.MapPost("/api/events/{id}/reduce-seats", async (int id, ReduceSeatsRequest req, AppDbContext db) =>
{
    var ev = await db.Events.FindAsync(id);
    if (ev is null)
        return Results.NotFound($"No event with ID {id}");

    if (req.Count > ev.SeatsAvailable)
        return Results.BadRequest($"Only {ev.SeatsAvailable} seats left, cannot reduce by {req.Count}");

    ev.SeatsAvailable -= req.Count;
    await db.SaveChangesAsync();

    // low-seat check now lives here, where seats actually change
    if (ev.SeatsAvailable < LowSeatThreshold)
    {
        Console.WriteLine($"[LOW SEATS] Event {id} has {ev.SeatsAvailable} seats left — trigger notification!");
    }

    return Results.Ok(new { eventId = id, seatsRemaining = ev.SeatsAvailable });
});

app.Run();

class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
    public DbSet<Event> Events => Set<Event>();
}

class Event
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string Venue { get; set; } = "";
    public DateTime StartsAt { get; set; }
    public decimal TicketPrice { get; set; }
    public int Capacity { get; set; }
    public int SeatsAvailable { get; set; }
}

record ReduceSeatsRequest(int Count);