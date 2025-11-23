namespace InstallApkDemo.Model;

// 定义安装过程中的状态
public enum InstallBlockingState
{
    None,           // 无阻塞，正常
    Success,        // 已经安装成功
    CordsClick,     // 需要纯坐标点击 (如 InstallGuideActivity)
    AccountRequired,// 需要输入账号密码 (如 Oppo的安全中心)
    UnknownBlock    // 未知阻塞
}