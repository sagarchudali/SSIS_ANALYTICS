using SSISAnalyticsDashboard.Services;
using SSISAnalyticsDashboard.Hubs;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Add HttpContextAccessor for session access in services
builder.Services.AddHttpContextAccessor();

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
if (app.Environment.IsDevelopment())
{
    // Explicitly disable browser refresh/hot reload
    app.Use(async (context, next) =>
    {
        // Block requests to browser refresh endpoint
        if (context.Request.Path.StartsWithSegments("/_framework/aspnetcore-browser-refresh.js"))
        {
            context.Response.StatusCode = 404;
            return;
        }
        await next();
    });
}
else
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseStaticFiles();

app.UseRouting();

app.UseSession();  // Add session middleware

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=ServerConfig}/{action=Index}/{id?}");

// Map SignalR Hub
app.MapHub<DashboardHub>("/dashboardHub");

app.Run();
