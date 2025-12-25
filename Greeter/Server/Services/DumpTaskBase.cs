using System;
using System.Threading;
using System.Threading.Tasks;

namespace DumpServer;

public enum DumpExitCode
{
    Success = 0,
    Error   = 1,
    Cancel  = 2,
}

public abstract class DumpTaskBase
{
    protected DumpTaskContext Ctx { get; }

    protected DumpTaskBase(DumpTaskContext ctx) => Ctx = ctx;

    public async Task<DumpExitCode> RunAsync(CancellationToken ct)
    {
        try
        {
            Ctx.LogInfo("Task entering RunAsync", stage: "Run");
            await StartOverrideAsync(ct).ConfigureAwait(false);
            Ctx.LogInfo("Task completed", stage: "Run");
            return DumpExitCode.Success;
        }
        catch (OperationCanceledException)
        {
            Ctx.LogWarn("Task canceled", stage: "Run");
            return DumpExitCode.Cancel;
        }
        catch (Exception ex)
        {
            Ctx.LogError($"Task error: {ex.Message}", stage: "Run");
            return DumpExitCode.Error;
        }
    }

    protected abstract Task StartOverrideAsync(CancellationToken cancellationToken);

    protected Task<DialogResponseModel> ConfirmAsync(string title, string message, CancellationToken ct)
        => Ctx.AskAsync(new DialogRequestModel(
            DialogId: Guid.NewGuid().ToString("N"),
            Title: title,
            Message: message,
            Kind: 2,              // DIALOG_KIND_CONFIRM
            PayloadJson: "",
            TimeoutSec: 0
        ), ct);

    protected void Progress(double percent, ProgressStateModel state = ProgressStateModel.RUNNING,
        long currentBytes = 0, long totalBytes = 0, long bps = 0, long etaSec = 0)
    {
        Ctx.ReportProgress(new ProgressModel
        {
            Percent = percent,
            CurrentBytes = currentBytes,
            TotalBytes = totalBytes,
            Bps = bps,
            EtaSec = etaSec,
            State = state
        });
    }

    protected void Info(string msg, string? stage = null) => Ctx.LogInfo(msg, stage);
    protected void Warn(string msg, string? stage = null) => Ctx.LogWarn(msg, stage);
    protected void Error(string msg, string? stage = null) => Ctx.LogError(msg, stage);
}

public sealed class AdbBackupTask : DumpTaskBase
{
    public AdbBackupTask(DumpTaskContext ctx) : base(ctx) { }

    protected override async Task StartOverrideAsync(CancellationToken cancellationToken)
    {
        Info("Step1: 准备开始提取...", stage: "Init");
        Progress(0, ProgressStateModel.RUNNING);

        await Task.Delay(1000, cancellationToken).ConfigureAwait(false);
        Progress(5);

        _ = Task.Run(async () =>
        {
            for (int i = 0; i < 10; i++)
            {
                await Task.Delay(1000, cancellationToken).ConfigureAwait(false);
                Info("异步线程输出日志");
            }
        }, cancellationToken);

        Info("等待用户确认...", stage: "UserGate");
        var resp = await ConfirmAsync(
            "需要用户确认",
            "请在手机上完成授权/确认操作，然后在客户端点击“继续(OK)”以继续提取；点 Cancel 将取消任务。",
            cancellationToken).ConfigureAwait(false);

        if (resp.Result != 1) // DIALOG_RESULT_OK = 1
        {
            Warn("用户取消", stage: "UserGate");
            Progress(0, ProgressStateModel.CANCEL);
            throw new OperationCanceledException(cancellationToken);
        }

        Info("用户已确认，继续提取...", stage: "Work");
        for (int i = 0; i < 10; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Delay(200, cancellationToken).ConfigureAwait(false);
            Progress(5 + (i + 1) * 9); // 5 -> 95
        }

        Info("提取完成", stage: "Done");
        Progress(100, ProgressStateModel.SUCCESS);
    }
}
