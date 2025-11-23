using SharpAdbClient;

namespace InstallApkDemo.Brandhandler;

public static class BrandHandlerFactory
{
    public static IBrandHandler GetHandler(DeviceData device, AdbClient client)
    {
        // 获取品牌
        var receiver = new ConsoleOutputReceiver();
        client.ExecuteRemoteCommand("getprop ro.product.brand", device, receiver);
        string brand = receiver.ToString().Trim().ToLower();

        return brand switch
        {
            "oppo" => new OppoHandler(),
            "xiaomi" => new XiaomiHandler(),
            "redmi" => new XiaomiHandler(),
            // "huawei" => new HuaweiHandler(),
            _ => new OppoHandler() // 默认或者通用处理器
        };
    }
}

// 简单的扩展方法类，方便代码阅读
public static class AdbExtensions 
{
    public static bool IsPackageInstalled(this DeviceData device, AdbClient client, string packageId)
    {
        var receiver = new ConsoleOutputReceiver();
        client.ExecuteRemoteCommand($"pm list packages {packageId}", device, receiver);
        return receiver.ToString().Contains(packageId);
    }
}

public static class AdbHelper
{
    public static string GetCurrentFocus(DeviceData device, AdbClient client)
    {
        try
        {
            var r = new ConsoleOutputReceiver();
            // 注意：grep mCurrentFocus 在某些新 Android 上可能需要改用 dumpsys activity activities
            client.ExecuteRemoteCommand("dumpsys window | grep mCurrentFocus", device, r);
            return r.ToString();
        }
        catch { return ""; }
    }
}