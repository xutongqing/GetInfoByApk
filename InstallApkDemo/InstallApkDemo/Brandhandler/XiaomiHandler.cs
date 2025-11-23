using SharpAdbClient;

namespace InstallApkDemo.Brandhandler;

public class XiaomiHandler : IBrandHandler
{
    public void ShowPreInstallInstructions()
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("【小米设备提示】");
        Console.WriteLine("1. 请确保手机已插入 SIM 卡（开启USB安装权限必须）。");
        Console.WriteLine("2. 开发者选项中，请开启【USB安装】和【USB调试(安全设置)】。");
        Console.ResetColor();
    }

    public void OnInstallingAction()
    {
        Console.WriteLine("【小米】安装中... 请注意手机屏幕可能会有倒计时弹窗，请准备点击。");
    }

    public bool CheckPreConditions(DeviceData device, AdbClient client)
    {
        // 小米如果没开USB安装，install命令会报错，这里可以预检
        // 模拟检查逻辑
        Console.WriteLine("【小米】正在检查 SIM 卡状态...");
        return true;
    }
}