using GpaCalculator.Models.Db;
using Microsoft.EntityFrameworkCore;

namespace GpaCalculator.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<SyllabusTemplate> SyllabusTemplates { get; set; }
    public DbSet<GradebookSession> GradebookSessions { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<GradebookSession>()
            .HasIndex(s => s.SessionId)
            .HasDatabaseName("idx_session");
    }
}
