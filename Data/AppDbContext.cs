using AIMLAPP.Configuration;
using AIMLAPP.Models;
using Microsoft.EntityFrameworkCore;

namespace AIMLAPP.Data;

public class AppDbContext : DbContext
{
    public static string ConnectionString => AppSettings.Current.RequireConnectionString();

    public DbSet<Experience> Experiences => Set<Experience>();

    // Used by direct 'new AppDbContext()' in the console flows.
    public AppDbContext() { }

    // Used by DI (AddDbContext) - e.g. by the MCP server host.
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            optionsBuilder.UseSqlServer(ConnectionString);
        }
    }

    // Must match Experiences.ExpVector VECTOR(N) in SQL Server and the
    // output size of OpenAI:EmbeddingModel.
    public static int VectorDimensions => AppSettings.Current.OpenAI.EmbeddingDimensions;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Experience>()
            .Property(e => e.ExpVector)
            .HasColumnType($"vector({VectorDimensions})");

        base.OnModelCreating(modelBuilder);
    }
}
