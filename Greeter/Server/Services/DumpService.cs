using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Dump.V1;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;

namespace DumpServer;

public sealed class DumpTaskGrpcService : DumpTaskService.DumpTaskServiceBase
{
    public override async Task CreateTask(
        IAsyncStreamReader<CreateTaskRequest> requestStream,
        IServerStreamWriter<CreateTaskResponse> responseStream,
        ServerCallContext context)
    {
        using var taskCtx = new DumpTaskContext();
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(taskCtx.Token, context.CancellationToken);
        var ct = linkedCts.Token;

        // 单一写通道：所有响应都从这里出去，避免并发写 responseStream
        var outbox = Channel.CreateUnbounded<CreateTaskResponse>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });

        // 统一 Writer：唯一一个对 responseStream.WriteAsync 的调用点
        var writer = Task.Run(async () =>
        {
            try
            {
                await foreach (var msg in outbox.Reader.ReadAllAsync(ct).ConfigureAwait(false))
                {
                    await responseStream.WriteAsync(msg).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) { }
            catch { /* client disconnected */ }
        }, CancellationToken.None);

        // 订阅：把 taskCtx 里产生的 Dialog/Event/Progress 转成 proto 响应投递到 outbox
        var dialogLoop = Task.Run(async () =>
        {
            try
            {
                await foreach (var dlg in taskCtx.ReadDialogsAsync(ct).ConfigureAwait(false))
                {
                    await outbox.Writer.WriteAsync(new CreateTaskResponse
                    {
                        TaskId = "single",
                        DialogRequest = new DialogRequest
                        {
                            DialogId = dlg.DialogId,
                            Kind = (DialogKind)dlg.Kind,
                            Title = dlg.Title,
                            Message = dlg.Message,
                            PayloadJson = dlg.PayloadJson ?? "",
                            TimeoutSec = dlg.TimeoutSec
                        }
                    }, ct).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) { }
        }, CancellationToken.None);

        var eventLoop = Task.Run(async () =>
        {
            try
            {
                await foreach (var e in taskCtx.ReadEventsAsync(ct).ConfigureAwait(false))
                {
                    await outbox.Writer.WriteAsync(new CreateTaskResponse
                    {
                        TaskId = "single",
                        Event = new TaskEvent
                        {
                            Ts = Timestamp.FromDateTime(DateTime.SpecifyKind(e.TimeUtc, DateTimeKind.Utc)),
                            Level = (EventLevel)e.Level,
                            Message = e.Message ?? "",
                            Stage = e.Stage ?? "",
                            Code = e.Code ?? ""
                        }
                    }, ct).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) { }
        }, CancellationToken.None);

        var progressLoop = Task.Run(async () =>
        {
            try
            {
                await foreach (var p in taskCtx.ReadProgressAsync(ct).ConfigureAwait(false))
                {
                    await outbox.Writer.WriteAsync(new CreateTaskResponse
                    {
                        TaskId = "single",
                        Progress = new TaskProgress
                        {
                            Percent = p.Percent,
                            CurrentBytes = p.CurrentBytes,
                            TotalBytes = p.TotalBytes,
                            Bps = p.Bps,
                            EtaSec = p.EtaSec,
                            State = (ProgressState)p.State
                        }
                    }, ct).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) { }
        }, CancellationToken.None);

        Task<DumpExitCode>? runningTask = null;

        try
        {
            while (await requestStream.MoveNext().ConfigureAwait(false))
            {
                var req = requestStream.Current;
                var taskId = string.IsNullOrWhiteSpace(req.TaskId) ? "single" : req.TaskId;

                switch (req.ActionCase)
                {
                    case CreateTaskRequest.ActionOneofCase.StartTask:
                        if (runningTask != null)
                        {
                            await outbox.Writer.WriteAsync(new CreateTaskResponse
                            {
                                TaskId = taskId,
                                Ack = new ServerAck { Kind = AckKind.Start, Ok = false, Message = "Task already started" }
                            }, ct).ConfigureAwait(false);
                            break;
                        }

                        await outbox.Writer.WriteAsync(new CreateTaskResponse
                        {
                            TaskId = taskId,
                            Ack = new ServerAck { Kind = AckKind.Start, Ok = true, Message = "Task starting" }
                        }, ct).ConfigureAwait(false);

                        runningTask = Task.Run(async () =>
                        {
                            // 任务开始前先发一条日志 + 进度状态
                            taskCtx.LogInfo($"Start task_type={req.StartTask.TaskType}", stage: "Start");
                            taskCtx.ReportProgress(new ProgressModel
                            {
                                Percent = 0,
                                State = ProgressStateModel.RUNNING
                            });

                            var task = new AdbBackupTask(taskCtx);
                            return await task.RunAsync(ct).ConfigureAwait(false);
                        }, CancellationToken.None);

                        break;

                    case CreateTaskRequest.ActionOneofCase.StopTask:
                        taskCtx.Cancel();
                        await outbox.Writer.WriteAsync(new CreateTaskResponse
                        {
                            TaskId = taskId,
                            Ack = new ServerAck { Kind = AckKind.Stop, Ok = true, Message = "Cancel requested" }
                        }, ct).ConfigureAwait(false);
                        break;

                    case CreateTaskRequest.ActionOneofCase.DialogResponse:
                        taskCtx.SetDialogResult(new DialogResponseModel(
                            req.DialogResponse.DialogId,
                            (int)req.DialogResponse.Result,
                            req.DialogResponse.PayloadJson ?? ""
                        ));

                        await outbox.Writer.WriteAsync(new CreateTaskResponse
                        {
                            TaskId = taskId,
                            Ack = new ServerAck { Kind = AckKind.Dialog, Ok = true, Message = "Dialog response received" }
                        }, ct).ConfigureAwait(false);
                        break;

                    case CreateTaskRequest.ActionOneofCase.Ping:
                        await outbox.Writer.WriteAsync(new CreateTaskResponse
                        {
                            TaskId = taskId,
                            Pong = new ServerPong
                            {
                                Seq = req.Ping.Seq,
                                Ts = Timestamp.FromDateTime(DateTime.UtcNow)
                            }
                        }, ct).ConfigureAwait(false);
                        break;
                }
            }
        }
        finally
        {
            taskCtx.Cancel();
            linkedCts.Cancel();

            if (runningTask != null)
            {
                DumpExitCode code;
                try { code = await runningTask.ConfigureAwait(false); }
                catch { code = DumpExitCode.Error; }

                // finished + 最终进度状态
                var finishCode = code switch
                {
                    DumpExitCode.Success => FinishCode.Success,
                    DumpExitCode.Cancel  => FinishCode.Cancel,
                    _                    => FinishCode.Error
                };

                taskCtx.ReportProgress(new ProgressModel
                {
                    Percent = (code == DumpExitCode.Success) ? 100 : 0,
                    State = code switch
                    {
                        DumpExitCode.Success => ProgressStateModel.SUCCESS,
                        DumpExitCode.Cancel  => ProgressStateModel.CANCEL,
                        _                    => ProgressStateModel.ERROR
                    }
                });

                try
                {
                    await outbox.Writer.WriteAsync(new CreateTaskResponse
                    {
                        TaskId = "single",
                        Finished = new TaskFinished
                        {
                            Code = finishCode,
                            Message = $"Finished with {code}",
                        }
                    }, CancellationToken.None).ConfigureAwait(false);
                }
                catch { }
            }

            // 关闭 outbox，收尾
            outbox.Writer.TryComplete();
            try { await Task.WhenAll(dialogLoop, eventLoop, progressLoop).ConfigureAwait(false); } catch { }
            try { await writer.ConfigureAwait(false); } catch { }
        }
    }
}