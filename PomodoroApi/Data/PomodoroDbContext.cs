using Microsoft.EntityFrameworkCore;
using PomodoroApi.Models;

namespace PomodoroApi.Data
{
    public class PomodoroDbContext : DbContext
    {
        public PomodoroDbContext(DbContextOptions<PomodoroDbContext> options) : base(options) { }

        public DbSet<User> Users { get; set; }
        public DbSet<PomodoroSession> PomodoroSessions { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // User için primary key tanımlama
            modelBuilder.Entity<User>()
                .HasKey(u => u.Id);

            // PomodoroSession için foreign key ilişkisi 
            modelBuilder.Entity<PomodoroSession>()
                .HasOne<User>()
                .WithMany(u => u.Sessions)
                .HasForeignKey(p => p.UserId);

            // Seed data ekleme - defaultUser oluşturma
            modelBuilder.Entity<User>().HasData(
                new User
                {
                    Id = "defaultUser",
                    Username = "Default User",
                    Email = "default@example.com"
                }
            );

            base.OnModelCreating(modelBuilder);
        }
    }
}