using System;
using System.Threading;
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
        var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(taskCtx.Token, context.CancellationToken);

        // 写循环：把 DialogRequest 持续推给客户端
        var writeDialogLoop = Task.Run(async () =>
        {
            try
            {
                await foreach (var dlg in taskCtx.ReadDialogsAsync(linkedCts.Token))
                {
                    var resp = new CreateTaskResponse
                    {
                        TaskId = "single", // 你一个进程一个任务就写死也行
                        DialogRequest = new DialogRequest
                        {
                            DialogId = dlg.DialogId,
                            Kind = (DialogKind)dlg.Kind,
                            Title = dlg.Title,
                            Message = dlg.Message,
                            PayloadJson = dlg.PayloadJson ?? "",
                            TimeoutSec = dlg.TimeoutSec
                        }
                    };
                    await responseStream.WriteAsync(resp).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) { }
        }, CancellationToken.None);

        Task<DumpExitCode>? runningTask = null;

        try
        {
            // 读循环：读取客户端指令
            while (await requestStream.MoveNext().ConfigureAwait(false))
            {
                var req = requestStream.Current;

                switch (req.ActionCase)
                {
                    case CreateTaskRequest.ActionOneofCase.StartTask:
                        if (runningTask != null)
                        {
                            await responseStream.WriteAsync(new CreateTaskResponse
                            {
                                TaskId = req.TaskId,
                                Ack = new ServerAck { Kind = AckKind.Start, Ok = false, Message = "Task already started" }
                            }).ConfigureAwait(false);
                            break;
                        }

                        await responseStream.WriteAsync(new CreateTaskResponse
                        {
                            TaskId = req.TaskId,
                            Ack = new ServerAck { Kind = AckKind.Start, Ok = true, Message = "Task starting" }
                        }).ConfigureAwait(false);

                        runningTask = Task.Run(async () =>
                        {
                            // 这里你可以按 task_type 做工厂/反射/DI
                            var task = new AdbBackupTask(taskCtx);

                            // 可选：发一些事件
                            await responseStream.WriteAsync(new CreateTaskResponse
                            {
                                TaskId = req.TaskId,
                                Event = new TaskEvent
                                {
                                    Ts = Timestamp.FromDateTime(DateTime.UtcNow),
                                    Level = EventLevel.Error,
                                    Message = $"Start task_type={req.StartTask.TaskType}"
                                }
                            }).ConfigureAwait(false);

                            return await task.RunAsync(linkedCts.Token).ConfigureAwait(false);
                        }, CancellationToken.None);

                        break;

                    case CreateTaskRequest.ActionOneofCase.StopTask:
                        taskCtx.Cancel();
                        await responseStream.WriteAsync(new CreateTaskResponse
                        {
                            TaskId = req.TaskId,
                            Ack = new ServerAck { Kind = AckKind.Stop, Ok = true, Message = "Cancel requested" }
                        }).ConfigureAwait(false);
                        break;

                    case CreateTaskRequest.ActionOneofCase.DialogResponse:
                        taskCtx.SetDialogResult(new DialogResponseModel(
                            req.DialogResponse.DialogId,
                            (int)req.DialogResponse.Result,
                            req.DialogResponse.PayloadJson ?? ""
                        ));

                        // 可选 ack
                        await responseStream.WriteAsync(new CreateTaskResponse
                        {
                            TaskId = req.TaskId,
                            Ack = new ServerAck { Kind = AckKind.Dialog, Ok = true, Message = "Dialog response received" }
                        }).ConfigureAwait(false);
                        break;

                    case CreateTaskRequest.ActionOneofCase.Ping:
                        Console.WriteLine("1321321312");
                        await responseStream.WriteAsync(new CreateTaskResponse
                        {
                            TaskId = req.TaskId,
                            Pong = new ServerPong { Seq = req.Ping.Seq, Ts = Timestamp.FromDateTime(DateTime.UtcNow) }
                        }).ConfigureAwait(false);
                        break;

                    default:
                        break;
                }
            }
        }
        finally
        {
            // 客户端断开/流结束：请求取消
            taskCtx.Cancel();
            linkedCts.Cancel();

            if (runningTask != null)
            {
                DumpExitCode code;
                try { code = await runningTask.ConfigureAwait(false); }
                catch { code = DumpExitCode.Error; }

                // 尝试发送 finished（如果流还可写）
                try
                {
                    await responseStream.WriteAsync(new CreateTaskResponse
                    {
                        TaskId = "single",
                        Finished = new TaskFinished
                        {
                            Code = code switch
                            {
                                DumpExitCode.Success => FinishCode.Success,
                                DumpExitCode.Cancel  => FinishCode.Cancel,
                                _                    => FinishCode.Error
                            },
                            Message = $"Finished with {code}"
                        }
                    }).ConfigureAwait(false);
                }
                catch { }
            }

            try { await writeDialogLoop.ConfigureAwait(false); } catch { }
            linkedCts.Dispose();
        }
    }
}
