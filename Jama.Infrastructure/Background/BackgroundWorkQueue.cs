using System.Threading.Channels;
using Jama.Application.Common.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jama.Infrastructure.Background;

/// <summary>A simple in-process queue: requests drop work in, one worker runs it in order. A failure is logged and never stops the queue.</summary>
public sealed class BackgroundWorkQueue(ILogger<BackgroundWorkQueue> logger) : BackgroundService, IBackgroundWork
{
    private readonly Channel<Func<CancellationToken, Task>> _queue = Channel.CreateUnbounded<Func<CancellationToken, Task>>();

    public void Enqueue(Func<CancellationToken, Task> work) => _queue.Writer.TryWrite(work);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var work in _queue.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await work(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "A background task failed.");
            }
        }
    }
}
