using BitacoraTech.Application.Common;
using BitacoraTech.Contracts.Articles;
using BitacoraTech.Domain.Articles;
using BitacoraTech.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BitacoraTech.Worker.Background;

public sealed class PublishingQueueStep(
    IServiceScopeFactory scopeFactory,
    ILogger<PublishingQueueStep> logger) : IBackgroundWorkerStep
{
    public string Name => "publishing-queue";

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BitacoraTechDbContext>();
        var publisher = scope.ServiceProvider.GetRequiredService<IWordPressPublishingService>();

        var pendingArticles = await db.Articles
            .Include(x => x.Images)
            .Include(x => x.Categories)
            .Include(x => x.Tags)
            .Where(x => x.Status == ArticleStatus.Approved)
            .OrderBy(x => x.ApprovedAt)
            .Take(10)
            .ToListAsync(cancellationToken);

        foreach (var article in pendingArticles)
        {
            var site = await db.Sites
                .Include(x => x.WordPressConnection)
                .FirstOrDefaultAsync(x => x.Id == article.SiteId, cancellationToken);

            if (site?.WordPressConnection is null)
            {
                logger.LogWarning("Skipping article {ArticleId} because site {SiteId} has no WordPress configuration", article.Id, article.SiteId);
                continue;
            }

            article.MarkPublishing();
            await db.SaveChangesAsync(cancellationToken);

            try
            {
                var featuredImage = article.Images.FirstOrDefault();
                var featuredImageUrl = featuredImage is not null && Uri.TryCreate(featuredImage.Url, UriKind.Absolute, out _)
                    ? featuredImage.Url
                    : null;

                var result = await publisher.PublishAsync(
                    article,
                    site,
                    new WordPressPublishRequest(
                        WordPressPublicationMode.Publish,
                        null,
                        article.Categories.Select(static x => x.Name).ToArray(),
                        article.Tags.Select(static x => x.Name).ToArray(),
                        featuredImageUrl,
                        featuredImage?.AltText,
                        null,
                        null),
                    cancellationToken);

                article.MarkPublished(result.PostId, DateTimeOffset.UtcNow);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Publishing failed for article {ArticleId}", article.Id);
                article.MarkPublishFailed();
            }

            await db.SaveChangesAsync(cancellationToken);
        }
    }
}
