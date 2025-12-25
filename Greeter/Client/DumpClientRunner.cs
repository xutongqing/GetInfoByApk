using System;
using System.Net.Mime;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Windows;
using Dump.V1;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Grpc.Net.Client;

namespace DumpWpfClient;

public sealed class DumpClientRunner
{
    private readonly DumpTaskService.DumpTaskServiceClient _client;

    public DumpClientRunner(string address)
    {
        var ch = GrpcChannel.ForAddress(address);
        _client = new DumpTaskService.DumpTaskServiceClient(ch);
    }

    public async Task RunAsync(CancellationToken ct)
{
    using var call = _client.CreateTask(cancellationToken: ct);

    // Dialog 队列（只允许一个弹框同时存在）
    var dialogQueue = Channel.CreateUnbounded<DialogRequest>();

    // 1) 读循环：永远不阻塞
    var readLoop = Task.Run(async () =>
    {
        await foreach (var resp in call.ResponseStream.ReadAllAsync(ct).ConfigureAwait(false))
        {
            if (resp.DialogRequest != null)
            {
                // 关键：不要在这里等待用户输入
                await dialogQueue.Writer.WriteAsync(resp.DialogRequest, ct).ConfigureAwait(false);
                continue;
            }

            if (resp.Event != null)
                System.Diagnostics.Debug.WriteLine($"[EVENT] {resp.Event.Level} {resp.Event.Message}");

            if (resp.Progress != null)
                System.Diagnostics.Debug.WriteLine($"[PROGRESS] {resp.Progress.Percent}%");

            if (resp.Finished != null)
                System.Diagnostics.Debug.WriteLine($"[FINISHED] {resp.Finished.Code} {resp.Finished.Message}");
        }
    }, CancellationToken.None);

    // 2) Dialog 处理 worker：串行弹框 + 写回 DialogResponse
    var dialogWorker = Task.Run(async () =>
    {
        await foreach (var dlg in dialogQueue.Reader.ReadAllAsync(ct).ConfigureAwait(false))
        {
            // WPF: UI线程弹框，但这里只阻塞 dialogWorker，不阻塞 readLoop
            // bool ok = await Application.Current.Dispatcher.InvokeAsync(() =>
            // {
            //     var r = MessageBox.Show(dlg.Message, dlg.Title, MessageBoxButton.OKCancel);
            //     return r == MessageBoxResult.OK;
            // });
            await Task.Delay(10_000, ct).ConfigureAwait(false);

            await call.RequestStream.WriteAsync(new CreateTaskRequest
            {
                TaskId = "single",
                DialogResponse = new DialogResponse
                {
                    DialogId = dlg.DialogId,
                    Result =  DialogResult.Ok ,
                    PayloadJson = ""
                }
            }).ConfigureAwait(false);
        }
    }, CancellationToken.None);

    // 3) 写 StartTask
    await call.RequestStream.WriteAsync(new CreateTaskRequest
    {
        TaskId = "single",
        StartTask = new StartTaskRequest
        {
            TaskType = "AdbBackupTask",
            ConfigJson = "{}",
            TargetDirectory = "D:\\DumpOut"
        }
    }).ConfigureAwait(false);

    // 4) 心跳（可选）
    _ = Task.Run(async () =>
    {
        long seq = 0;
        while (!ct.IsCancellationRequested)
        {
            await Task.Delay(2000, ct).ConfigureAwait(false);
            await call.RequestStream.WriteAsync(new CreateTaskRequest
            {
                TaskId = "single",
                Ping = new ClientPing { Seq = ++seq, Ts = Timestamp.FromDateTime(DateTime.UtcNow) }
            }).ConfigureAwait(false);
        }
    }, CancellationToken.None);

    // 5) 等待结束
    await readLoop.ConfigureAwait(false);

    // 收尾
    dialogQueue.Writer.TryComplete();
    try { await dialogWorker.ConfigureAwait(false); } catch { }

    await call.RequestStream.CompleteAsync().ConfigureAwait(false);
}
}
