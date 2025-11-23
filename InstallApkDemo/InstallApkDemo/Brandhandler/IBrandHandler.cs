using InstallApkDemo.Model;
using SharpAdbClient;

namespace InstallApkDemo.Brandhandler;

public interface IBrandHandler
{
    // 1. 检测当前界面是否阻碍了安装 (对应你代码中的 DetermineLockType)
    InstallBlockingState DetectInstallBlockingState(DeviceData device, AdbClient client);

    // 2. 处理阻塞状态 (对应 HandleAccountRequiredAsync / HandCordsClick)
    // 返回值表示是否处理成功，如果处理失败可能需要走兜底
    Task<bool> HandleBlockingStateAsync(DeviceData device, AdbClient client, InstallBlockingState state, InstallModel model, CancellationToken token);

    // 3. 点击“安装完成”按钮 (对应 DismissInstallCompleteAsync)
    Task DismissInstallSuccessDialogAsync(DeviceData device, AdbClient client, CancellationToken token);

    // 4. 终极兜底方案 (对应 LaunchFileManagerAndClickAsync)
    // 小米可能是去打开“文件管理”，Oppo是“com.coloros.filemanager”
    Task ExecuteFallbackInstallAsync(DeviceData device, AdbClient client, InstallModel model, CancellationToken token);
}