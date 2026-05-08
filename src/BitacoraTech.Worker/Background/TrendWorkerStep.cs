using BitacoraTech.Application.Common;
using BitacoraTech.Infrastructure.Configuration;
using BitacoraTech.Infrastructure.Hangfire;
using Microsoft.Extensions.Options;

namespace BitacoraTech.Worker.Background;

public sealed class TrendWorkerStep(
    IServiceScopeFactory scopeFactory,
    IOptions<KeywordResearchOptions> keywordOptions,
    ILogger<TrendWorkerStep> logger) : IBackgroundWorkerStep
{
    private readonly KeywordResearchOptions options = keywordOptions.Value;

    public string Name => "trend-worker";

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var sites = scope.ServiceProvider.GetRequiredService<ISiteRepository>();
        var runner = scope.ServiceProvider.GetRequiredService<IKeywordResearchRunner>();
        var scheduler = scope.ServiceProvider.GetRequiredService<IJobScheduler>();
        var interval = TimeSpan.FromMinutes(Math.Max(15, options.SchedulerIntervalMinutes));

        var configuredSites = await sites.ListAsync(cancellationToken);
        foreach (var site in configuredSites)
        {
            var scheduled = scheduler.ScheduleRecurring(QueueNames.Keywords, nameof(Jobs.ResearchKeywordJob), site.TenantId, site.Id, interval);
            logger.LogInformation("Keyword trend scheduler {JobId} running for site {SiteId}", scheduled.JobId, site.Id);

            var context = new KeywordResearchContext(
                site.TenantId,
                site.Id,
                site.Name,
                options.DefaultCountry,
                options.DefaultLanguage,
                options.RedditSubreddits,
                options.RssFeeds);

            var result = await runner.RunAsync(context, cancellationToken);
            logger.LogInformation("Keyword research completed for site {SiteId}: {Count} keywords", site.Id, result.Keywords.Count);
        }
    }
}
