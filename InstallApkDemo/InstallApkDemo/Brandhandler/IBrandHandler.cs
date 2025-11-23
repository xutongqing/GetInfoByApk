using SharpAdbClient;

namespace InstallApkDemo.Brandhandler;

public interface IBrandHandler
{
    // 1. 安装前的提示 (例如：提示用户去手机上改设置)
    void ShowPreInstallInstructions();

    // 2. 安装中可能需要的特殊 C# 逻辑 (例如：Oppo 需要在电脑端输入密码? 或者是单纯的等待)
    void OnInstallingAction();

    // 3. 授权前的检查 (例如：检查小米账号是否登录)
    bool CheckPreConditions(DeviceData device, AdbClient client);
}