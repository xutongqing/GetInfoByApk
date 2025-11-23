using InstallApkDemo.Brandhandler;
using InstallApkDemo.Strategies;
using SharpAdbClient;

Console.WriteLine("Hello, World!");

var client = new AdbClient();
// ... 启动服务代码省略 ...
        
// 预先加载配置 JSON
//BrandProfileFactory.LoadProfiles("Config/BrandConfig.json");

var devices = client.GetDevices();

foreach (var device in devices)
{
    Console.WriteLine($"=== 开始处理设备: {device.Name} ===");

    // Step 1: 获取 品牌处理器 (决定说什么话，UI怎么点)
    var brandHandler = BrandHandlerFactory.GetHandler(device, client);
            
    // Step 2: 获取 自动化策略 (决定用 Jar 还是 APK)
    var automationStrategy = StrategyFactory.GetStrategy(device, client);

    // --- 阶段 1: 安装前交互 ---
    brandHandler.ShowPreInstallInstructions(); // C# 弹窗/控制台提示
            
    if (!brandHandler.CheckPreConditions(device, client))
    {
        Console.WriteLine("预检查失败，跳过此设备。");
        continue;
    }

    // --- 阶段 2: 准备环境 (Strategy 负责) ---
    automationStrategy.PrepareEnvironment(device, client);

    // --- 阶段 3: 安装 APK ---
    brandHandler.OnInstallingAction(); // 安装时的额外提示
    try 
    {
        // Strategy 负责实际的 install 命令
        automationStrategy.InstallTargetApk(device, client, @"C:\Apks\MyTarget.apk");
    }
    catch(Exception ex)
    {
        Console.WriteLine($"安装失败: {ex.Message}。请检查品牌特定设置。");
        continue;
    }

    // --- 阶段 4: 自动化授权 ---
    Console.WriteLine("正在执行自动化授权...");
            
    // 这里是关键：我们将 BrandHandler 里的 Profile 数据传给了 Strategy
    // Strategy 负责把这些数据传给手机里的自动化脚本
    automationStrategy.ExecuteAutoGrant(
        device, 
        client, 
        "com.mycompany.targetapp"
    );

    // --- 阶段 5: 清理 ---
    automationStrategy.Cleanup(device, client);
            
    Console.WriteLine("=== 设备处理完成 ===");
}