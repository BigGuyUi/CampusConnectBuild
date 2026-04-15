using CampusConnect;
using CampusConnect.Components;
using CampusConnect.Services;

var builder = WebApplication.CreateBuilder(args);

// initialize DB using connection string from configuration
var sqliteConn = builder.Configuration.GetConnectionString("DefaultConnection");

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Register services
builder.Services.AddScoped<IChatService, ChatService>();
builder.Services.AddScoped<IEventService, EventService>();

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
