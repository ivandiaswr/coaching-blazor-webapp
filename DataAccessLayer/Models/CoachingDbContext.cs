using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using ModelLayer.Models;

namespace DataAccessLayer {
    public class CoachingDbContext : IdentityDbContext<ApplicationUser>
    {
        public DbSet<EmailSubscription> EmailSubscriptions { get; set; }
        public DbSet<Session> Sessions { get; set; }
        public DbSet<UnavailableTime> UnavailableTimes { get; set; }
        public DbSet<VideoSession> VideoSessions { get; set; }
        public DbSet<SessionPrice> SessionPrices { get; set; }
        public DbSet<SessionPackPrice> SessionPackPrices { get; set; }
        public DbSet<SubscriptionPrice> SubscriptionPrices { get; set; }
        public DbSet<SessionPack> SessionPacks { get; set; }
        public DbSet<UserSubscription> UserSubscriptions { get; set; }
        public DbSet<Log> Logs { get; set; }
        
        public CoachingDbContext(DbContextOptions<CoachingDbContext> options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Session>()
                .HasOne(s => s.VideoSession)
                .WithOne(v => v.Session)
                .HasForeignKey<VideoSession>(v => v.SessionRefId);
        }
    }
}