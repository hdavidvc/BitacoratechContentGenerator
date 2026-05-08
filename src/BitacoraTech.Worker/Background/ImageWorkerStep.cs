using System.Text;
using BitacoraTech.Application.Common;
using BitacoraTech.Domain.Articles;
using BitacoraTech.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BitacoraTech.Worker.Background;

public sealed class ImageWorkerStep(
    IServiceScopeFactory scopeFactory,
    ILogger<ImageWorkerStep> logger) : IBackgroundWorkerStep
{
    public string Name => "image-worker";

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BitacoraTechDbContext>();
        var media = scope.ServiceProvider.GetRequiredService<IMediaService>();

        var pendingArticles = await db.Articles
            .Include(x => x.Images)
            .Where(x => (x.Status == ArticleStatus.Generated || x.Status == ArticleStatus.ReadyForReview) && !x.Images.Any())
            .OrderBy(x => x.CreatedAt)
            .Take(10)
            .ToListAsync(cancellationToken);

        foreach (var article in pendingArticles)
        {
            try
            {
                var svg = BuildSvg(article.Title);
                await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(svg));
                var url = await media.UploadArticleImageAsync(article.Id, stream, "featured.svg", cancellationToken);
                article.AddImage(new ArticleImage(Guid.NewGuid(), article.TenantId, article.Id, url, $"{article.Title} featured image"));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Image generation failed for article {ArticleId}", article.Id);
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static string BuildSvg(string title)
    {
        var safeTitle = System.Security.SecurityElement.Escape(title) ?? "Article";
        return $$"""
        <svg xmlns="http://www.w3.org/2000/svg" width="1600" height="900" viewBox="0 0 1600 900">
          <defs>
            <linearGradient id="bg" x1="0%" y1="0%" x2="100%" y2="100%">
              <stop offset="0%" stop-color="#0f172a"/>
              <stop offset="100%" stop-color="#1d4ed8"/>
            </linearGradient>
          </defs>
          <rect width="1600" height="900" fill="url(#bg)" rx="48"/>
          <circle cx="1320" cy="180" r="220" fill="rgba(255,255,255,0.10)"/>
          <circle cx="260" cy="760" r="180" fill="rgba(255,255,255,0.08)"/>
          <text x="120" y="380" fill="#f8fafc" font-size="84" font-family="Segoe UI, Arial, sans-serif" font-weight="700">{{safeTitle}}</text>
          <text x="120" y="470" fill="#bfdbfe" font-size="36" font-family="Segoe UI, Arial, sans-serif">BitacoraTech automated cover</text>
        </svg>
        """;
    }
}
