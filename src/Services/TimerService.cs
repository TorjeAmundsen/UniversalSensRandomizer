using System;
using System.Threading;
using System.Threading.Tasks;

namespace UniversalSensRandomizer.Services;

public sealed class TimerService : IDisposable
{
    private CancellationTokenSource? cts;
    private Task? loopTask;

    public bool IsRunning => loopTask is { IsCompleted: false };

    public void Start(TimeSpan interval, Func<Task> tick)
    {
        Stop();
        cts = new CancellationTokenSource();
        CancellationToken token = cts.Token;
        loopTask = Task.Run(async () =>
        {
            using PeriodicTimer timer = new(interval);
            try
            {
                while (await timer.WaitForNextTickAsync(token).ConfigureAwait(false))
                {
                    try
                    {
                        await tick().ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch
                    {
                        // Swallow per-tick errors so a transient driver failure doesn't kill the timer.
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
        }, token);
    }

    public void Stop()
    {
        if (cts is { } source)
        {
            source.Cancel();
            try
            {
                loopTask?.Wait(TimeSpan.FromSeconds(2));
            }
            catch
            {
            }
            source.Dispose();
            cts = null;
            loopTask = null;
        }
    }

    public void Dispose()
    {
        Stop();
    }
}
