using BitacoraTech.Domain.Ai;
using BitacoraTech.Domain.Articles;
using BitacoraTech.Domain.Common;
using BitacoraTech.Domain.Keywords;
using BitacoraTech.Domain.Sites;
using BitacoraTech.Domain.Tenancy;
using BitacoraTech.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace BitacoraTech.Infrastructure.Persistence;

public sealed class BitacoraTechDbContext(DbContextOptions<BitacoraTechDbContext> options) : DbContext(options)
{
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Site> Sites => Set<Site>();
    public DbSet<WordPressConnection> WordPressConnections => Set<WordPressConnection>();
    public DbSet<KeywordResearchRun> KeywordResearchRuns => Set<KeywordResearchRun>();
    public DbSet<Keyword> Keywords => Set<Keyword>();
    public DbSet<Article> Articles => Set<Article>();
    public DbSet<ArticleSeoAnalysis> ArticleSeoAnalyses => Set<ArticleSeoAnalysis>();
    public DbSet<ArticleImage> ArticleImages => Set<ArticleImage>();
    public DbSet<ArticleApproval> ArticleApprovals => Set<ArticleApproval>();
    public DbSet<PublishingJob> PublishingJobs => Set<PublishingJob>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<AiProvider> AiProviders => Set<AiProvider>();
    public DbSet<AiUsageRecord> AiUsageRecords => Set<AiUsageRecord>();
    public DbSet<PromptTemplate> PromptTemplates => Set<PromptTemplate>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ConfigureTenancy(modelBuilder);
        ConfigureUsers(modelBuilder);
        ConfigureSites(modelBuilder);
        ConfigureKeywords(modelBuilder);
        ConfigureArticles(modelBuilder);
        ConfigureAi(modelBuilder);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries<Entity>())
        {
            if (entry.State == EntityState.Modified)
            {
                entry.Property(nameof(Entity.UpdatedAt)).CurrentValue = DateTimeOffset.UtcNow;
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }

    private static void ConfigureTenancy(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Tenant>(entity =>
        {
            entity.ToTable("Tenants");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(160).IsRequired();
            entity.HasIndex(x => x.Name).IsUnique();
        });
    }

    private static void ConfigureUsers(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("Users");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Email).HasMaxLength(320).IsRequired();
            entity.Property(x => x.PasswordHash).HasMaxLength(512).IsRequired();
            entity.HasIndex(x => new { x.TenantId, x.Email }).IsUnique();
            entity.HasMany(x => x.Roles).WithMany().UsingEntity("UserRoles");
            entity.Navigation(x => x.Roles).UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.ToTable("Roles");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(80).IsRequired();
            entity.HasIndex(x => x.Name).IsUnique();
        });

        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.ToTable("RefreshTokens");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.TokenHash).HasMaxLength(128).IsRequired();
            entity.Property(x => x.ReplacedByTokenHash).HasMaxLength(128);
            entity.HasIndex(x => x.TokenHash).IsUnique();
            entity.HasIndex(x => new { x.UserId, x.ExpiresAt });
            entity.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigureSites(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Site>(entity =>
        {
            entity.ToTable("Sites");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(180).IsRequired();
            entity.Property(x => x.BaseUrl).HasConversion(v => v.ToString(), v => new Uri(v)).HasMaxLength(500).IsRequired();
            entity.HasIndex(x => new { x.TenantId, x.Name }).IsUnique();
            entity.HasOne(x => x.WordPressConnection).WithOne().HasForeignKey<WordPressConnection>(x => x.SiteId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<WordPressConnection>(entity =>
        {
            entity.ToTable("WordPressConnections");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Username).HasMaxLength(160).IsRequired();
            entity.Property(x => x.EncryptedApplicationPassword).HasMaxLength(1024).IsRequired();
            entity.HasIndex(x => x.SiteId).IsUnique();
        });
    }

    private static void ConfigureKeywords(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<KeywordResearchRun>(entity =>
        {
            entity.ToTable("KeywordResearchRuns");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Seed).HasMaxLength(300).IsRequired();
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(40).IsRequired();
            entity.HasMany(x => x.Keywords).WithOne().HasForeignKey(x => x.ResearchRunId).OnDelete(DeleteBehavior.SetNull);
            entity.Navigation(x => x.Keywords).UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<Keyword>(entity =>
        {
            entity.ToTable("Keywords");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Text).HasMaxLength(300).IsRequired();
            entity.Property(x => x.Difficulty).HasPrecision(5, 2);
            entity.Property(x => x.TrendScore).HasPrecision(5, 2);
            entity.Property(x => x.OpportunityScore).HasPrecision(5, 2);
            entity.Property(x => x.Source).HasMaxLength(80).IsRequired();
            entity.Property(x => x.Cluster).HasMaxLength(120).IsRequired();
            entity.Property(x => x.Intent).HasMaxLength(80).IsRequired();
            entity.HasIndex(x => new { x.SiteId, x.Text }).IsUnique();
            entity.HasIndex(x => new { x.SiteId, x.OpportunityScore });
            entity.HasIndex(x => new { x.SiteId, x.Cluster });
        });
    }

    private static void ConfigureArticles(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Article>(entity =>
        {
            entity.ToTable("Articles");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Title).HasMaxLength(300).IsRequired();
            entity.Property(x => x.HtmlContent).IsRequired();
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(80).IsRequired();
            entity.Property(x => x.WordPressPostId).HasMaxLength(120);
            entity.HasIndex(x => new { x.SiteId, x.Status });
            entity.HasMany(x => x.SeoAnalyses).WithOne().HasForeignKey(x => x.ArticleId).OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(x => x.Images).WithOne().HasForeignKey(x => x.ArticleId).OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(x => x.PublishingJobs).WithOne().HasForeignKey(x => x.ArticleId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Approval).WithOne().HasForeignKey<ArticleApproval>(x => x.ArticleId).OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(x => x.Categories).WithMany().UsingEntity("ArticleCategories");
            entity.HasMany(x => x.Tags).WithMany().UsingEntity("ArticleTags");
            entity.Navigation(x => x.SeoAnalyses).UsePropertyAccessMode(PropertyAccessMode.Field);
            entity.Navigation(x => x.Images).UsePropertyAccessMode(PropertyAccessMode.Field);
            entity.Navigation(x => x.PublishingJobs).UsePropertyAccessMode(PropertyAccessMode.Field);
            entity.Navigation(x => x.Categories).UsePropertyAccessMode(PropertyAccessMode.Field);
            entity.Navigation(x => x.Tags).UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<ArticleSeoAnalysis>(entity =>
        {
            entity.ToTable("ArticleSeoAnalyses");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Summary).HasMaxLength(2000).IsRequired();
        });

        modelBuilder.Entity<ArticleImage>(entity =>
        {
            entity.ToTable("ArticleImages");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Url).HasMaxLength(1000).IsRequired();
            entity.Property(x => x.AltText).HasMaxLength(300).IsRequired();
        });

        modelBuilder.Entity<ArticleApproval>(entity =>
        {
            entity.ToTable("ArticleApprovals");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.ArticleId).IsUnique();
        });

        modelBuilder.Entity<PublishingJob>(entity =>
        {
            entity.ToTable("PublishingJobs");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(80).IsRequired();
        });

        modelBuilder.Entity<Category>(entity =>
        {
            entity.ToTable("Categories");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(180).IsRequired();
            entity.Property(x => x.WordPressId).HasMaxLength(120);
        });

        modelBuilder.Entity<Tag>(entity =>
        {
            entity.ToTable("Tags");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(180).IsRequired();
            entity.Property(x => x.WordPressId).HasMaxLength(120);
        });
    }

    private static void ConfigureAi(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AiProvider>(entity =>
        {
            entity.ToTable("AiProviders");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(80).IsRequired();
            entity.HasIndex(x => x.Name).IsUnique();
        });

        modelBuilder.Entity<AiUsageRecord>(entity =>
        {
            entity.ToTable("AiUsageRecords");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Provider).HasMaxLength(80).IsRequired();
            entity.Property(x => x.Model).HasMaxLength(120).IsRequired();
            entity.Property(x => x.EstimatedCost).HasPrecision(18, 6);
            entity.Property(x => x.FeatureArea).HasMaxLength(120).IsRequired();
        });

        modelBuilder.Entity<PromptTemplate>(entity =>
        {
            entity.ToTable("PromptTemplates");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Key).HasMaxLength(160).IsRequired();
            entity.Property(x => x.Template).IsRequired();
            entity.HasIndex(x => new { x.TenantId, x.Key }).IsUnique();
        });
    }
}
