using SharpAdbClient;

namespace InstallApkDemo.Strategies;

public static class StrategyFactory
{
    public static IAutomationStrategy GetStrategy(DeviceData device, AdbClient client)
    {
        // 1. 获取安卓版本
        // ro.build.version.release 返回如 "10", "11", "4.4.4"
        var versionReceiver = new ConsoleOutputReceiver();
        client.ExecuteRemoteCommand("getprop ro.build.version.release", device, versionReceiver);
        string versionStr = versionReceiver.ToString().Trim();
            
        // 简单解析版本号
        double version = 0;
        if (!double.TryParse(versionStr.Split('.')[0], out version))
        {
            version = 0; // 解析失败兜底
        }

        // 2. 获取厂商 (如果需要更细分的策略，比如华为专用策略)
        var brandReceiver = new ConsoleOutputReceiver();
        client.ExecuteRemoteCommand("getprop ro.product.brand", device, brandReceiver);
        string brand = brandReceiver.ToString().Trim().ToLower();

        Console.WriteLine($"检测到设备: {brand} - Android {versionStr}");

        // 3. 决策逻辑
        if (version >= 11)
        {
            // Android 11+ 使用 APK 辅助模式
            return new ModernAutomationStrategy();
        }

        // Android 11 以下使用 Jar 注入模式
        return new LegacyAutomationStrategy();
    }
}