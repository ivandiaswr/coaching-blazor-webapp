using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using ModelLayer.Models;

namespace DataAccessLayer;

public class CoachingDbContext : IdentityDbContext<IdentityUser>
{
    public DbSet<EmailSubscription> EmailSubscriptions { get; set; }
    public DbSet<Session> Sessions { get; set; }
    public DbSet<UnavailableTime> UnavailableTimes { get; set; }
    public DbSet<Log> Logs { get; set; }
    
    public CoachingDbContext(DbContextOptions<CoachingDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
    }
}
