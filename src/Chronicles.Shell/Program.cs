using System.Globalization;
using System.Reflection;
using Chronicles.Game;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://127.0.0.1:5211");
var app = builder.Build();
var root = args.Contains("--preview", StringComparer.Ordinal)
    ? Path.Combine(Path.GetTempPath(), "seonro-preview")
    : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SeonroMansangrok", "worlds");
var session = new GameSession(root);
using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Chronicles.Shell.index.html")!;
using var reader = new StreamReader(stream);
var html = reader.ReadToEnd();
app.MapGet("/", () => Results.Content(html, "text/html; charset=utf-8"));
app.MapPost("/api", async (HttpRequest request) =>
{
    // The local UI is the only HTTP caller permitted to mutate the desktop session.
    if (request.Headers.Origin.Count > 0 && !string.Equals(request.Headers.Origin.ToString(), "http://127.0.0.1:5211", StringComparison.Ordinal))
        return Results.StatusCode(403);
    if (request.ContentLength > 16384 || !request.HasJsonContentType()) return Results.BadRequest();
    using var body = new StreamReader(request.Body);
    var json = await body.ReadToEndAsync(request.HttpContext.RequestAborted);
    return Results.Content(session.HandleJson(json), "application/json; charset=utf-8");
});
app.Lifetime.ApplicationStopping.Register(session.Suspend);
if (!args.Contains("--preview", StringComparer.Ordinal))
    Console.WriteLine("선로만상록: http://127.0.0.1:5211 을 브라우저에서 여세요.");
await app.RunAsync();
