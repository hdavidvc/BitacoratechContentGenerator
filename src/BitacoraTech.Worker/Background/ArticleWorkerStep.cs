using System.Text.RegularExpressions;
using BitacoraTech.Application.Common;
using BitacoraTech.Domain.Articles;
using BitacoraTech.Infrastructure.Hangfire;
using BitacoraTech.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BitacoraTech.Worker.Background;

public sealed class ArticleWorkerStep(
    IServiceScopeFactory scopeFactory,
    ILogger<ArticleWorkerStep> logger) : IBackgroundWorkerStep
{
    public string Name => "article-worker";

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BitacoraTechDbContext>();
        var prompts = scope.ServiceProvider.GetRequiredService<IPromptTemplateService>();
        var ai = scope.ServiceProvider.GetRequiredService<IAiGateway>();
        var scheduler = scope.ServiceProvider.GetRequiredService<IJobScheduler>();

        var pendingArticles = await db.Articles
            .Where(x => x.Status == ArticleStatus.Generating)
            .OrderBy(x => x.CreatedAt)
            .Take(10)
            .ToListAsync(cancellationToken);

        foreach (var article in pendingArticles)
        {
            try
            {
                var prompt = await prompts.RenderAsync(
                    "article.generate",
                    new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["keyword"] = article.Title,
                        ["audience"] = "blog readers"
                    },
                    cancellationToken);

                var completion = await ai.CompleteAsync(
                    "article.generate",
                    $"{prompt}\n\nReturn valid HTML with one <h1> and several <h2>/<p> sections about: {article.Title}.",
                    cancellationToken);

                var html = NormalizeHtml(completion, article.Title);
                var title = ExtractTitle(html, article.Title);

                article.CompleteGeneration(title, html);
                await db.SaveChangesAsync(cancellationToken);

                scheduler.Enqueue(QueueNames.Media, nameof(Jobs.GenerateArticleImagesJob), article.TenantId, article.Id);
                scheduler.Enqueue(QueueNames.Seo, nameof(Jobs.AnalyzeSeoJob), article.TenantId, article.Id);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Article generation failed for article {ArticleId}", article.Id);
                article.MarkGenerationFailed();
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static string NormalizeHtml(string completion, string fallbackTitle)
    {
        if (string.IsNullOrWhiteSpace(completion))
        {
            return $"<h1>{fallbackTitle}</h1><p>Content generation returned an empty response.</p>";
        }

        var trimmed = completion.Trim();
        if (trimmed.Contains('<'))
        {
            return trimmed;
        }

        var paragraphs = trimmed
            .Split(["\r\n\r\n", "\n\n"], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(static paragraph => $"<p>{System.Net.WebUtility.HtmlEncode(paragraph)}</p>");

        return $"<h1>{System.Net.WebUtility.HtmlEncode(fallbackTitle)}</h1>{string.Concat(paragraphs)}";
    }

    private static string ExtractTitle(string html, string fallbackTitle)
    {
        var match = Regex.Match(html, "<h1[^>]*>(.*?)</h1>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        if (!match.Success)
        {
            return fallbackTitle;
        }

        var title = Regex.Replace(match.Groups[1].Value, "<[^>]+>", string.Empty).Trim();
        return string.IsNullOrWhiteSpace(title) ? fallbackTitle : title;
    }
}
