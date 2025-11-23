using SharpAdbClient;

namespace InstallApkDemo.Strategies;

public class ModernAutomationStrategy : IAutomationStrategy
    {
        private const string HelperApkPath = "./Assets/HelperApp.apk";
        private const string HelperPackageName = "com.mycompany.helper";

        public void PrepareEnvironment(DeviceData device, AdbClient client)
        {
            Console.WriteLine("[Modern] 安装辅助自动化APK...");
            // 1. 安装辅助APK
            using (var stream = System.IO.File.OpenRead(HelperApkPath))
            {
                // -g 参数在安装辅助APK时直接授予它运行时权限（如果可能）
                // -t 允许测试包
                client.Install(device, stream, "-r", "-t", "-g"); 
            }

            // 2. 这一步非常关键：通过ADB直接开启辅助APK的无障碍服务权限
            // 这样辅助APK就不需要人工去点“开启服务”了
            string enableServiceCmd = $"settings put secure enabled_accessibility_services {HelperPackageName}/{HelperPackageName}.MyAccessibilityService";
            client.ExecuteRemoteCommand(enableServiceCmd, device, null);
            
            string enableAccessibilityCmd = "settings put secure accessibility_enabled 1";
            client.ExecuteRemoteCommand(enableAccessibilityCmd, device, null);
        }

        public void InstallTargetApk(DeviceData device, AdbClient client, string apkPath)
        {
            Console.WriteLine("[Modern] 安装目标APK (此时辅助服务应在后台监听)...");
            // 在安装过程中，Android 11+ 可能会弹出极其严格的确认框
            // 此时辅助APK的无障碍服务需要监听 "Package Installer" 的事件并自动点击
            using (var stream = System.IO.File.OpenRead(apkPath))
            {
                client.Install(device, stream, "-r");
            }
        }

        public void ExecuteAutoGrant(DeviceData device, AdbClient client, string targetPackageName)
        {
            Console.WriteLine("[Modern] 发送广播通知辅助APK开始执行应用内权限点击...");
            // 安装完成后，启动目标APP，辅助APK检测到目标APP启动并请求权限时，自动点击“允许”
            
            // 也可以发送一个Intent给辅助APK，告诉它：“我现在要搞这个包了，准备好”
            client.ExecuteRemoteCommand($"am startservice -a com.mycompany.ACTION_START_WATCH --es target_package {targetPackageName} {HelperPackageName}/.CommandService", device, null);
            
            // 启动目标APP
            client.ExecuteRemoteCommand($"monkey -p {targetPackageName} -c android.intent.category.LAUNCHER 1", device, null);
            
            // 等待自动化执行...
            Thread.Sleep(5000); 
        }

        public void Cleanup(DeviceData device, AdbClient client)
        {
            // 可选：卸载辅助APK
            // client.UninstallDevice(device, HelperPackageName);
        }
    }