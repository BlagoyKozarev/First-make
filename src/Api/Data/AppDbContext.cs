using Microsoft.EntityFrameworkCore;

namespace Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<CachedMatch> CachedMatches => Set<CachedMatch>();
    public DbSet<UserApproval> UserApprovals => Set<UserApproval>();
    public DbSet<SessionData> SessionData => Set<SessionData>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // CachedMatch indexes
        modelBuilder.Entity<CachedMatch>()
            .HasIndex(m => new { m.BoqItemName, m.Unit })
            .HasDatabaseName("IX_CachedMatch_BoqItemName_Unit");

        modelBuilder.Entity<CachedMatch>()
            .HasIndex(m => m.CreatedAt)
            .HasDatabaseName("IX_CachedMatch_CreatedAt");

        // UserApproval indexes
        modelBuilder.Entity<UserApproval>()
            .HasIndex(a => a.SessionId)
            .HasDatabaseName("IX_UserApproval_SessionId");

        modelBuilder.Entity<UserApproval>()
            .HasIndex(a => a.BoqItemId)
            .HasDatabaseName("IX_UserApproval_BoqItemId");

        // SessionData indexes
        modelBuilder.Entity<SessionData>()
            .HasIndex(s => s.CreatedAt)
            .HasDatabaseName("IX_SessionData_CreatedAt");
    }
}

// Cached fuzzy match results
public class CachedMatch
{
    public int Id { get; set; }
    public required string BoqItemName { get; set; }
    public required string Unit { get; set; }
    public required string CandidatesJson { get; set; } // JSON array of MatchedItem candidates
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ExpiresAt { get; set; }
}

// User approval decisions
public class UserApproval
{
    public int Id { get; set; }
    public required string SessionId { get; set; }
    public required string BoqItemId { get; set; }
    public required string BoqItemName { get; set; }
    public required string SelectedCatalogueItemId { get; set; }
    public required string SelectedCatalogueItemName { get; set; }
    public decimal BasePrice { get; set; }
    public required string Unit { get; set; }
    public double MatchScore { get; set; }
    public DateTime ApprovedAt { get; set; } = DateTime.UtcNow;
}

// Session metadata
public class SessionData
{
    public int Id { get; set; }
    public required string SessionId { get; set; }
    public required string SourceFileName { get; set; }
    public required string BoqDataJson { get; set; } // Serialized BoqData
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
}
