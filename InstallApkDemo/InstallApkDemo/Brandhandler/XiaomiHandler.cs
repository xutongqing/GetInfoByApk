using InstallApkDemo.Model;
using SharpAdbClient;

namespace InstallApkDemo.Brandhandler;

// ================== Xiaomi / MIUI 处理器 ==================
public class XiaomiHandler : IBrandHandler
{
    public InstallBlockingState DetectState(DeviceData device, AdbClient client)
    {
        string focus = AdbHelper.GetCurrentFocus(device, client);
        // 小米的安装器通常叫 com.miui.packageinstaller
        if (focus.Contains("com.miui.packageinstaller", StringComparison.OrdinalIgnoreCase))
            return InstallBlockingState.CordsClick;

        return InstallBlockingState.None;
    }

    public async Task<bool> HandleBlockingStateAsync(DeviceData device, AdbClient client, InstallBlockingState state)
    {
        if (state == InstallBlockingState.CordsClick)
        {
            Console.WriteLine("【Xiaomi】检测到安装弹窗，等待倒计时...");
            // 小米通常有倒计时，这里可以 Sleep 一下再点
            await Task.Delay(10000); 
            // AdbHelper.Tap(device, x, y);
            return true;
        }
        return false;
    }

    public async Task ExecuteFallbackInstallAsync(DeviceData device, AdbClient client, InstallModel model)
    {
        Console.WriteLine("【Xiaomi】启动 MIUI 文件管理器兜底...");
        client.ExecuteRemoteCommand("monkey -p com.android.fileexplorer -c android.intent.category.LAUNCHER 1", device, null);
    }

    public async Task DismissInstallSuccessDialogAsync(DeviceData device, AdbClient client) { /*...*/ }
}