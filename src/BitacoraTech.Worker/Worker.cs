using BitacoraTech.Application.Common;
using BitacoraTech.Infrastructure.Configuration;
using BitacoraTech.Infrastructure.Hangfire;
using Microsoft.Extensions.Options;

namespace BitacoraTech.Worker;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly KeywordResearchOptions _keywordOptions;
    private readonly TimeSpan _pollingInterval;

    public Worker(ILogger<Worker> logger, IServiceScopeFactory scopeFactory, IOptions<KeywordResearchOptions> keywordOptions)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
        _keywordOptions = keywordOptions.Value;
        _pollingInterval = TimeSpan.FromMinutes(Math.Max(15, _keywordOptions.SchedulerIntervalMinutes));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await RunKeywordResearchScheduleAsync(stoppingToken);
            await Task.Delay(_pollingInterval, stoppingToken);
        }
    }

    private async Task RunKeywordResearchScheduleAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var sites = scope.ServiceProvider.GetRequiredService<ISiteRepository>();
        var runner = scope.ServiceProvider.GetRequiredService<IKeywordResearchRunner>();
        var scheduler = scope.ServiceProvider.GetRequiredService<IJobScheduler>();

        var configuredSites = await sites.ListAsync(cancellationToken);
        foreach (var site in configuredSites)
        {
            var scheduled = scheduler.ScheduleRecurring(QueueNames.Keywords, nameof(Jobs.ResearchKeywordJob), site.TenantId, site.Id, _pollingInterval);
            _logger.LogInformation("Keyword trend scheduler {JobId} running for site {SiteId}", scheduled.JobId, site.Id);

            var context = new KeywordResearchContext(
                site.TenantId,
                site.Id,
                site.Name,
                _keywordOptions.DefaultCountry,
                _keywordOptions.DefaultLanguage,
                _keywordOptions.RedditSubreddits,
                _keywordOptions.RssFeeds);

            var result = await runner.RunAsync(context, cancellationToken);
            _logger.LogInformation("Keyword research completed for site {SiteId}: {Count} keywords", site.Id, result.Keywords.Count);
        }
    }
}
