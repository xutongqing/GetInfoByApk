using SharpAdbClient;

namespace InstallApkDemo.Brandhandler;

public class DefaultBrandHandler : IBrandHandler
{
    public void ShowPreInstallInstructions()
    {
        Console.WriteLine("【通用设备】准备开始安装，请保持屏幕常亮。");
    }

    public void OnInstallingAction() { /* 默认无动作 */ }

    public bool CheckPreConditions(DeviceData device, AdbClient client) { return true; }
}