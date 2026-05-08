using BitacoraTech.Infrastructure.Configuration;
using BitacoraTech.Worker.Background;
using Microsoft.Extensions.Options;

namespace BitacoraTech.Worker;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeSpan _pollingInterval;

    public Worker(ILogger<Worker> logger, IServiceScopeFactory scopeFactory, IOptions<KeywordResearchOptions> keywordOptions)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
        _pollingInterval = TimeSpan.FromMinutes(Math.Max(15, keywordOptions.Value.SchedulerIntervalMinutes));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            using var scope = _scopeFactory.CreateScope();
            var runner = scope.ServiceProvider.GetRequiredService<BackgroundWorkerRunner>();
            await runner.RunOnceAsync(stoppingToken);
            await Task.Delay(_pollingInterval, stoppingToken);
        }
    }
}
