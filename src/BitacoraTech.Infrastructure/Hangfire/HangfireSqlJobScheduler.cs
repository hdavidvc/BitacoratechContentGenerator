using BitacoraTech.Application.Common;
using BitacoraTech.Contracts.Common;

namespace BitacoraTech.Infrastructure.Hangfire;

public static class QueueNames
{
    public const string Keywords = "keywords";
    public const string Articles = "articles";
    public const string Seo = "seo";
    public const string Media = "media";
    public const string Publishing = "publishing";
    public const string Maintenance = "maintenance";
}

public sealed class HangfireSqlJobScheduler : IJobScheduler
{
    public JobResponse Enqueue(string queue, string jobName, Guid tenantId, Guid? entityId = null)
    {
        var jobId = $"{queue}:{jobName}:{entityId ?? tenantId}:{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
        return new JobResponse(jobId, queue, "Queued");
    }

    public JobResponse ScheduleRecurring(string queue, string jobName, Guid tenantId, Guid? entityId, TimeSpan interval)
    {
        var jobId = $"{queue}:{jobName}:recurring:{entityId ?? tenantId}:{interval.TotalMinutes:N0}m";
        return new JobResponse(jobId, queue, "Scheduled");
    }
}
