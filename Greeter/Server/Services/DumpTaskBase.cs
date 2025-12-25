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
            await StartOverrideAsync(ct).ConfigureAwait(false);
            return DumpExitCode.Success;
        }
        catch (OperationCanceledException)
        {
            return DumpExitCode.Cancel;
        }
        catch
        {
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
}

public sealed class AdbBackupTask : DumpTaskBase
{
    public AdbBackupTask(DumpTaskContext ctx) : base(ctx) { }

    protected override async Task StartOverrideAsync(CancellationToken cancellationToken)
    {
        // 模拟：前置工作
        Console.WriteLine("Step1: 准备开始提取...");
        await Task.Delay(1000, cancellationToken).ConfigureAwait(false);

        // 关键：等待 UI 点击“继续”
        var resp = await ConfirmAsync(
            "需要用户确认",
            "请在手机上完成授权/确认操作，然后在客户端点击“继续(OK)”以继续提取；点 Cancel 将取消任务。",
            cancellationToken).ConfigureAwait(false);

        if (resp.Result != 1) // DIALOG_RESULT_OK = 1
            throw new OperationCanceledException(cancellationToken);

        // 模拟：后续工作
        Console.WriteLine("Step2: 用户已确认，继续提取...");
        await Task.Delay(2000, cancellationToken).ConfigureAwait(false);

        Console.WriteLine("Step3: 提取完成。");
    }
}
