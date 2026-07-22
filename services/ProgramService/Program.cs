using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Read the connection string (supplied by appsettings locally, or a Secret in Kubernetes)
var connectionString = builder.Configuration.GetConnectionString("ProgramDb");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));

var app = builder.Build();

// On startup: create the table if needed, and seed sample data once.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();

    if (!db.Sessions.Any())
    {
        db.Sessions.AddRange(
            new Session { Day = "Day 1", Track = "Cloud Computing Track",
                          SessionName = "Scaling Kubernetes in Production",
                          SpeakerName = "Dr. Anaya Perera", StartTime = "09:30", EndTime = "10:30" },
            new Session { Day = "Day 1", Track = "Cloud Computing Track",
                          SessionName = "Serverless Cost Optimisation",
                          SpeakerName = "Rajiv Menon", StartTime = "11:00", EndTime = "12:00" },
            new Session { Day = "Day 1", Track = "AI & Data Science Track",
                          SessionName = "Retrieval-Augmented Generation in Practice",
                          SpeakerName = "Dr. Lena Fischer", StartTime = "13:30", EndTime = "14:30" },
            new Session { Day = "Day 2", Track = "AI & Data Science Track",
                          SessionName = "Responsible AI and Governance",
                          SpeakerName = "Sara Whitmore", StartTime = "10:00", EndTime = "11:00" }
        );
        db.SaveChanges();
    }
}

// Return the full agenda (from the database)
app.MapGet("/api/programs", async (AppDbContext db) => await db.Sessions.ToListAsync());

// Filter by track
app.MapGet("/api/programs/track/{track}", async (string track, AppDbContext db) =>
    await db.Sessions.Where(s => s.Track.Contains(track)).ToListAsync());

app.Run();

class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
    public DbSet<Session> Sessions => Set<Session>();
}

class Session
{
    public int Id { get; set; }
    public string Day { get; set; } = "";
    public string Track { get; set; } = "";
    public string SessionName { get; set; } = "";
    public string SpeakerName { get; set; } = "";
    public string StartTime { get; set; } = "";
    public string EndTime { get; set; } = "";
}