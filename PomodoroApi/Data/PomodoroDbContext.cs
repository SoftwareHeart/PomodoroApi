using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PomodoroApi.Models;

namespace PomodoroApi.Data
{
    public class PomodoroDbContext : IdentityDbContext<ApplicationUser>
    {
        public PomodoroDbContext(DbContextOptions<PomodoroDbContext> options) : base(options) { }

        public DbSet<PomodoroSession> PomodoroSessions { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder); // Identity tabloları için gerekli

            // PomodoroSession için primary key tanımlama
            modelBuilder.Entity<PomodoroSession>()
                .HasKey(p => p.Id);

            // PomodoroSession için foreign key ilişkisi 
            modelBuilder.Entity<PomodoroSession>()
                .HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(p => p.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}