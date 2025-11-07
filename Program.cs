using SSISAnalyticsDashboard.Services;
using SSISAnalyticsDashboard.Hubs;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Register SSIS Data Service
builder.Services.AddScoped<ISSISDataService, SSISDataService>();

// Add SignalR
builder.Services.AddSignalR();

// Add session support for configuration bypass
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseSession();  // Add session middleware

app.UseAuthorization();

// Middleware to check server configuration
app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value?.ToLower() ?? "";
    
    // Skip check for ServerConfig, static files, and SignalR hub
    if (path.StartsWith("/serverconfig") || 
        path.StartsWith("/lib") || 
        path.StartsWith("/css") || 
        path.StartsWith("/js") ||
        path.StartsWith("/dashboardhub"))
    {
        await next();
        return;
    }

    // Check if configuration was just saved (bypass check for this request)
    if (context.Session.GetString("ConfigJustSaved") == "true")
    {
        context.Session.Remove("ConfigJustSaved");
        await next();
        return;
    }

    var configuration = context.RequestServices.GetRequiredService<IConfiguration>();
    var connectionString = configuration.GetConnectionString("SSISDBConnection");
    
    if (string.IsNullOrEmpty(connectionString) || connectionString.Contains("your-server-name"))
    {
        context.Response.Redirect("/ServerConfig");
        return;
    }

    await next();
});

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=ServerConfig}/{action=Index}/{id?}");

// Map SignalR Hub
app.MapHub<DashboardHub>("/dashboardHub");

app.Run();
