using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace DumpServer;

public sealed class DumpTaskContext : IDisposable
{
    private readonly CancellationTokenSource _cts = new();

    private readonly Channel<DialogRequestModel> _dialogOut = Channel.CreateUnbounded<DialogRequestModel>(
        new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });

    private readonly Channel<EventModel> _eventOut = Channel.CreateUnbounded<EventModel>(
        new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });

    // 进度建议“只保留最新”：用 Bounded + DropOldest，UI 不会被刷爆
    private readonly Channel<ProgressModel> _progressOut = Channel.CreateBounded<ProgressModel>(
        new BoundedChannelOptions(capacity: 1)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.DropOldest
        });

    private readonly ConcurrentDictionary<string, TaskCompletionSource<DialogResponseModel>> _pending =
        new(StringComparer.Ordinal);

    public CancellationToken Token => _cts.Token;

    public void Cancel() => _cts.Cancel();

    // ---------- Dialog ----------
    public async Task<DialogResponseModel> AskAsync(DialogRequestModel req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.DialogId))
            req = req with { DialogId = Guid.NewGuid().ToString("N") };

        var tcs = new TaskCompletionSource<DialogResponseModel>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_pending.TryAdd(req.DialogId, tcs))
            throw new InvalidOperationException($"Duplicate dialog id: {req.DialogId}");

        using var reg = ct.Register(() =>
        {
            if (_pending.TryRemove(req.DialogId, out var p))
                p.TrySetCanceled(ct);
        });

        await _dialogOut.Writer.WriteAsync(req, ct).ConfigureAwait(false);

        try { return await tcs.Task.ConfigureAwait(false); }
        finally { _pending.TryRemove(req.DialogId, out _); }
    }

    public void SetDialogResult(DialogResponseModel resp)
    {
        if (resp is null || string.IsNullOrWhiteSpace(resp.DialogId)) return;
        if (_pending.TryRemove(resp.DialogId, out var tcs))
            tcs.TrySetResult(resp);
    }

    public IAsyncEnumerable<DialogRequestModel> ReadDialogsAsync(CancellationToken ct)
        => _dialogOut.Reader.ReadAllAsync(ct);

    // ---------- Event / Log ----------
    public void LogInfo(string message, string? stage = null, string? code = null)
        => WriteEvent(new EventModel(EventLevelModel.INFO, message, stage, code));

    public void LogWarn(string message, string? stage = null, string? code = null)
        => WriteEvent(new EventModel(EventLevelModel.WARN, message, stage, code));

    public void LogError(string message, string? stage = null, string? code = null)
        => WriteEvent(new EventModel(EventLevelModel.ERROR, message, stage, code));

    public void WriteEvent(EventModel e)
    {
        // 不要阻塞任务主流程；尽力写入即可
        _eventOut.Writer.TryWrite(e with { TimeUtc = DateTime.UtcNow });
    }

    public IAsyncEnumerable<EventModel> ReadEventsAsync(CancellationToken ct)
        => _eventOut.Reader.ReadAllAsync(ct);

    // ---------- Progress ----------
    public void ReportProgress(ProgressModel p)
    {
        // 只保留最新
        _progressOut.Writer.TryWrite(p);
    }

    public IAsyncEnumerable<ProgressModel> ReadProgressAsync(CancellationToken ct)
        => _progressOut.Reader.ReadAllAsync(ct);

    public void Dispose()
    {
        _cts.Cancel();
        _dialogOut.Writer.TryComplete();
        _eventOut.Writer.TryComplete();
        _progressOut.Writer.TryComplete();
        _cts.Dispose();
    }
}

public enum EventLevelModel { INFO = 1, WARN = 2, ERROR = 3 }

public sealed record EventModel(
    EventLevelModel Level,
    string Message,
    string? Stage = null,
    string? Code = null)
{
    public DateTime TimeUtc { get; init; } = DateTime.UtcNow;
}

public enum ProgressStateModel { WAITING = 1, RUNNING = 2, SUCCESS = 3, ERROR = 4, CANCEL = 5 }

public sealed record ProgressModel
{
    public double Percent { get; init; }
    public long CurrentBytes { get; init; }
    public long TotalBytes { get; init; }
    public long Bps { get; init; }
    public long EtaSec { get; init; }
    public ProgressStateModel State { get; init; } = ProgressStateModel.RUNNING;
}

// 原有 models 保留
public sealed record DialogRequestModel(
    string DialogId,
    string Title,
    string Message,
    int Kind,
    string PayloadJson,
    long TimeoutSec);

public sealed record DialogResponseModel(
    string DialogId,
    int Result,
    string PayloadJson);