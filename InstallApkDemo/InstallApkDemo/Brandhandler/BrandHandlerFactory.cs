using SharpAdbClient;

namespace InstallApkDemo.Brandhandler;

public static class BrandHandlerFactory
{
    public static IBrandHandler GetHandler(DeviceData device, AdbClient client)
    {
        // 1. 获取品牌
        var receiver = new ConsoleOutputReceiver();
        client.ExecuteRemoteCommand("getprop ro.product.brand", device, receiver);
        string brand = receiver.ToString().Trim().ToLower();

        // 2. 从 JSON 加载配置数据 (复用上一轮对话的 ProfileFactory)
        //var profile = BrandProfileFactory.GetProfile(brand);

        // 3. 返回对应的 Handler 类
        switch (brand)
        {
            case "huawei":
            case "honor": // 荣耀通常和华为逻辑类似
                return new HuaweiHandler();
                
            case "xiaomi":
            case "redmi":
                return new XiaomiHandler();
                
            case "oppo":
            // return new OppoHandler(profile);
                
            case "vivo":
            // return new VivoHandler(profile);

            default:
                return new DefaultBrandHandler();
        }
    }
}