using InstallApkDemo.Brandhandler;
using InstallApkDemo.Model;
using SharpAdbClient;

namespace InstallApkDemo.Strategies;

public class RobustAutomationStrategy
    {
        // 核心安装方法
        public async Task Install(DeviceData device, AdbClient client, InstallModel installModel, IBrandHandler brandHandler)
        {
            // 统一用 CTS 控制 30s 超时
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var token = cts.Token;

            try
            {
                // 1. 启动并行任务：ADB安装 vs UI监控
                // 注意：这里调用的是内部的 RunAdbInstallAsync
                Task<bool> installTask = Task.Run(() => RunAdbInstall(device, client, installModel.ApkFile), token);
                
                // 注意：WaitForUI 是一个利用 BrandHandler 进行循环检测的方法
                Task<InstallBlockingState> waitTask = WaitForUIAsync(device, client, installModel.PackageId, brandHandler, token);

                // 2. 竞速：看谁先完成
                Task first = await Task.WhenAny(installTask, waitTask).ConfigureAwait(false);

                // 3. 取消未完成的任务
                cts.Cancel(); // 触发取消
                try { await Task.WhenAll(installTask, waitTask).ConfigureAwait(false); }
                catch { /* 忽略取消异常 */ }

                bool installSucceeded = installTask.Status == TaskStatus.RanToCompletion && installTask.Result;
                InstallBlockingState uiState = waitTask.Status == TaskStatus.RanToCompletion ? waitTask.Result : InstallBlockingState.UnknownBlock;

                // ====== 主逻辑分发 ======
                if (installSucceeded)
                {
                    Console.WriteLine("ADB 返回安装成功。");
                    // 二次校验逻辑（保留你原有的 CheckPackagePresent）
                    if (!CheckPackagePresent(device, client, installModel.PackageId))
                    {
                         // 即使ADB返回成功，如果包不在，尝试品牌特定的兜底安装
                         Console.WriteLine("校验失败，执行品牌兜底安装...");
                         await brandHandler.ExecuteFallbackInstallAsync(device, client, installModel, CancellationToken.None);
                    }
                }
                else
                {
                    // ADB 安装失败，根据 UI 状态由 BrandHandler 处理
                    Console.WriteLine($"ADB安装未完成，UI状态: {uiState}");
                    
                    // 委托给 Handler 处理各种奇葩弹窗
                    bool handled = await brandHandler.HandleBlockingStateAsync(device, client, uiState, installModel, CancellationToken.None);
                    
                    if (!handled)
                    {
                        // 如果 Handler 也没搞定，最后尝试一次文件管理器兜底
                        await brandHandler.ExecuteFallbackInstallAsync(device, client, installModel, CancellationToken.None);
                    }
                }

                // ====== 收尾 ======
                await brandHandler.DismissInstallSuccessDialogAsync(device, client, CancellationToken.None);

            }
            catch (Exception ex)
            {
                Console.WriteLine($"安装流程异常: {ex.Message}");
            }
        }

        // --- 辅助方法 ---

        private bool RunAdbInstall(DeviceData device, AdbClient client, string apkPath)
        {
            // 保留你原有的 -g 重试逻辑
            try
            {
                var output = new ConsoleOutputReceiver();
                client.ExecuteRemoteCommand($"pm install -r -g \"{apkPath}\"", device, output);
                string resp = output.ToString();
                
                if (resp.Contains("INSTALL_GRANT_RUNTIME_PERMISSIONS")) // 权限错误重试
                {
                     output = new ConsoleOutputReceiver();
                     client.ExecuteRemoteCommand($"pm install -r \"{apkPath}\"", device, output);
                     resp = output.ToString();
                }
                return resp.ToLower().Contains("success");
            }
            catch { return false; }
        }

        private async Task<InstallBlockingState> WaitForUIAsync(DeviceData device, AdbClient client, string pkgId, IBrandHandler handler, CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                // 1. 检查包是否已存在（成功）
                if (CheckPackagePresent(device, client, pkgId)) return InstallBlockingState.Success;

                // 2. 让 Handler 检查当前是否有弹窗
                var state = handler.DetectInstallBlockingState(device, client);
                if (state != InstallBlockingState.None && state != InstallBlockingState.UnknownBlock)
                {
                    return state; // 发现阻塞（如输入账号），立即返回让主线程处理
                }

                await Task.Delay(1000, token);
            }
            return InstallBlockingState.UnknownBlock;
        }

        private bool CheckPackagePresent(DeviceData device, AdbClient client, string pkgId)
        {
            // 简单封装 SharpAdbClient 的包检查
            // 实际可以使用 pm list packages | grep pkgId
            return true; // 模拟实现
        }
    }