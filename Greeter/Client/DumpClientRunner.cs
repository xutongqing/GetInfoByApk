using System;
using System.Net.Mime;
using System.Threading;
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

        // 1) 先起读循环
        var readLoop = Task.Run(async () =>
        {
            await foreach (var resp in call.ResponseStream.ReadAllAsync(ct))
            {
                if (resp.DialogRequest != null)
                {
                    var dlg = resp.DialogRequest;

                    // UI 线程弹框
                    // bool ok = await MediaTypeNames.Application.Current.Dispatcher.InvokeAsync(() =>
                    // {
                    //     var r = MessageBox.Show(dlg.Message, dlg.Title, MessageBoxButton.OKCancel);
                    //     return r == MessageBoxResult.OK;
                    // });
                    await Task.Delay(10_000, ct).ConfigureAwait(false);

                    // 回写 DialogResponse
                    await call.RequestStream.WriteAsync(new CreateTaskRequest
                    {
                        TaskId = resp.TaskId,
                        DialogResponse = new DialogResponse
                        {
                            DialogId = dlg.DialogId,
                            Result = DialogResult.Ok,
                            PayloadJson = ""
                        }
                    }).ConfigureAwait(false);
                }
                else if (resp.Event != null)
                {
                    // TODO: 绑定到 UI 日志列表
                    System.Diagnostics.Debug.WriteLine($"[EVENT] {resp.Event.Level} {resp.Event.Message}");
                }
                else if (resp.Progress != null)
                {
                    // TODO: 更新进度条
                    System.Diagnostics.Debug.WriteLine($"[PROGRESS] {resp.Progress.Percent}%");
                }
                else if (resp.Finished != null)
                {
                    System.Diagnostics.Debug.WriteLine($"[FINISHED] {resp.Finished.Code} {resp.Finished.Message}");
                }
            }
        }, ct);

        // 2) 写 StartTask
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

        // 3) 可选：心跳
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

        // 4) 等读循环结束（服务端 finished/断开）
        await readLoop.ConfigureAwait(false);

        // 5) 结束写流
        await call.RequestStream.CompleteAsync().ConfigureAwait(false);
    }
}
