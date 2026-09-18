namespace Wanetra.Application.Scheduling;

public sealed class ScheduleChangeSignal
{
    private volatile TaskCompletionSource<bool> current = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task WaitAsync(CancellationToken cancellationToken) => current.Task.WaitAsync(cancellationToken);

    public void Signal()
    {
        var previous = Interlocked.Exchange(
            ref current,
            new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously));
        previous.TrySetResult(true);
    }
}
