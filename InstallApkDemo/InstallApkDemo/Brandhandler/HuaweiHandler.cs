using SharpAdbClient;

namespace InstallApkDemo.Brandhandler;

public class HuaweiHandler : IBrandHandler
{
    public void ShowPreInstallInstructions()
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine("【华为设备警告】检测到华为手机！");
        Console.WriteLine("请务必在手机设置中搜索'纯净模式'并【关闭】，否则安装将失败。");
        Console.WriteLine("请务必断开并重连 USB 线以确保授权弹窗已确认。");
        Console.ResetColor();
    }

    public void OnInstallingAction()
    {
        Console.WriteLine("【华为】正在安装... 如果手机弹出'外部来源应用'警告，请在手机上手动点击'允许'。");
    }

    public bool CheckPreConditions(DeviceData device, AdbClient client)
    {
        // 可以在这里写代码检查是否开启了某些特定设置
        return true;
    }
}