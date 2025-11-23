using SharpAdbClient;

namespace InstallApkDemo.Strategies;

// 定义自动化安装及授权的通用接口
public interface IAutomationStrategy
{
    // 准备环境 (推送Jar 或 安装Helper APK)
    void PrepareEnvironment(DeviceData device, AdbClient client);

    // 安装目标APK
    void InstallTargetApk(DeviceData device, AdbClient client, string apkPath);

    // 执行自动化授权逻辑
    void ExecuteAutoGrant(DeviceData device, AdbClient client, string targetPackageName);

    // 清理环境 (删除临时文件等)
    void Cleanup(DeviceData device, AdbClient client);
}