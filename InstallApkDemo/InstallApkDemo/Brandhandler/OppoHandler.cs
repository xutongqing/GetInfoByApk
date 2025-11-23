using InstallApkDemo.Model;
using SharpAdbClient;

namespace InstallApkDemo.Brandhandler;

// ================== OPPO / ColorOS 处理器 ==================
    public class OppoHandler : IBrandHandler
    {
        // 将你原本的关键字移到这里，仅针对 Oppo 生效
        private static readonly string[] _cordsClickKeywords = { 
            "OPlusPackageInstallerActivity", "OppoPackageInstallerActivity", "InstallGuideActivity" 
        };
        private static readonly string[] _accountRequiredKeywords = { 
            "safecenter", "securitycenter" // 只有 Oppo/Vivo 会有这种恶心的安全中心
        };

        public InstallBlockingState DetectState(DeviceData device, AdbClient client)
        {
            // 获取当前 Activity (封装好的辅助方法见底部)
            string focus = AdbHelper.GetCurrentFocus(device, client);

            if (device.IsPackageInstalled(client, "target.package.id")) return InstallBlockingState.Success;

            if (_accountRequiredKeywords.Any(k => focus.Contains(k, StringComparison.OrdinalIgnoreCase)))
                return InstallBlockingState.AccountRequired;

            if (_cordsClickKeywords.Any(k => focus.Contains(k, StringComparison.OrdinalIgnoreCase)))
                return InstallBlockingState.CordsClick;

            return InstallBlockingState.None;
        }

        public async Task<bool> HandleBlockingStateAsync(DeviceData device, AdbClient client, InstallBlockingState state)
        {
            if (state == InstallBlockingState.AccountRequired)
            {
                Console.WriteLine("【Oppo】检测到账号密码拦截，尝试回退并准备兜底...");
                client.ExecuteRemoteCommand("input keyevent 4", device, null); // 按 Back 键
                return false; // 返回 false，告诉主程序去执行 Fallback
            }
            
            if (state == InstallBlockingState.CordsClick)
            {
                Console.WriteLine("【Oppo】执行坐标点击...");
                // TODO: 这里调用你原有的 HandCordsClick() 逻辑
                // AdbHelper.Tap(device, x, y);
                return true;
            }
            return false;
        }

        public async Task ExecuteFallbackInstallAsync(DeviceData device, AdbClient client, InstallModel model)
        {
            Console.WriteLine("【Oppo】启动 ColorOS 文件管理器兜底安装...");
            // 1. 启动文件管理器
            client.ExecuteRemoteCommand("monkey -p com.coloros.filemanager -c android.intent.category.LAUNCHER 1", device, null);
            await Task.Delay(1000);
            // 2. 执行后续点击寻找 APK 的逻辑 (你的 LaunchFileManagerAndClickAsync 逻辑)
        }

        public async Task DismissInstallSuccessDialogAsync(DeviceData device, AdbClient client)
        {
            // 实现针对 Oppo 的"完成"按钮查找与点击
        }
    }