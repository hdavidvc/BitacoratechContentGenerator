using BitacoraTech.Application.Common;
using BitacoraTech.Domain.Ai;
using BitacoraTech.Domain.Articles;
using BitacoraTech.Domain.Keywords;
using BitacoraTech.Domain.Sites;
using BitacoraTech.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace BitacoraTech.Infrastructure.Persistence;

public sealed class EfArticleRepository(BitacoraTechDbContext dbContext) : IArticleRepository
{
    public async Task<IReadOnlyCollection<Article>> ListAsync(CancellationToken cancellationToken)
    {
        return await dbContext.Articles.AsNoTracking().OrderByDescending(x => x.CreatedAt).ToListAsync(cancellationToken);
    }

    public Task<Article?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        return dbContext.Articles
            .Include(x => x.Approval)
            .Include(x => x.SeoAnalyses)
            .Include(x => x.Images)
            .Include(x => x.PublishingJobs)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task AddAsync(Article article, CancellationToken cancellationToken)
    {
        await dbContext.Articles.AddAsync(article, cancellationToken);
    }
}

public sealed class EfKeywordRepository(BitacoraTechDbContext dbContext) : IKeywordRepository
{
    public async Task<IReadOnlyCollection<Keyword>> ListAsync(CancellationToken cancellationToken)
    {
        return await dbContext.Keywords.AsNoTracking().OrderByDescending(x => x.OpportunityScore).ThenByDescending(x => x.SearchVolume).ToListAsync(cancellationToken);
    }

    public Task<Keyword?> GetBySiteAndTextAsync(Guid siteId, string text, CancellationToken cancellationToken)
    {
        var normalizedText = text.Trim();
        return dbContext.Keywords.FirstOrDefaultAsync(x => x.SiteId == siteId && x.Text == normalizedText, cancellationToken);
    }

    public async Task AddAsync(Keyword keyword, CancellationToken cancellationToken)
    {
        await dbContext.Keywords.AddAsync(keyword, cancellationToken);
    }
}

public sealed class EfKeywordResearchRunRepository(BitacoraTechDbContext dbContext) : IKeywordResearchRunRepository
{
    public async Task AddAsync(KeywordResearchRun researchRun, CancellationToken cancellationToken)
    {
        await dbContext.KeywordResearchRuns.AddAsync(researchRun, cancellationToken);
    }
}

public sealed class EfSiteRepository(BitacoraTechDbContext dbContext) : ISiteRepository
{
    public async Task<IReadOnlyCollection<Site>> ListAsync(CancellationToken cancellationToken)
    {
        return await dbContext.Sites.Include(x => x.WordPressConnection).AsNoTracking().OrderBy(x => x.Name).ToListAsync(cancellationToken);
    }

    public Task<Site?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        return dbContext.Sites.Include(x => x.WordPressConnection).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task AddAsync(Site site, CancellationToken cancellationToken)
    {
        await dbContext.Sites.AddAsync(site, cancellationToken);
    }
}

public sealed class EfUserRepository(BitacoraTechDbContext dbContext) : IUserRepository
{
    public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        return dbContext.Users.Include(x => x.Roles).FirstOrDefaultAsync(x => x.Email == normalizedEmail, cancellationToken);
    }

    public async Task AddAsync(User user, CancellationToken cancellationToken)
    {
        await dbContext.Users.AddAsync(user, cancellationToken);
    }

    public Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        return dbContext.Users.AnyAsync(x => x.Email == normalizedEmail, cancellationToken);
    }
}

public sealed class EfRoleRepository(BitacoraTechDbContext dbContext) : IRoleRepository
{
    public Task<Role?> GetByNameAsync(string name, CancellationToken cancellationToken)
    {
        var normalizedName = name.Trim();
        return dbContext.Roles.FirstOrDefaultAsync(x => x.Name == normalizedName, cancellationToken);
    }

    public async Task<Role> GetOrCreateAsync(string name, CancellationToken cancellationToken)
    {
        var normalizedName = name.Trim();
        var role = await dbContext.Roles.FirstOrDefaultAsync(x => x.Name == normalizedName, cancellationToken);
        if (role is not null)
        {
            return role;
        }

        role = new Role(Guid.NewGuid(), normalizedName);
        await dbContext.Roles.AddAsync(role, cancellationToken);
        return role;
    }
}

public sealed class EfAiUsageRepository(BitacoraTechDbContext dbContext) : IAiUsageRepository
{
    public async Task AddAsync(AiUsageRecord usageRecord, CancellationToken cancellationToken)
    {
        await dbContext.AiUsageRecords.AddAsync(usageRecord, cancellationToken);
    }
}

public sealed class EfUnitOfWork(BitacoraTechDbContext dbContext) : IUnitOfWork
{
    public Task SaveChangesAsync(CancellationToken cancellationToken) => dbContext.SaveChangesAsync(cancellationToken);
}
