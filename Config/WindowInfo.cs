namespace IMV.Config;

using System;
using System.Windows;

public class WindowInfo
{
    public double? Left { get; set; } = null;
    public double? Top { get; set; } = null;
    public double Width { get; set; } = 1000;
    public double Height { get; set; } = 600;
    public WindowState State { get; set; } = WindowState.Normal;

    public static WindowInfo FromWindow(Window window)
    {
        // 保存する前に、現在の状態をチェック
        var state = window.WindowState;

        // 最大化しているときは「復元時のサイズ(this.RestoreBounds)」を保存する
        var bounds = (state == WindowState.Maximized)
            ? window.RestoreBounds
            : new Rect(window.Left, window.Top, window.Width, window.Height);

        // 現在の状態を格納
        var info = new WindowInfo()
        {
            Width = bounds.Width,
            Height = bounds.Height,
            Left = Math.Max(0, bounds.Left),
            Top = Math.Max(0, bounds.Top),
            State = state
        };

        return info;
    }

    public void Restore(Window window)
    {
        // デスクトップの作業領域を取得（タスクバー分を引いた領域）
        var workArea = SystemParameters.WorkArea;

        // --- ウィンドウサイズの決定 ---
        double targetWidth = Math.Min(Width, workArea.Width);
        double targetHeight = Math.Min(Height, workArea.Height);

        window.Width = targetWidth;
        window.Height = targetHeight;

        // --- ウィンドウ位置の決定 ---
        if (Left.HasValue && Top.HasValue)
        {
            double targetLeft = Left.Value;
            double targetTop = Top.Value;

            // はみ出し補正
            // 右端/下端を計算（ウィンドウが画面から完全に消えないように 100px 残す）
            double maxLeft = workArea.Right - 100;
            double maxTop = workArea.Bottom - 100;

            window.Left = Math.Clamp(targetLeft, workArea.Left, maxLeft);
            window.Top = Math.Clamp(targetTop, workArea.Top, maxTop);

            window.WindowStartupLocation = WindowStartupLocation.Manual;
        }
        else
        {
            // 初回起動時は中央へ
            window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }

        // ウィンドウ状態を復元
        window.WindowState = State;
    }
}
