using BitacoraTech.Contracts.Articles;
using BitacoraTech.Contracts.Auth;
using BitacoraTech.Contracts.Common;
using BitacoraTech.Contracts.Keywords;
using BitacoraTech.Contracts.Seo;
using BitacoraTech.Contracts.Sites;
using BitacoraTech.Domain.Articles;
using BitacoraTech.Domain.Ai;
using BitacoraTech.Domain.Keywords;
using BitacoraTech.Domain.Sites;
using BitacoraTech.Domain.Users;

namespace BitacoraTech.Application.Common;

public interface ICurrentUserContext
{
    Guid? UserId { get; }
    Guid? TenantId { get; }
    bool IsAuthenticated { get; }
}

public interface ITenantContext
{
    Guid TenantId { get; }
}

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken);
    Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken);
    Task<AuthResponse> RefreshAsync(RefreshTokenRequest request, CancellationToken cancellationToken);
    Task LogoutAsync(LogoutRequest request, CancellationToken cancellationToken);
}

public interface IKeywordResearchService
{
    Task<IReadOnlyCollection<KeywordResponse>> GetKeywordsAsync(CancellationToken cancellationToken);
    Task<IReadOnlyCollection<KeywordClusterResponse>> GetClustersAsync(CancellationToken cancellationToken);
    Task<JobResponse> ResearchAsync(ResearchKeywordRequest request, CancellationToken cancellationToken);
}

public sealed record KeywordResearchContext(
    Guid TenantId,
    Guid SiteId,
    string SeedKeyword,
    string Country,
    string Language,
    IReadOnlyCollection<string> Subreddits,
    IReadOnlyCollection<string> RssFeeds);

public sealed record TrendKeywordCandidate(
    string Text,
    string Source,
    int Mentions,
    decimal SourceScore,
    DateTimeOffset SeenAt);

public sealed record ScoredKeywordCandidate(
    string Text,
    string Source,
    int SearchVolume,
    decimal Difficulty,
    decimal TrendScore,
    decimal OpportunityScore,
    int Mentions,
    string Cluster,
    string Intent,
    DateTimeOffset SeenAt);

public sealed record KeywordResearchResult(Guid ResearchRunId, IReadOnlyCollection<ScoredKeywordCandidate> Keywords);

public interface IKeywordTrendSource
{
    string Name { get; }
    Task<IReadOnlyCollection<TrendKeywordCandidate>> GetCandidatesAsync(KeywordResearchContext context, CancellationToken cancellationToken);
}

public interface IKeywordResearchRunner
{
    Task<KeywordResearchResult> RunAsync(KeywordResearchContext context, CancellationToken cancellationToken);
}

public interface IArticleGenerationService
{
    Task<IReadOnlyCollection<ArticleSummaryResponse>> GetArticlesAsync(CancellationToken cancellationToken);
    Task<ArticleDetailResponse?> GetArticleAsync(Guid articleId, CancellationToken cancellationToken);
    Task<JobResponse> GenerateAsync(GenerateArticleRequest request, CancellationToken cancellationToken);
}

public interface ISeoAnalysisService
{
    Task<SeoAnalyzeResponse> AnalyzeAsync(SeoAnalyzeRequest request, CancellationToken cancellationToken);
    Task<JobResponse> AnalyzeArticleAsync(Guid articleId, CancellationToken cancellationToken);
}

public interface IAiGateway
{
    Task<string> CompleteAsync(string featureArea, string prompt, CancellationToken cancellationToken);
}

public sealed record AiCompletionRequest(
    string FeatureArea,
    string Prompt,
    Guid? RelatedEntityId = null,
    int? MaxTokens = null,
    decimal Temperature = 0.7m);

public sealed record AiCompletionResult(
    string Provider,
    string Model,
    string Text,
    int PromptTokens,
    int CompletionTokens,
    decimal EstimatedCost);

public interface IAiProvider
{
    string Name { get; }
    bool IsEnabled { get; }
    int Priority { get; }
    Task<AiCompletionResult> CompleteAsync(AiCompletionRequest request, CancellationToken cancellationToken);
}

public interface IAiCostTracker
{
    Task TrackAsync(string provider, string model, int promptTokens, int completionTokens, decimal estimatedCost, string featureArea, Guid? relatedEntityId, CancellationToken cancellationToken);
}

public interface IPromptTemplateService
{
    Task<string> RenderAsync(string key, IReadOnlyDictionary<string, string> values, CancellationToken cancellationToken);
}

public interface IWordPressPublishingService
{
    Task<string> PublishAsync(Article article, Site site, CancellationToken cancellationToken);
}

public interface IMediaService
{
    Task<string> UploadArticleImageAsync(Guid articleId, Stream content, string fileName, CancellationToken cancellationToken);
}

public interface IJobScheduler
{
    JobResponse Enqueue(string queue, string jobName, Guid tenantId, Guid? entityId = null);
    JobResponse ScheduleRecurring(string queue, string jobName, Guid tenantId, Guid? entityId, TimeSpan interval);
}

public interface IArticleWorkflowService
{
    Task<ArticleDetailResponse> ApproveAsync(Guid articleId, Guid approvedByUserId, CancellationToken cancellationToken);
    Task<JobResponse> PublishAsync(Guid articleId, Guid siteId, CancellationToken cancellationToken);
}

public interface ISiteService
{
    Task<IReadOnlyCollection<SiteResponse>> GetSitesAsync(CancellationToken cancellationToken);
    Task<SiteResponse> CreateAsync(CreateSiteRequest request, CancellationToken cancellationToken);
    Task<SiteResponse> ConfigureWordPressAsync(Guid siteId, UpdateWordPressSettingsRequest request, CancellationToken cancellationToken);
}

public interface IArticleRepository
{
    Task<IReadOnlyCollection<Article>> ListAsync(CancellationToken cancellationToken);
    Task<Article?> GetAsync(Guid id, CancellationToken cancellationToken);
    Task AddAsync(Article article, CancellationToken cancellationToken);
}

public interface IKeywordRepository
{
    Task<IReadOnlyCollection<Keyword>> ListAsync(CancellationToken cancellationToken);
    Task<Keyword?> GetBySiteAndTextAsync(Guid siteId, string text, CancellationToken cancellationToken);
    Task AddAsync(Keyword keyword, CancellationToken cancellationToken);
}

public interface IKeywordResearchRunRepository
{
    Task AddAsync(KeywordResearchRun researchRun, CancellationToken cancellationToken);
}

public interface ISiteRepository
{
    Task<IReadOnlyCollection<Site>> ListAsync(CancellationToken cancellationToken);
    Task<Site?> GetAsync(Guid id, CancellationToken cancellationToken);
    Task AddAsync(Site site, CancellationToken cancellationToken);
}

public interface IUnitOfWork
{
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public interface IUserRepository
{
    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken);
    Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken);
    Task AddAsync(User user, CancellationToken cancellationToken);
}

public interface IRoleRepository
{
    Task<Role?> GetByNameAsync(string name, CancellationToken cancellationToken);
    Task<Role> GetOrCreateAsync(string name, CancellationToken cancellationToken);
}

public interface IAiUsageRepository
{
    Task AddAsync(AiUsageRecord usageRecord, CancellationToken cancellationToken);
}

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string passwordHash);
}

public interface IDatabaseInitializer
{
    Task InitializeAsync(CancellationToken cancellationToken);
}
