using BusinessLayer;
using BusinessLayer.Services;
using BusinessLayer.Services.Interfaces;
using coachingWebapp.Components;
using DataAccessLayer;
using Google.Apis.Auth.AspNetCore3;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

if (builder.Environment.IsDevelopment())
{
    builder.Configuration.AddUserSecrets<Program>();
}

SQLitePCL.Batteries.Init();
SQLitePCL.raw.SetProvider(new SQLitePCL.SQLite3Provider_e_sqlite3());  

string solutionDirectory = AppContext.BaseDirectory;
string projectRootDirectory = Path.GetFullPath(Path.Combine(solutionDirectory, "../../../../"));
string databaseDirectory = Path.Combine(projectRootDirectory, "Database");

// Ensure the Database directory exists
Directory.CreateDirectory(databaseDirectory);

string databasePath = Path.Combine(databaseDirectory, "coaching.db");

// builder.Configuration.GetConnectionString("DefaultConnection")
builder.Services.AddDbContext<CoachingDbContext>(options =>
    options.UseSqlite($"Data Source={databasePath}",
        b => b.MigrationsAssembly(typeof(CoachingDbContext).Assembly.FullName))
    .LogTo(Console.WriteLine, LogLevel.Information)
    .EnableSensitiveDataLogging()
    .EnableDetailedErrors());

builder.Services.AddControllersWithViews().AddRazorRuntimeCompilation();

builder.Services.AddScoped<IEmailSubscriptionService, EmailSubscriptionService>();
builder.Services.AddScoped<IContactService, ContactService>();
builder.Services.AddScoped<IScrollService, ScrollService>();
builder.Services.AddScoped<IGoogleService, GoogleService>();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Add logging
builder.Services.AddLogging(loggingBuilder =>
{
    loggingBuilder.AddConfiguration(builder.Configuration.GetSection("Logging"));
    loggingBuilder.AddConsole();
    loggingBuilder.AddDebug();
});

// Google

builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = GoogleOpenIdConnectDefaults.AuthenticationScheme;
})
.AddCookie()
.AddGoogleOpenIdConnect(options =>
{
    options.ClientId = builder.Configuration["GoogleCalendar:ClientId"];
    options.ClientSecret = builder.Configuration["GoogleCalendar:ClientSecret"];
    options.Scope.Add(Google.Apis.Calendar.v3.CalendarService.Scope.Calendar);
    options.SaveTokens = true;
});

builder.Services.AddHttpClient<GoogleService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();

    app.UseHttpsRedirection();
}

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Ensure the database is created
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<CoachingDbContext>();
    dbContext.Database.EnsureCreated();
}

app.Run();
