using Microsoft.EntityFrameworkCore;
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

        modelBuilder.Entity<EmailSubscription>(entity =>
        {
            entity.ToTable("EmailSubscriptions");
            entity.Property(e => e.SubscribedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            entity.Property(e => e.IsSubscribed)
                .HasDefaultValue(true);
        });

        modelBuilder.Entity<Contact>(entity =>
        {
            entity.ToTable("Contacts");
            entity.Property(e => e.TimeStampInserted)
                .HasDefaultValueSql("CURRENT_TIMESTAMP");
        });
    }
}
