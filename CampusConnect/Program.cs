using System;
using CampusConnect;
using CampusConnect.Components;
using CampusConnect.Services;
using Microsoft.Data.Sqlite;

var builder = WebApplication.CreateBuilder(args);

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
