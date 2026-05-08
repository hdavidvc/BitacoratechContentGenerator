namespace BitacoraTech.Worker.Background;

public sealed class BackgroundWorkerRunner(
    IEnumerable<IBackgroundWorkerStep> steps,
    ILogger<BackgroundWorkerRunner> logger)
{
    public async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        foreach (var step in steps)
        {
            try
            {
                logger.LogInformation("Running background worker step {StepName}", step.Name);
                await step.ExecuteAsync(cancellationToken);
                logger.LogInformation("Completed background worker step {StepName}", step.Name);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Background worker step {StepName} failed", step.Name);
            }
        }
    }
}
