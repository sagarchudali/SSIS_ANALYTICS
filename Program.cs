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
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Strict;
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// Enable HTTPS redirection
app.UseHttpsRedirection();

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
