using BusinessLayer;
using BusinessLayer.Services;
using BusinessLayer.Services.Interfaces;
using coachingWebapp.Components;
using DataAccessLayer;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

if (builder.Environment.IsDevelopment())
{
    builder.Configuration.AddUserSecrets<Program>();
}

SQLitePCL.Batteries.Init();
SQLitePCL.raw.SetProvider(new SQLitePCL.SQLite3Provider_e_sqlite3());  

// Get the solution directory
string solutionDirectory = AppDomain.CurrentDomain.BaseDirectory;
for (int i = 0; i < 3; i++)
{
    solutionDirectory = Directory.GetParent(solutionDirectory)?.FullName ?? solutionDirectory;
}
string databaseDirectory = Path.Combine(solutionDirectory, "Database");

// Ensure the Database directory exists
Directory.CreateDirectory(databaseDirectory);

// builder.Configuration.GetConnectionString("DefaultConnection")
builder.Services.AddDbContext<CoachingDbContext>(options =>
    options.UseSqlite($"Data Source={Path.Combine(databaseDirectory, "coaching.db")}",
        b => b.MigrationsAssembly("DataAccessLayer"))
    .LogTo(Console.WriteLine, LogLevel.Information)
    .EnableSensitiveDataLogging()
    .EnableDetailedErrors());

builder.Services.AddControllersWithViews().AddRazorRuntimeCompilation();

builder.Services.AddScoped<IEmailSubscriptionService, EmailSubscriptionService>();
builder.Services.AddScoped<IContactService, ContactService>();

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
