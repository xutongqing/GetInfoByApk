using InstallApkDemo.Brandhandler;
using InstallApkDemo.Model;
using SharpAdbClient;

namespace InstallApkDemo.Strategies;

public class InstallerEngine
    {
        private readonly AdbClient _client;
        private readonly DeviceData _device;
        private readonly IBrandHandler _brandHandler; // 核心：持有当前品牌的处理器

        public InstallerEngine(DeviceData device, AdbClient client, IBrandHandler brandHandler)
        {
            _device = device;
            _client = client;
            _brandHandler = brandHandler;
        }

        // 这就是你原本的 Install() 方法，现在的逻辑非常干净
        public async Task InstallAsync(InstallModel installModel)
        {
            Console.WriteLine($"开始安装流程，策略: {installModel.Brand}");

            // 1. 统一用 CTS 控制 30s 超时
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var token = cts.Token;

            try
            {
                // 2. 启动并行任务
                // 任务 A: ADB 命令行安装 (纯后台)
                Task<bool> adbInstallTask = Task.Run(() => RunAdbInstall(_device, installModel.ApkFile), token);
                
                // 任务 B: UI 界面监控 (利用 BrandHandler 去看界面)
                Task<InstallBlockingState> uiWatchTask = WaitForUIAsync(installModel.PackageId, token);

                // 3. 竞速：看谁先出结果
                Task firstFinished = await Task.WhenAny(adbInstallTask, uiWatchTask).ConfigureAwait(false);

                // 4. 结果处理与取消
                if (firstFinished == uiWatchTask && uiWatchTask.Result == InstallBlockingState.OverTime)
                {
                    Console.WriteLine("安装等待超时 (UI Task Returns OverTime)");
                    cts.Cancel(); // 触发取消
                    TryKillAdbInstallProcess(); // 杀掉 ADB 进程
                    return;
                }

                cts.Cancel(); // 有一个完成了，取消另一个
                
                // 吸收取消异常
                try { await Task.WhenAll(adbInstallTask, uiWatchTask).ConfigureAwait(false); }
                catch { /* Ignore */ }

                // 获取状态
                bool adbSuccess = adbInstallTask.Status == TaskStatus.RanToCompletion && adbInstallTask.Result;
                InstallBlockingState uiState = uiWatchTask.Status == TaskStatus.RanToCompletion ? uiWatchTask.Result : InstallBlockingState.UnknownBlock;

                // ====== 核心分支判断 ======
                
                if (adbSuccess)
                {
                    Console.WriteLine("ADB 返回安装成功。");
                    // 二次校验
                    if (!_device.IsPackageInstalled(_client, installModel.PackageId))
                    {
                        Console.WriteLine("ADB 骗人，包不存在。执行文件管理器兜底。");
                        await _brandHandler.ExecuteFallbackInstallAsync(_device, _client, installModel);
                    }
                }
                else
                {
                    // ADB 失败，检查 UI 状态
                    Console.WriteLine($"ADB 未完成，UI 状态: {uiState}");

                    // 让 BrandHandler 尝试解决阻塞 (比如点击，或者回退)
                    bool handled = await _brandHandler.HandleBlockingStateAsync(_device, _client, uiState);

                    if (!handled)
                    {
                        // 如果 Handler 处理不了 (比如遇到需要密码)，则执行兜底
                        Console.WriteLine("常规手段无法安装，转入文件管理器兜底。");
                        await _brandHandler.ExecuteFallbackInstallAsync(_device, _client, installModel);
                    }
                }

                // 收尾
                await _brandHandler.DismissInstallSuccessDialogAsync(_device, _client);

            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("安装任务被整体取消/超时。");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"安装流程发生未捕获异常: {ex}");
            }
        }

        // --- 辅助方法 ---

        private async Task<InstallBlockingState> WaitForUIAsync(string packageId, CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                // 1. 成功检查
                if (_device.IsPackageInstalled(_client, packageId)) return InstallBlockingState.Success;

                // 2. 委托 BrandHandler 检查当前是不是卡住了
                // 这里的 DetectState 内部包含了你之前的 DetermineLockType 逻辑
                var state = _brandHandler.DetectState(_device, _client);
                
                if (state != InstallBlockingState.None && state != InstallBlockingState.UnknownBlock)
                {
                    // 发现明确的阻塞（要点击或要密码），立即返回
                    return state;
                }

                try { await Task.Delay(1000, token); } catch { break; }
            }
            return InstallBlockingState.None; // 或者 OverTime
        }

        private bool RunAdbInstall(DeviceData device, string apkPath)
        {
            // 你的 ADB 安装逻辑，包含 -g 重试
            try
            {
                var receiver = new ConsoleOutputReceiver();
                _client.ExecuteRemoteCommand($"pm install -r -g \"{apkPath}\"", device, receiver);
                if (receiver.ToString().Contains("Failure")) 
                {
                    // 重试逻辑...
                    return false;
                }
                return true;
            }
            catch { return false; }
        }

        private void TryKillAdbInstallProcess() { /* 实现 kill adb 逻辑 */ }
    }