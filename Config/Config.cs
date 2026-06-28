namespace IMV.Config;

public class Config
{
    public static JsonConfigSerializer<Config> Shared = new();

    public double? WindowLeft { get; set; } = null;
    public double? WindowTop { get; set; } = null;
    public double WindowWidth { get; set; } = 1000;
    public double WindowHeight { get; set; } = 600;
    public System.Windows.WindowState WindowState { get; set; } = System.Windows.WindowState.Normal;

    public double TreeColumnWidth { get; set; } = 250;
    public string SelectedTreePath { get; set; } = "";
}
