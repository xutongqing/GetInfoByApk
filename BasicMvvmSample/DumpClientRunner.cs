using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;
using BasicMvvmSample.ViewModels;
using Dump.V1;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Grpc.Net.Client;

namespace BasicMvvmSample;

public sealed class DumpClientRunner
{
    private readonly Window _owner;
    private readonly DumpViewModel _vm;

    private CancellationTokenSource? _cts;
    private Task? _running;

    public DumpClientRunner(Window owner, DumpViewModel vm)
    {
        _owner = owner;
        _vm = vm;
    }

    public bool IsRunning => _running is { IsCompleted: false };

    public Task StartAsync()
    {
        if (IsRunning) return Task.CompletedTask;

        _cts = new CancellationTokenSource();
        _running = RunCoreAsync(_cts.Token);
        return Task.CompletedTask;
    }

    public void Stop()
    {
        try { _cts?.Cancel(); } catch { }
    }

    private async Task RunCoreAsync(CancellationToken ct)
    {
        await Ui(() =>
        {
            _vm.IsRunning = true;
            _vm.Status = "Connecting...";
            _vm.Log("[CLIENT] Start");
            _vm.Progress = 0;
        }).ConfigureAwait(false);

        try
        {
            
            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback =
                    HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            };

            var ch = GrpcChannel.ForAddress(_vm.Address, new GrpcChannelOptions
            {
                HttpHandler = handler
            });
            
            var client = new DumpTaskService.DumpTaskServiceClient(ch);

            using var call = client.CreateTask(cancellationToken: ct);

            var requestOut = Channel.CreateUnbounded<CreateTaskRequest>(new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false
            });

            var dialogQueue = Channel.CreateUnbounded<DialogRequest>(new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false
            });

            // 单写循环：唯一写 RequestStream 的地方
            var writeLoop = Task.Run(async () =>
            {
                try
                {
                    await foreach (var req in requestOut.Reader.ReadAllAsync(ct).ConfigureAwait(false))
                        await call.RequestStream.WriteAsync(req).ConfigureAwait(false);
                }
                catch (OperationCanceledException) { }
                catch { }
            }, CancellationToken.None);

            // 读循环：永远不阻塞
            var readLoop = Task.Run(async () =>
            {
                try
                {
                    await foreach (var resp in call.ResponseStream.ReadAllAsync(ct).ConfigureAwait(false))
                    {
                        if (resp.Event != null)
                        {
                            System.Diagnostics.Debug.WriteLine($"[EVENT] {resp.Event.Level} {resp.Event.Message}");
                        }

                        if (resp.Progress != null)
                        {
                            System.Diagnostics.Debug.WriteLine($"[PROGRESS] {resp.Progress.Percent}%");
                        }

                        if (resp.DialogRequest != null)
                        {
                            //System.Diagnostics.Debug.WriteLine($"[FINISHED] {resp.Finished.Code} {resp.Finished.Message}");
                            //Console.WriteLine($"[CLIENT] {resp.DialogRequest.DialogId} {resp.DialogRequest.Message}");
                            await dialogQueue.Writer.WriteAsync(resp.DialogRequest, ct).ConfigureAwait(false);
                        }

                        if (resp.Finished != null)
                        {
                            break;
                        }
                    }
                }
                catch (OperationCanceledException operationCanceledException)
                {
                    Console.WriteLine($"[CLIENT] {operationCanceledException.Message}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[CLIENT] {ex}");
                }
            }, CancellationToken.None);

            // Dialog worker：串行弹框，结果写回 requestOut（不阻塞读循环）
            var dialogWorker = Task.Run(async () =>
            {
                try
                {
                    await foreach (var dlg in dialogQueue.Reader.ReadAllAsync(ct).ConfigureAwait(false))
                    {
                        bool ok = await DialogService.ShowConfirmAsync(_owner, dlg.Title, dlg.Message).ConfigureAwait(false);

                        await requestOut.Writer.WriteAsync(new CreateTaskRequest
                        {
                            TaskId = "single",
                            DialogResponse = new DialogResponse
                            {
                                DialogId = dlg.DialogId,
                                Result = ok ? DialogResult.Ok : DialogResult.Cancel,
                                PayloadJson = ""
                            }
                        }, ct).ConfigureAwait(false);
                    }
                }
                catch (OperationCanceledException) { }
                catch (Exception ex)
                {
                    await Ui(() => _vm.Log($"[DIALOG_ERROR] {ex.Message}")).ConfigureAwait(false);
                }
            }, CancellationToken.None);

            // StartTask
            await requestOut.Writer.WriteAsync(new CreateTaskRequest
            {
                TaskId = "single",
                StartTask = new StartTaskRequest
                {
                    TaskType = "AdbBackupTask",
                    ConfigJson = "{}",
                    TargetDirectory = "/tmp/DumpOut"
                }
            }, ct).ConfigureAwait(false);

            await Ui(() => _vm.Status = "Task started").ConfigureAwait(false);

            // Ping
            var pingLoop = Task.Run(async () =>
            {
                try
                {
                    long seq = 0;
                    while (!ct.IsCancellationRequested)
                    {
                        await Task.Delay(2000, ct).ConfigureAwait(false);
                        await requestOut.Writer.WriteAsync(new CreateTaskRequest
                        {
                            TaskId = "single",
                            Ping = new ClientPing { Seq = ++seq, Ts = Timestamp.FromDateTime(DateTime.UtcNow) }
                        }, ct).ConfigureAwait(false);
                    }
                }
                catch (OperationCanceledException) { }
            }, CancellationToken.None);

            // 等读循环结束
            await readLoop.ConfigureAwait(false);

            // 收尾
            dialogQueue.Writer.TryComplete();
            requestOut.Writer.TryComplete();

            try { await Task.WhenAll(dialogWorker, pingLoop).ConfigureAwait(false); } catch { }
            try { await writeLoop.ConfigureAwait(false); } catch { }

            try { await call.RequestStream.CompleteAsync().ConfigureAwait(false); } catch { }
        }
        catch (OperationCanceledException)
        {
            await Ui(() =>
            {
                _vm.Log("[CLIENT] Canceled");
                _vm.Status = "Canceled";
            }).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await Ui(() =>
            {
                _vm.Log($"[CLIENT_ERROR] {ex.Message}");
                _vm.Status = "Error";
            }).ConfigureAwait(false);
        }
        finally
        {
            await Ui(() => _vm.IsRunning = false).ConfigureAwait(false);
        }
    }

    private static Task Ui(Action a) => Dispatcher.UIThread.InvokeAsync(a).GetTask();
}