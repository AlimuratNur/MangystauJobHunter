using MangystauJobHuntPlatform.Models;
using Microsoft.EntityFrameworkCore ;

namespace MangystauJobHuntPlatform.DB;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Users> Users { get; set; }
    public DbSet<Vacancy> Vacancies { get; set; }
    public DbSet<Application> Applications { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Users>().HasIndex(u => u.TelegramId).IsUnique();
    }
}
