using IMV.Config;
using IMV.IO;
using IMV.ViewModels;
using System.ComponentModel;
using System.Windows;

namespace IMV.Views;

/// <summary>
/// メインウィンドウ
/// </summary>
public partial class MainWindow : Window
{
    private MainWindowViewModel _vm = new MainWindowViewModel();
    private ImageWindow? _imageWindow;

    /// <summary>
    /// コンストラクタ
    /// </summary>
    public MainWindow()
    {
        InitializeComponent();

        this.DataContext = _vm;
        this.thumbnailList.DataContext = _vm;

        // 設定の読み込み
        var config = IMVConfig.Shared.Load();

        // ウィンドウ状態を復元
        config.MainWindowInfo.Restore(this);

        if (!string.IsNullOrEmpty(config.SelectedTreePath))
            _vm.SelectedTreePath = config.SelectedTreePath;

        folderTreeColumn.Width = new GridLength(config.TreeColumnWidth);
    }

    /// <summary>
    /// ウィンドウクローズ時
    /// </summary>
    /// <param name="e">イベント引数</param>
    protected override void OnClosing(CancelEventArgs e)
    {
        _imageWindow?.Close();

        // 現在の状態を Config オブジェクトに格納
        var config = IMVConfig.Shared.Load();
        config.MainWindowInfo = WindowInfo.FromWindow(this);
        config.TreeColumnWidth = folderTreeColumn.ActualWidth;
        config.SelectedTreePath = string.IsNullOrEmpty(_vm.SelectedTreePath)
            ? ""
            : _vm.SelectedTreePath;
        IMVConfig.Shared.Save(config);

        // ユーザーコントロール開放
        folderTree.Dispose();
        thumbnailList.Dispose();

        // テンポラリファイルとフォルダ削除
        TempFileManager.Shared.Cleanup();

        base.OnClosing(e);
    }

    private async void OnThumbnailDoubleClick(object sender, RadianTools.UI.WPF.Controls.ThumbnailItemEventArgs e)
    {
        if (_imageWindow == null)
        {
            _imageWindow = new ImageWindow();

            _imageWindow.Closed += (_, _) =>
            {
                _imageWindow = null;
            };
        }

        _vm.ViewState.SelectedThumbnailIndex = e.Index;

        await _imageWindow.ShowImagesAsync(
            thumbnailList.Items,
            _vm.ViewState);

        if (!_imageWindow.IsVisible)
            _imageWindow.Show();

        _imageWindow.Activate();
    }

    private async void OnSelectedFolderChanged(object sender, RadianTools.UI.WPF.Controls.FolderItemEventArgs e)
    {
        if (_imageWindow == null)
            return;
        
        _vm.ViewState.SelectedThumbnailIndex = 0;

        await _imageWindow.ShowImagesAsync(
            thumbnailList.Items,
            _vm.ViewState);
    }

    private void thumbnailList_MouseRightButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (thumbnailList.SelectedIndex < 0)
            return;

        if (thumbnailList.ContextMenu == null)
            thumbnailList.ContextMenu = ImageContextMenu.Shared;

        var vm = thumbnailList.Items[thumbnailList.SelectedIndex];
        var menu = (ImageContextMenu)thumbnailList.ContextMenu!;
        menu.Show(vm);
    }
}
