using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Baseline.Dates;
using Jasper.Configuration;
using Jasper.Logging;
using Jasper.Persistence.Durability;
using Jasper.Transports;
using Microsoft.Extensions.Logging;

namespace Jasper.Runtime.WorkerQueues;

public class DurableWorkerQueue : IWorkerQueue, IChannelCallback, IHasNativeScheduling, IHasDeadLetterQueue, IAsyncDisposable
{
    private readonly ILogger _logger;
    private readonly IEnvelopePersistence _persistence;
    private readonly ActionBlock<Envelope> _receiver;
    private readonly AdvancedSettings _settings;
    private IListener? _listener;

    public DurableWorkerQueue(Endpoint endpoint, IHandlerPipeline pipeline,
        AdvancedSettings settings, IEnvelopePersistence persistence, ILogger logger)
    {
        _settings = settings;
        _persistence = persistence;
        _logger = logger;

        endpoint.ExecutionOptions.CancellationToken = settings.Cancellation;

        _receiver = new ActionBlock<Envelope>(async envelope =>
        {
            try
            {
                envelope.ContentType ??= EnvelopeConstants.JsonContentType;

                await pipeline.InvokeAsync(envelope, this);
            }
            catch (Exception? e)
            {
                // This *should* never happen, but of course it will
                logger.LogError(e, "Unexpected pipeline invocation error");
            }
        }, endpoint.ExecutionOptions);
    }

    private async Task executeWithRetriesAsync(Func<Task> action)
    {
        var i = 0;
        while (true)
        {
            try
            {
                await action().ConfigureAwait(false);
                return;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Unexpected failure");
                i++;
                await Task.Delay(i * 100).ConfigureAwait(false);
            }
        }
    }

    public Uri? Address { get; set; }

    public async ValueTask CompleteAsync(Envelope envelope)
    {
        await executeWithRetriesAsync(() => _persistence.DeleteIncomingEnvelopeAsync(envelope));
    }

    public async ValueTask DeferAsync(Envelope envelope)
    {
        envelope.Attempts++;

        Enqueue(envelope);

        await executeWithRetriesAsync(() => _persistence.IncrementIncomingEnvelopeAttemptsAsync(envelope));
    }

    public Task MoveToErrorsAsync(Envelope envelope, Exception exception)
    {
        var errorReport = new ErrorReport(envelope, exception);

        return executeWithRetriesAsync(() => _persistence.MoveToDeadLetterStorageAsync(new[] { errorReport }));
    }

    public Task MoveToScheduledUntilAsync(Envelope envelope, DateTimeOffset time)
    {
        envelope.OwnerId = TransportConstants.AnyNode;
        envelope.ScheduledTime = time;
        envelope.Status = EnvelopeStatus.Scheduled;

        return executeWithRetriesAsync(() => _persistence.ScheduleExecutionAsync(new[] { envelope }));
    }

    public int QueuedCount => _receiver.InputCount;

    public void Enqueue(Envelope envelope)
    {
        envelope.ReplyUri = envelope.ReplyUri ?? Address;
        _receiver.Post(envelope);
    }

    public void ScheduleExecution(Envelope envelope)
    {
        // Should never be called, this is only used by lightweight
        // worker queues
        throw new NotSupportedException();
    }

    public void StartListening(IListener listener)
    {
        _listener = listener;
        _listener.Start(this, _settings.Cancellation);

        Address = _listener.Address;
    }


    public Task ReceivedAsync(Uri uri, Envelope[] messages)
    {
        var now = DateTimeOffset.Now;

        return ProcessReceivedMessagesAsync(now, uri, messages);
    }

    public async ValueTask ReceivedAsync(Uri uri, Envelope envelope)
    {
        if (_listener == null) throw new InvalidOperationException($"Worker queue for {uri} has not been started");

        using var activity = JasperTracing.StartExecution(_settings.OpenTelemetryReceiveSpanName!, envelope,
            ActivityKind.Consumer);
        var now = DateTimeOffset.Now;
        envelope.MarkReceived(uri, now, _settings.UniqueNodeId);

        await _persistence.StoreIncomingAsync(envelope);

        if (envelope.Status == EnvelopeStatus.Incoming)
        {
            Enqueue(envelope);
        }

        await _listener.CompleteAsync(envelope);

        _logger.IncomingReceived(envelope, Address);
    }


    public void Dispose()
    {
        // Might need to drain the block
        _receiver.Complete();
    }

    public async ValueTask DisposeAsync()
    {
        _receiver.Complete();
        await _receiver.Completion;
    }

    // Separated for testing here.
    public async Task ProcessReceivedMessagesAsync(DateTimeOffset now, Uri uri, Envelope[] envelopes)
    {
        if (_settings.Cancellation.IsCancellationRequested)
        {
            throw new OperationCanceledException();
        }

        foreach (var envelope in envelopes)
        {
            envelope.MarkReceived(uri, now, _settings.UniqueNodeId);
        }

        await _persistence.StoreIncomingAsync(envelopes);

        foreach (var message in envelopes)
        {
            Enqueue(message);
            await _listener!.CompleteAsync(message);
        }

        _logger.IncomingBatchReceived(envelopes);
    }
}
