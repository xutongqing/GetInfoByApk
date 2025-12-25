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

    private readonly ConcurrentDictionary<string, TaskCompletionSource<DialogResponseModel>> _pending =
        new(StringComparer.Ordinal);

    public CancellationToken Token => _cts.Token;

    public void Cancel() => _cts.Cancel();

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

        try
        {
            return await tcs.Task.ConfigureAwait(false);
        }
        finally
        {
            _pending.TryRemove(req.DialogId, out _);
        }
    }

    public void SetDialogResult(DialogResponseModel resp)
    {
        if (resp is null || string.IsNullOrWhiteSpace(resp.DialogId)) return;

        if (_pending.TryRemove(resp.DialogId, out var tcs))
            tcs.TrySetResult(resp);
        // 否则可能是：已取消/超时/重复响应，忽略即可
    }

    public IAsyncEnumerable<DialogRequestModel> ReadDialogsAsync(CancellationToken ct)
        => _dialogOut.Reader.ReadAllAsync(ct);

    public void Dispose()
    {
        _cts.Cancel();
        _dialogOut.Writer.TryComplete();
        _cts.Dispose();
    }
}

// 这里用简单 model 映射，便于任务层不依赖 proto 生成类型
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
