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
    /// <summary>1ページ</summary>
    Single = 1,
    /// <summary>2ページ(右→左)</summary>
    DoubleRL,
    /// <summary>2ページ(左→右)</summary>
    DoubleLR
}

public partial class ImageWindow : Window
{
    private IReadOnlyList<ThumbnailItemViewModel> _items = Array.Empty<ThumbnailItemViewModel>();
    private ImageWindowPageMode _pageMode = ImageWindowPageMode.DoubleRL;
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

        ThumbnailItemViewModel? right = null;
        ThumbnailItemViewModel? left = null;
        switch( _pageMode )
        {
            case ImageWindowPageMode.Single:
                left = GetItem(SelectedIndex);
                break;

            case ImageWindowPageMode.DoubleRL:
                left = GetItem(SelectedIndex + 1);
                right = GetItem(SelectedIndex);
                break;

            case ImageWindowPageMode.DoubleLR:
                left = GetItem(SelectedIndex);
                right = GetItem(SelectedIndex + 1);
                break;

        }

        UpdateTitle(right, left, true);

        var rightTask = (right != null)
            ? LoadImageAsync(right, token)
            : Task.FromResult<ImageSource?>(null);

        var leftTask = (left != null)
            ? LoadImageAsync(left, token)
            : Task.FromResult<ImageSource?>(null);

        var rightImg = await rightTask;
        var leftImg = await leftTask;

        // 古い結果なら破棄
        if (version != _loadVersion)
            return;

        LeftImage.Source = leftImg;
        RightImage.Source = rightImg;

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
                LeftCol.Width = new GridLength(1, GridUnitType.Star);
                RightCol.Width = new GridLength(0);
                break;

            case ImageWindowPageMode.DoubleRL:
            case ImageWindowPageMode.DoubleLR:
                LeftCol.Width = new GridLength(1, GridUnitType.Star);
                RightCol.Width = new GridLength(1, GridUnitType.Star);
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
        if( left==null && right== null)
        {
            Title = "";
            return;
        }

        var total = _items.Count;
        var page = _items.Count == 0 ? 0 : SelectedIndex + 1;
        var strLoading = loading ? " (Loading...)" : "";

        if( left!=null && right!=null )
        {
            Title = $"({page}, {page + 1} / {total}) L:{left?.DisplayName} R:{right?.DisplayName}{strLoading}";
            return;
        } 
        if (left != null)
        {
            Title = $"({page} / {total}) {left?.DisplayName}{strLoading}";
            return;
        }

        Title = $"({page} / {total}) {right?.DisplayName}{strLoading}";
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

        var diff = 1;
        switch (_pageMode)
        {
            case ImageWindowPageMode.DoubleLR:
            case ImageWindowPageMode.DoubleRL:
                diff = 2;
                break;
        }
        await SetPageIndexAsync(SelectedIndex + diff);
    }

    /// <summary>
    /// 前ページ移動
    /// </summary>
    private async Task PrevAsync()
    {
        var diff = 1;
        switch(_pageMode)
        {
            case ImageWindowPageMode.DoubleLR:
            case ImageWindowPageMode.DoubleRL:
                diff = 2;
                break;
        }
        await SetPageIndexAsync(SelectedIndex - diff);
    }

    private async Task SetPageIndexAsync(int index)
    {
        if (_items.Count == 0)
            return;

        var current = SelectedIndex;
        var next = ClampPageIndex(index);
        if (current == next)
            return;

        SelectedIndex = next;
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
    protected override async void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

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
        }

        e.Handled = true;
    }

    /// <summary>
    /// ホイール操作
    /// </summary>
    protected override async void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);

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

    private async void Mode2RLButton_Click(object sender, RoutedEventArgs e)
    {
        _pageMode = ImageWindowPageMode.DoubleRL;
        ApplyLayout();
        await UpdateImagesAsync();
    }


    private async void Mode2LRButton_Click(object sender, RoutedEventArgs e)
    {
        _pageMode = ImageWindowPageMode.DoubleLR;
        ApplyLayout();
        await UpdateImagesAsync();
    }
}