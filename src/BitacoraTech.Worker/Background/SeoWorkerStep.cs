using BitacoraTech.Contracts.Seo;
using BitacoraTech.Domain.Articles;
using BitacoraTech.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BitacoraTech.Worker.Background;

public sealed class SeoWorkerStep(
    IServiceScopeFactory scopeFactory,
    ILogger<SeoWorkerStep> logger) : IBackgroundWorkerStep
{
    public string Name => "seo-worker";

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BitacoraTechDbContext>();
        var seo = scope.ServiceProvider.GetRequiredService<BitacoraTech.Application.Common.ISeoAnalysisService>();

        var pendingArticles = await db.Articles
            .Include(x => x.SeoAnalyses)
            .Where(x => x.Status == ArticleStatus.Generated || x.Status == ArticleStatus.SeoFailed)
            .OrderBy(x => x.CreatedAt)
            .Take(10)
            .ToListAsync(cancellationToken);

        foreach (var article in pendingArticles)
        {
            try
            {
                article.MarkSeoAnalyzing();
                var analysis = await seo.AnalyzeAsync(
                    new SeoAnalyzeRequest(article.Id, article.Title, article.HtmlContent, article.Title),
                    cancellationToken);

                article.AddSeoAnalysis(new ArticleSeoAnalysis(Guid.NewGuid(), article.TenantId, article.Id, analysis.Score, string.Join(" | ", analysis.Recommendations)));
                article.MarkReadyForReview(analysis.Score);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "SEO analysis failed for article {ArticleId}", article.Id);
                article.MarkSeoFailed();
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
