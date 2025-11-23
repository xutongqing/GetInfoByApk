using SharpAdbClient;

namespace InstallApkDemo.Strategies;

public class LegacyAutomationStrategy : IAutomationStrategy
{
    private const string JarLocalPath = "./Assets/AutoGrant.jar"; // 本地Jar路径
    private const string JarRemotePath = "/data/local/tmp/AutoGrant.jar";

    public void PrepareEnvironment(DeviceData device, AdbClient client)
    {
        Console.WriteLine($"[Legacy] 推送自动化Jar包到设备: {device.Name}");
        using (var service = new SyncService(client, device))
        {
            using (var stream = System.IO.File.OpenRead(JarLocalPath))
            {
                service.Push(stream, JarRemotePath, 777, DateTime.Now, null, System.Threading.CancellationToken.None);
            }
        }
    }

    public void InstallTargetApk(DeviceData device, AdbClient client, string apkPath)
    {
        Console.WriteLine("[Legacy] 安装目标APK...");
        // 普通安装，-r 覆盖安装
        using (var stream = System.IO.File.OpenRead(apkPath))
        {
            client.Install(device, stream, "-r");
        }
    }

    public void ExecuteAutoGrant(DeviceData device, AdbClient client, string targetPackageName)
    {
        Console.WriteLine("[Legacy] 执行 app_process 自动化脚本...");
            
        // 这里通过 nohup 或者直接执行命令来启动 Jar 包中的 main 方法
        // 该 Jar 包内部使用 uiautomator 1.0/2.0 逻辑去点击屏幕
        // 参数传入目标包名，以便 Jar 知道要给谁点权限
        var command = $"CLASSPATH={JarRemotePath} app_process /data/local/tmp com.example.autogrant.Main \"{targetPackageName}\"";
            
        var receiver = new ConsoleOutputReceiver();
        client.ExecuteRemoteCommand(command, device, receiver);
            
        Console.WriteLine($"[Legacy] 脚本输出: {receiver.ToString()}");
    }

    public void Cleanup(DeviceData device, AdbClient client)
    {
        client.ExecuteRemoteCommand($"rm {JarRemotePath}", device, null);
    }
}