var builder = WebApplication.CreateBuilder(args);

// Allow the browser (frontend) to call this service from a different origin.
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
});

// HttpClient to talk to ClickHouse over its HTTP interface.
var clickhouseUrl = builder.Configuration["ClickHouseUrl"] ?? "http://localhost:8123";
builder.Services.AddHttpClient("clickhouse", client =>
{
    client.BaseAddress = new Uri(clickhouseUrl);
});

var app = builder.Build();
app.UseCors();

// Receive an analytics event from the frontend and store it in ClickHouse.
app.MapPost("/api/track", async (TrackEvent ev, IHttpClientFactory httpFactory) =>
{
    var client = httpFactory.CreateClient("clickhouse");

    // Escape single quotes to avoid breaking the SQL.
    string Clean(string s) => (s ?? "").Replace("'", "''");

    var sql =
        $"INSERT INTO webanalytics.events (event_type, event_detail, session_id, page_url) " +
        $"VALUES ('{Clean(ev.EventType)}', '{Clean(ev.EventDetail)}', '{Clean(ev.SessionId)}', '{Clean(ev.PageUrl)}')";

    var response = await client.PostAsync(
        "/?user=analytics&password=analytics123",
        new StringContent(sql));

    return response.IsSuccessStatusCode
        ? Results.Ok(new { status = "recorded" })
        : Results.StatusCode(500);
});

// Simple health/test endpoint
app.MapGet("/api/track/health", () => Results.Ok("analytics collector running"));

app.Run();

record TrackEvent(string EventType, string EventDetail, string SessionId, string PageUrl);