using IMV.ViewModels;
using RadianTools.UI.WPF.ViewModels;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using RadianTools.UI.WPF.IO;

namespace IMV.Views;

public partial class MainWindow : Window
{
    private MainWindowViewModel _vm = new MainWindowViewModel();
    private ImageWindow? _imageWindow;

    public MainWindow()
    {
        InitializeComponent();

        this.DataContext = _vm;
        this.thumbnailList.DataContext = _vm;

        // デスクトップの作業領域を取得（タスクバー分を引いた領域）
        var workArea = SystemParameters.WorkArea;

        // 設定の読み込み
        var config = Config.Config.Shared.Load();

        // --- ウィンドウサイズの決定 ---
        double targetWidth = Math.Min(config.WindowWidth, workArea.Width);
        double targetHeight = Math.Min(config.WindowHeight, workArea.Height);

        this.Width = targetWidth;
        this.Height = targetHeight;

        // --- ウィンドウ位置の決定 ---
        if (config.WindowLeft.HasValue && config.WindowTop.HasValue)
        {
            double targetLeft = config.WindowLeft.Value;
            double targetTop = config.WindowTop.Value;

            // はみ出し補正
            // 右端/下端を計算（ウィンドウが画面から完全に消えないように 100px 残す）
            double maxLeft = workArea.Right - 100;
            double maxTop = workArea.Bottom - 100;

            this.Left = Math.Clamp(targetLeft, workArea.Left, maxLeft);
            this.Top = Math.Clamp(targetTop, workArea.Top, maxTop);

            this.WindowStartupLocation = WindowStartupLocation.Manual;
        }
        else
        {
            // 初回起動時は中央へ
            this.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }

        // ウィンドウ状態を復元
        this.WindowState = config.WindowState;

        if (!string.IsNullOrEmpty(config.SelectedTreePath))
            _vm.SelectedTreePath = config.SelectedTreePath;

        folderTreeColumn.Width = new GridLength(config.TreeColumnWidth);
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        // 保存する前に、現在の状態をチェック
        var state = this.WindowState;

        // 最大化しているときは「復元時のサイズ(this.RestoreBounds)」を保存する
        var bounds = (state == WindowState.Maximized) 
            ? this.RestoreBounds
            : new Rect(this.Left, this.Top, this.Width, this.Height);

        // 現在の状態を Config オブジェクトに格納
        var config = new Config.Config
        {
            WindowWidth = bounds.Width,
            WindowHeight = bounds.Height,
            WindowLeft = Math.Max(0, bounds.Left),
            WindowTop = Math.Max(0, bounds.Top),
            WindowState = state,
            TreeColumnWidth = folderTreeColumn.ActualWidth,
            SelectedTreePath = string.IsNullOrEmpty(_vm.SelectedTreePath)
                ? ""
                : _vm.SelectedTreePath
        };

        // 保存実行
        Config.Config.Shared.Save(config);

        base.OnClosing(e);
    }

    private async void thumbnailList_ItemDoubleClick(object sender, RadianTools.UI.WPF.Controls.ThumbnailItemEventArgs e)
    {
        if (_imageWindow == null)
        {
            _imageWindow = new ImageWindow();

            _imageWindow.Closed += (_, _) =>
            {
                _imageWindow = null;
            };
        }

        await _imageWindow.ShowImagesAsync(
            thumbnailList.Items,
            e.Index);

        if (!_imageWindow.IsVisible)
            _imageWindow.Show();

        _imageWindow.Activate();
    }
}
