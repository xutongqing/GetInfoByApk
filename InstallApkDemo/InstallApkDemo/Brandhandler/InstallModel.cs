namespace InstallApkDemo.Brandhandler;

public class InstallModel
{
    public string ApkFile { get; set; }     // 本地 APK 路径
    public string PackageId { get; set; }   // 包名 (com.example.app)
    public string Brand { get; set; }       // 品牌 (方便调试)
}