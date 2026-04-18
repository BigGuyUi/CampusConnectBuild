using System;
using CampusConnect;
using CampusConnect.Components;
using CampusConnect.Services;
using Microsoft.Data.Sqlite;
using System.IO;

string? ResolveWebRootFromEnvOrArgs(string[] args)
{
    // Prefer explicit --webroot=path or env var ASPNETCORE_WEBROOT
    string? fromEnv = Environment.GetEnvironmentVariable("ASPNETCORE_WEBROOT");
    if (!string.IsNullOrWhiteSpace(fromEnv))
        return fromEnv;

    foreach (var a in args)
    {
        if (a.StartsWith("--webroot=", StringComparison.OrdinalIgnoreCase))
            return a.Substring("--webroot=".Length).Trim('"');
    }

    return null;
}

var argsArray = args ?? Array.Empty<string>();
var candidate = ResolveWebRootFromEnvOrArgs(argsArray);

// Only use the candidate if the directory actually exists; otherwise let the host pick the default
string? safeWebRoot = null;
if (!string.IsNullOrWhiteSpace(candidate))
{
    try
    {
        if (Path.IsPathRooted(candidate))
        {
            if (Directory.Exists(candidate))
                safeWebRoot = candidate;
            else
                Console.WriteLine($"Warning: requested web root '{candidate}' does not exist; ignoring.");
        }
        else
        {
            // relative -> resolve against current directory
            var resolved = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), candidate));
            if (Directory.Exists(resolved))
                safeWebRoot = resolved;
            else
                Console.WriteLine($"Warning: requested web root '{resolved}' does not exist; ignoring.");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Warning: error validating web root '{candidate}': {ex.Message}; ignoring.");
    }
}

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = argsArray,
    WebRootPath = safeWebRoot
});

// initialize DB using connection string from configuration
var sqliteConn = builder.Configuration.GetConnectionString("DefaultConnection")
                 ?? throw new InvalidOperationException("Missing connection string 'DefaultConnection'.");

// Ensure database file & schema exist (development convenience) using DatabaseHelper
DatabaseHelper.IntialiseDatabase(sqliteConn);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Register services
builder.Services.AddScoped<IChatService, ChatService>();
builder.Services.AddScoped<IEventService, EventService>();
builder.Services.AddScoped<ISocietyService, SocietyService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

// Routing must be enabled before antiforgery middleware
app.UseRouting();

// Antiforgery middleware must appear between UseRouting and endpoint mapping
app.UseAntiforgery();

// (If you add authentication/authorization, call them before UseAntiforgery)
// app.UseAuthentication();
// app.UseAuthorization();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
