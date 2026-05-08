namespace BitacoraTech.Worker.Background;

public interface IBackgroundWorkerStep
{
    string Name { get; }
    Task ExecuteAsync(CancellationToken cancellationToken);
}
