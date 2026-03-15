using CampusConnect.Components;
using CampusConnect.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Register services
builder.Services.AddScoped<IChatService, ChatService>();
builder.Services.AddScoped<IEventService, EventService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

// Routing must be enabled
app.UseRouting();

// Antiforgery middleware must be between UseRouting and endpoint mapping
app.UseAntiforgery();

// Map interactive server components
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();