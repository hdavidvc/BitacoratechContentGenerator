using BitacoraTech.Application.Common;
using BitacoraTech.Contracts.Common;
using BitacoraTech.Contracts.Seo;
using BitacoraTech.Infrastructure.Services;
using Xunit;

namespace BitacoraTech.UnitTests;

public sealed class SeoAnalysisServiceTests
{
    [Fact]
    public async Task AnalyzeAsync_ReturnsAdvancedSeoSignals()
    {
        var service = new SeoAnalysisService(
            new StubCurrentUserContext(),
            new StubArticleRepository(),
            new StubUnitOfWork(),
            new StubJobScheduler());

        var response = await service.AnalyzeAsync(
            new SeoAnalyzeRequest(
                null,
                "Advanced SEO engine",
                """
                <h1>Advanced SEO engine</h1>
                <p>An advanced SEO engine improves content strategy, search visibility, and topical authority.</p>
                <h2>Why use an advanced SEO engine?</h2>
                <p>The advanced SEO engine helps with content optimization, internal linking, readability, semantic SEO, and FAQ markup.</p>
                <h2>How does an advanced SEO engine improve readability?</h2>
                <p>It uses short sentences, clear headings, and connected entities for better search intent coverage.</p>
                """,
                "advanced SEO engine"),
            CancellationToken.None);

        Assert.InRange(response.KeywordDensity, 1m, 10m);
        Assert.NotEmpty(response.SemanticKeywords);
        Assert.NotEmpty(response.InternalLinks);
        Assert.NotEmpty(response.Faqs);
        Assert.Contains("FAQPage", response.FaqSchema);
        Assert.Contains("\"@type\":\"Article\"", response.JsonLd);
        Assert.True(response.ReadabilityMetrics.WordCount > 0);
        Assert.InRange(response.Score, 1, 100);
    }

    [Fact]
    public async Task AnalyzeAsync_FlagsThinSeoCoverage()
    {
        var service = new SeoAnalysisService(
            new StubCurrentUserContext(),
            new StubArticleRepository(),
            new StubUnitOfWork(),
            new StubJobScheduler());

        var response = await service.AnalyzeAsync(
            new SeoAnalyzeRequest(null, "SEO", "<p>SEO basics.</p>", "SEO"),
            CancellationToken.None);

        Assert.Contains(response.Recommendations, x => x.Contains("internal links", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(response.Recommendations, x => x.Contains("semantic", StringComparison.OrdinalIgnoreCase));
    }

    private sealed class StubCurrentUserContext : ICurrentUserContext
    {
        public Guid? UserId => Guid.NewGuid();
        public Guid? TenantId => Guid.NewGuid();
        public bool IsAuthenticated => true;
    }

    private sealed class StubArticleRepository : IArticleRepository
    {
        public Task<IReadOnlyCollection<BitacoraTech.Domain.Articles.Article>> ListAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyCollection<BitacoraTech.Domain.Articles.Article>>([]);

        public Task<BitacoraTech.Domain.Articles.Article?> GetAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult<BitacoraTech.Domain.Articles.Article?>(null);

        public Task AddAsync(BitacoraTech.Domain.Articles.Article article, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class StubUnitOfWork : IUnitOfWork
    {
        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class StubJobScheduler : IJobScheduler
    {
        public JobResponse Enqueue(string queue, string jobName, Guid tenantId, Guid? entityId = null) => new(Guid.NewGuid().ToString(), queue, "Queued");
        public JobResponse ScheduleRecurring(string queue, string jobName, Guid tenantId, Guid? entityId, TimeSpan interval) => new(Guid.NewGuid().ToString(), queue, "Queued");
    }
}
