namespace IMV.Views;

using IMV.Config;
using IMV.State;
using RadianTools.UI.WPF.Imaging;
using RadianTools.UI.WPF.IO;
using RadianTools.UI.WPF.ViewModels;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

public enum ImageWindowPageMode
{
    Single = 1,
    Double = 2
}

public enum ImageWindowPageOrder
{
    RightToLeft,
    LeftToRight
}

public partial class ImageWindow : Window
{
    private IReadOnlyList<ThumbnailItemViewModel> _items = Array.Empty<ThumbnailItemViewModel>();
    private ImageWindowPageMode _pageMode = ImageWindowPageMode.Double;
    private int _loadVersion;
    private CancellationTokenSource? _loadCts;
    private ImageViewState? _imageViewState;

    private int SelectedIndex
    {
        get => _imageViewState!.SelectedThumbnailIndex;
        set => _imageViewState!.SelectedThumbnailIndex = value;
    }

    public ImageWindow()
    {
        InitializeComponent();

        // 設定の読み込み
        var config = IMVConfig.Shared.Load();

        // ウィンドウ状態を復元
        config.ImageWindowInfo.Restore(this);
        _pageMode = config.ImageWindowPageMode;
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        // 現在の状態を Config オブジェクトに格納
        var config = IMVConfig.Shared.Load();
        config.ImageWindowInfo = WindowInfo.FromWindow(this);
        config.ImageWindowPageMode = _pageMode;
        IMVConfig.Shared.Save(config);

        base.OnClosing(e);
    }

    /// <summary>
    /// 外部から表示開始
    /// </summary>
    public async Task ShowImagesAsync(
        IReadOnlyList<ThumbnailItemViewModel> items,
        ImageViewState imageViewState)
    {
        _items = items ?? Array.Empty<ThumbnailItemViewModel>();
        _imageViewState = imageViewState;

        if (_items.Count == 0)
        {
            SelectedIndex = 0;
            await ClearAsync();
            UpdateTitle(null, null);
            return;
        }

        ApplyLayout();

        await UpdateImagesAsync();
    }

    /// <summary>
    /// 画像更新
    /// </summary>
    private async Task UpdateImagesAsync()
    {
        var token = _loadCts?.Token ?? CancellationToken.None;
        var version = _loadVersion;

        var right = GetItem(SelectedIndex);
        var left = GetItem(SelectedIndex + 1);

        UpdateTitle(right, left, true);

        var rightTask = LoadImageAsync(right, token);
        var leftTask = _pageMode == ImageWindowPageMode.Double
            ? LoadImageAsync(left, token)
            : Task.FromResult<ImageSource?>(null);

        var rightImg = await rightTask;
        var leftImg = await leftTask;

        // 古い結果なら破棄
        if (version != _loadVersion)
            return;

        RightImage.Source = rightImg;

        if (_pageMode == ImageWindowPageMode.Double)
            LeftImage.Source = leftImg;
        else
            LeftImage.Source = null;

        UpdateTitle(right, left);
    }

    /// <summary>
    /// レイアウト反映
    /// </summary>
    private void ApplyLayout()
    {
        switch (_pageMode)
        {
            case ImageWindowPageMode.Single:
                LeftCol.Width = new GridLength(0);
                break;

            case ImageWindowPageMode.Double:
                LeftCol.Width = new GridLength(1, GridUnitType.Star);
                break;
        }
    }

    /// <summary>
    /// タイトル更新
    /// </summary>
    private void UpdateTitle(
        ThumbnailItemViewModel? right,
        ThumbnailItemViewModel? left,
        bool loading = false)
    {
        var total = _items.Count;
        var page = _items.Count == 0 ? 0 : SelectedIndex + 1;
        var strLoading = loading ? " (Loading...)" : "";

        string filePart;
        if (_pageMode == ImageWindowPageMode.Double)
        {
            filePart = $"L:{left?.DisplayName} R:{right?.DisplayName}";
        }
        else
        {
            filePart = $"{right?.DisplayName}";
        }

        Title = $"({page} / {total}) {filePart}{strLoading}";
    }

    /// <summary>
    /// 表示クリア
    /// </summary>
    private Task ClearAsync()
    {
        LeftImage.Source = null;
        RightImage.Source = null;
        return Task.CompletedTask;
    }

    /// <summary>
    /// 次ページ移動
    /// </summary>
    private async Task NextAsync()
    {
        if (_items.Count == 0)
            return;

        var diff = (int)_pageMode;
        await SetPageIndexAsync(SelectedIndex + diff);
    }

    /// <summary>
    /// 前ページ移動
    /// </summary>
    private async Task PrevAsync()
    {
        var diff = (int)_pageMode;
        await SetPageIndexAsync(SelectedIndex - diff);
    }

    private async Task SetPageIndexAsync(int index)
    {
        if (_items.Count == 0)
            return;

        SelectedIndex = ClampPageIndex(index);
        StartNewLoad();
        await UpdateImagesAsync();
    }

    private void StartNewLoad()
    {
        Interlocked.Increment(ref _loadVersion);

        _loadCts?.Cancel();
        _loadCts?.Dispose();
        _loadCts = new CancellationTokenSource();
    }

    /// <summary>
    /// キーボード操作
    /// </summary>
    private async void ImageWindow_KeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Right:
                await NextAsync();
                break;

            case Key.Left:
                await PrevAsync();
                break;

            case Key.Escape:
                Close();
                break;

            case Key.D1:
                _pageMode = ImageWindowPageMode.Single;
                ApplyLayout();
                await UpdateImagesAsync();
                break;

            case Key.D2:
                _pageMode = ImageWindowPageMode.Double;
                ApplyLayout();
                await UpdateImagesAsync();
                break;
        }
    }

    /// <summary>
    /// ホイール操作
    /// </summary>
    private async void ImageWindow_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (e.Delta > 0)
            await PrevAsync();
        else
            await NextAsync();

        e.Handled = true;
    }

    /// <summary>
    /// インデックス安全取得
    /// </summary>
    private ThumbnailItemViewModel? GetItem(int index)
    {
        if ((uint)index >= (uint)_items.Count)
            return null;

        return _items[index];
    }

    /// <summary>
    /// ページインデックスを指定可能範囲内の値にする
    /// </summary>
    private int ClampPageIndex(int index)
    {
        if (_items.Count == 0)
            return 0;

        if (index < 0)
            return 0;

        var last = _items.Count - 1;
        if (index > last)
            return last;

        return index;
    }

    private static IImageFactory _imageFactory = RsImageFactory.Shared;

    /// <summary>
    /// 画像ロード
    /// </summary>
    private async Task<ImageSource?> LoadImageAsync(
        ThumbnailItemViewModel? item,
        CancellationToken token = default)
    {
        try
        {
            if (item?.FileEntry == null)
                return null;

            var version = _loadVersion;

            var bytes = await item.FileEntry.ReadAllBytesAsync(token)
                .ConfigureAwait(false);
            if (bytes == null)
                return null;

            // ここで古い処理を破棄
            if (version != _loadVersion)
                return null;

            return await _imageFactory
                .CreateImageAsync(bytes, token)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
    }

    private async void Mode1Button_Click(object sender, RoutedEventArgs e)
    {
        _pageMode = ImageWindowPageMode.Single;
        ApplyLayout();
        await UpdateImagesAsync();
    }

    private async void Mode2Button_Click(object sender, RoutedEventArgs e)
    {
        _pageMode = ImageWindowPageMode.Double;
        ApplyLayout();
        await UpdateImagesAsync();
    }
}