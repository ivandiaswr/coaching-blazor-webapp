using BusinessLayer;
using BusinessLayer.Services;
using BusinessLayer.Services.Interfaces;
using DataAccessLayer;
using coachingWebapp.Components;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MudBlazor.Services;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetry.Logs;
using System.Globalization;
using MudBlazor;
using ModelLayer.Models;
using Microsoft.AspNetCore.Diagnostics;
using Newtonsoft.Json;

var builder = WebApplication.CreateBuilder(args);

CultureInfo.DefaultThreadCurrentCulture = new CultureInfo("en-GB");
CultureInfo.DefaultThreadCurrentUICulture = new CultureInfo("en-GB");

builder.Services.AddSignalR(options =>
{
    options.MaximumReceiveMessageSize = 10 * 1024 * 1024; // 10MB
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
    options.MaximumParallelInvocationsPerClient = 1; // Reduce memory per client
});

// Reduce logging in production
if (builder.Environment.IsDevelopment())
{
    builder.Logging.SetMinimumLevel(LogLevel.Debug);
}
else
{
    builder.Logging.SetMinimumLevel(LogLevel.Warning);
    builder.Logging.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Error);
    builder.Logging.AddFilter("Microsoft.AspNetCore", LogLevel.Warning);
}

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
{
    options.UseSqlite($"Data Source={databasePath}",
        b => b.MigrationsAssembly(typeof(CoachingDbContext).Assembly.FullName));
    options.EnableServiceProviderCaching(false); // Reduces provider cache
    options.EnableSensitiveDataLogging(false);   // Remove unnecessary logging
    options.ConfigureWarnings(warnings => warnings.Ignore(
        Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.MultipleCollectionIncludeWarning,
        Microsoft.EntityFrameworkCore.Diagnostics.CoreEventId.FirstWithoutOrderByAndFilterWarning));
    options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking); // Reduce query tracking and memory overhead

    if (!builder.Environment.IsDevelopment())
    {
        options.EnableDetailedErrors(false); // Disable detailed errors in production to save memory
    }
});
// .LogTo(message =>
//            {
//                if (!message.Contains("An 'IServiceProvider' was created for internal use by Entity Framework."))
//                {
//                    Console.WriteLine(message);
//                }
//            }, LogLevel.Information));
// .EnableSensitiveDataLogging() // Only use in development
// .EnableDetailedErrors());

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequiredLength = 12;
    })
    .AddEntityFrameworkStores<CoachingDbContext>() // Links Identity to context for storing users and roles in the database
    .AddDefaultTokenProviders(); // Enables features like email confirmation, password reset, and two-factor authentication

// Defines policy for Admin role to restrict access to specific parts of the application
// builder.Services.AddAuthorization(options =>
//     {
//         options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
//     }
// );

builder.Services.AddScoped<IEmailSubscriptionService, EmailSubscriptionService>();
builder.Services.AddScoped<ISessionService, SessionService>();
builder.Services.AddScoped<IUnavailableTimeService, UnavailableTimeService>();
builder.Services.AddScoped<IVideoCallService, VideoCallService>();
builder.Services.AddScoped<ISecurityService, SecurityService>();
builder.Services.AddScoped<IHelperService, HelperService>();
builder.Services.AddScoped<IChatService, OpenRouterChatService>();
builder.Services.AddScoped<IPaymentService, StripeService>();
builder.Services.AddScoped<ISessionPriceService, SessionPriceService>();
builder.Services.AddScoped<ISessionPackPriceService, SessionPackPriceService>();
builder.Services.AddScoped<ISubscriptionPriceService, SubscriptionPriceService>();
builder.Services.AddScoped<ISessionPackService, SessionPackService>();
builder.Services.AddScoped<IUserSubscriptionService, UserSubscriptionService>();
builder.Services.AddScoped<ICurrencyConversionService, CurrencyConversionService>();
builder.Services.AddScoped<ISitemapService, SitemapService>();

builder.Services.AddHttpClient<GoogleReviewsService>();

builder.Services.AddScoped<ILogService>(provider =>
{
    var context = provider.GetRequiredService<CoachingDbContext>();
    return new LogService(context);
});

builder.Services.AddSingleton<LogProcessor>();

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
// builder.Services.AddOpenTelemetry()
//     .WithTracing(tracerProviderBuilder =>
//     {
//         tracerProviderBuilder
//             .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("CoachingApp"))
//             .AddAspNetCoreInstrumentation()
//             .AddConsoleExporter();
//     })
//     .WithMetrics(metricsProviderBuilder =>
//     {
//         metricsProviderBuilder
//             .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("CoachingApp"))
//             .AddAspNetCoreInstrumentation()
//             .AddMeter("CoachingMetrics")
//             .AddConsoleExporter();
//     });;

// OpenTelemetry Logging
// builder.Logging
//     .SetMinimumLevel(LogLevel.Error)
//     .AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Error)
//     .AddOpenTelemetry(options =>
//     {
//         options.IncludeFormattedMessage = true;
//         options.AddProcessor(provider => provider.GetRequiredService<LogProcessor>());
//         options.AddConsoleExporter();
//     })
//     .AddConsole();

// Mudblazor framework
builder.Services.AddMudServices(config =>
{
    config.SnackbarConfiguration.PositionClass = Defaults.Classes.Position.BottomRight;
    config.SnackbarConfiguration.MaxDisplayedSnackbars = 3;
    config.SnackbarConfiguration.ShowCloseIcon = true;
    config.SnackbarConfiguration.SnackbarVariant = Variant.Filled;
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();

    app.UseHttpsRedirection();
}

app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        var exceptionHandlerPathFeature = context.Features.Get<IExceptionHandlerPathFeature>();
        var exception = exceptionHandlerPathFeature.Error;

        if (context.Request.Path.Value.StartsWith("/api") || context.Request.Headers["Accept"].Contains("application/json"))
        {
            context.Response.ContentType = "application/json";
            context.Response.StatusCode = 500;
            await context.Response.WriteAsync(JsonConvert.SerializeObject(new { error = exception.Message }));
        }
        else
        {
            context.Response.Redirect("/Error");
        }
    });
});

app.UseWebSockets();

app.UseStaticFiles();
app.UseAntiforgery();

// When the user logins it created a cookie that UseAuthentication uses to validate
app.UseAuthentication(); // Verifies user credentials and sets the users identity for the current request
app.UseAuthorization(); // Checks the users permissions to give access to resources

app.MapControllers(); // Use controllers for the api

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Ensure the database is created anad roles exist
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<CoachingDbContext>();
    dbContext.Database.EnsureCreated();

    var roleMananger = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    string[] roles = { "Admin", "User" };

    foreach (var role in roles)
    {
        if (!await roleMananger.RoleExistsAsync(role))
        {
            await roleMananger.CreateAsync(new IdentityRole(role));
        }
    }
}

app.MapHub<VideoCallHub>("/videoHub");

app.Run();
