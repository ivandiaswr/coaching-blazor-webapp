using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ModelLayer.Models;

namespace DataAccessLayer;

public class CoachingDbContext : DbContext
{
    public DbSet<EmailSubscription> EmailSubscriptions { get; set; }
    public DbSet<Contact> Contacts { get; set; }
    public CoachingDbContext(DbContextOptions<CoachingDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
    }
}
