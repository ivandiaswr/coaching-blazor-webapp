using BusinessLayer;
using BusinessLayer.Services;
using BusinessLayer.Services.Interfaces;
using DataAccessLayer;
using Google.Apis.Auth.AspNetCore3;
using coachingWebapp.Components;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MudBlazor.Services;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetry.Logs;

var builder = WebApplication.CreateBuilder(args);

SQLitePCL.Batteries.Init();
SQLitePCL.raw.SetProvider(new SQLitePCL.SQLite3Provider_e_sqlite3());  

string databasePath;
if (builder.Environment.IsDevelopment())
{
    builder.Configuration.AddUserSecrets<Program>();

    // Local development path
    string solutionDirectory = AppContext.BaseDirectory;
    string projectRootDirectory = Path.GetFullPath(Path.Combine(solutionDirectory, "../../../../"));
    string databaseDirectory = Path.Combine(projectRootDirectory, "Database");
    Directory.CreateDirectory(databaseDirectory);
    databasePath = Path.Combine(databaseDirectory, "coaching.db");
}
else
{
    // Production path for SmarterASP.NET
    string dbFolder = Path.Combine(AppContext.BaseDirectory, "../db"); 
    Directory.CreateDirectory(dbFolder); // Ensure folder exists
    databasePath = Path.Combine(dbFolder, "coaching.db");
}

// builder.Configuration.GetConnectionString("DefaultConnection")
builder.Services.AddDbContext<CoachingDbContext>(options =>
    options.UseSqlite($"Data Source={databasePath}",
        b => b.MigrationsAssembly(typeof(CoachingDbContext).Assembly.FullName)));
// .LogTo(message =>
//            {
//                if (!message.Contains("An 'IServiceProvider' was created for internal use by Entity Framework."))
//                {
//                    Console.WriteLine(message);
//                }
//            }, LogLevel.Information));
    // .EnableSensitiveDataLogging() // Only use in development
    // .EnableDetailedErrors());

builder.Services.AddIdentity<IdentityUser, IdentityRole>(options =>
    {
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequiredLength = 12;
    })
    .AddEntityFrameworkStores<CoachingDbContext>() // Links Identity to context for storing users and roles in the database
    .AddDefaultTokenProviders(); // Enables features like email confirmation, password reset, and two-factor authentication.

// Defines policy for Admin role to restrict access to specific parts of the application
builder.Services.AddAuthorization(options =>
    {
        options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
    }
);

builder.Services.AddScoped<IEmailSubscriptionService, EmailSubscriptionService>();
builder.Services.AddScoped<IContactService, ContactService>();
builder.Services.AddScoped<IScrollService, ScrollService>();
builder.Services.AddScoped<IGoogleService, GoogleService>();
builder.Services.AddScoped<ISecurityService, SecurityService>();
builder.Services.AddScoped<IUserRefreshTokenService, UserRefreshTokenService>();
builder.Services.AddScoped<IHelperService, HelperService>();

builder.Services.AddScoped<ILogService, LogService>();
builder.Services.AddSingleton<LogProcessor>();

builder.Services.AddHttpClient<GoogleService>();

builder.Services.AddControllersWithViews().AddRazorRuntimeCompilation(); 

// Add services to the container
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
// builder.Services.AddAuthentication(options =>
// {
//     options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
//     options.DefaultChallengeScheme = GoogleOpenIdConnectDefaults.AuthenticationScheme;
// })
// .AddCookie()
// .AddGoogleOpenIdConnect(options =>
// {
//     options.ClientId = builder.Configuration["GoogleCalendar:ClientId"];
//     options.ClientSecret = builder.Configuration["GoogleCalendar:ClientSecret"];
//     options.Scope.Add(Google.Apis.Calendar.v3.CalendarService.Scope.Calendar);
//     options.SaveTokens = true;
// });

builder.Services.AddHttpClient();
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
})
.AddCookie(options =>
{
    options.Cookie.Name = ".AspNetCore.Identity.Application";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.LoginPath = "/login";
    options.LogoutPath = "/logout";
    options.AccessDeniedPath = "/access-denied";
});

// Open Telemetry
builder.Services.AddOpenTelemetry()
    .WithTracing(tracerProviderBuilder =>
    {
        tracerProviderBuilder
            .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("CoachingApp"))
            .AddAspNetCoreInstrumentation()
            .AddConsoleExporter();
    })
    .WithMetrics(metricsProviderBuilder =>
    {
        metricsProviderBuilder
            .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("CoachingApp"))
            .AddAspNetCoreInstrumentation()
            .AddMeter("CoachingMetrics")
            .AddConsoleExporter();
    });;

// OpenTelemetry Logging
builder.Logging
    .SetMinimumLevel(LogLevel.Error)
    .AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Error)
    .AddOpenTelemetry(options =>
    {
        options.IncludeFormattedMessage = true;
        options.AddProcessor(provider => provider.GetRequiredService<LogProcessor>());
        options.AddConsoleExporter();
    })
    .AddConsole();

// Mudblazor framework
builder.Services.AddMudServices();

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

// When the user logins it created a cookie that UseAuthentication uses to validate
app.UseAuthentication(); // Verifies user credentials and sets the users identity for the current request
app.UseAuthorization(); // Checks the users permissions to give access to resources

app.MapControllers(); // Use controllers for the api

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Ensure the database is created
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<CoachingDbContext>();
    dbContext.Database.EnsureCreated();
}

app.Run();
