using InstallApkDemo.Brandhandler;
using InstallApkDemo.Strategies;
using SharpAdbClient;

Console.WriteLine("Hello, World!");

var client = new AdbClient();
var device = client.GetDevices().FirstOrDefault();
if (device == null) return;

// 1. 准备数据
var model = new InstallModel 
{ 
    ApkFile = @"C:\Apps\Target.apk", 
    PackageId = "com.example.app" 
};

// 2. 获取该设备的专属处理器 (工厂模式)
// 如果是 Oppo 手机，这里得到的就是 OppoHandler
IBrandHandler handler = BrandHandlerFactory.GetHandler(device, client);
model.Brand = handler.GetType().Name;

// 3. 初始化安装引擎 (传入 Handler)
var engine = new InstallerEngine(device, client, handler);

// 4. 执行安装 (内部包含 Task.WhenAny 竞速逻辑)
await engine.InstallAsync(model);

Console.WriteLine("流程结束");
Console.ReadKey();