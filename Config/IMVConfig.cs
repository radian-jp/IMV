namespace IMV.Config;

using IMV.Views;

public class IMVConfig
{
    public static JsonConfigSerializer<IMVConfig> Shared = new();

    // MainWindow設定
    public WindowInfo MainWindowInfo { get; set; } = new WindowInfo();
    public double TreeColumnWidth { get; set; } = 250;
    public string SelectedTreePath { get; set; } = "";

    // ImageWindow設定
    public WindowInfo ImageWindowInfo { get; set; } = new WindowInfo();
    public ImageWindowPageMode ImageWindowPageMode { get; set; } = ImageWindowPageMode.Double;
}
