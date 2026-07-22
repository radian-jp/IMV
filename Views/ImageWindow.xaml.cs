namespace IMV.Views;

using IMV.Config;
using IMV.State;
using RadianTools.UI.WPF.ViewModels;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

public enum ImageWindowPageMode
{
    /// <summary>未設定</summary>
    NotSet,
    /// <summary>1ページ</summary>
    Single = 1,
    /// <summary>2ページ(右→左)</summary>
    DoubleRL,
    /// <summary>2ページ(左→右)</summary>
    DoubleLR
}

public partial class ImageWindow : Window
{
    private record struct CurrentPages(ThumbnailItemViewModel? Left, ThumbnailItemViewModel? Right);

    private IReadOnlyList<ThumbnailItemViewModel> _items = Array.Empty<ThumbnailItemViewModel>();

    public ImageWindowPageMode PageMode
    {
        get => _pageMode;
        private set
        {
            if (value == _pageMode)
                return;

            _pageMode = value;
            switch (_pageMode)
            {
                case ImageWindowPageMode.Single:
                    ButtonPageModeSingle.IsChecked = true;
                    PageSlider.SmallChange = 1;
                    break;

                case ImageWindowPageMode.DoubleRL:
                    ButtonPageModeDoubleRL.IsChecked = true;
                    PageSlider.SmallChange = 2;
                    break;

                case ImageWindowPageMode.DoubleLR:
                    ButtonPageModeDoubleLR.IsChecked = true;
                    PageSlider.SmallChange = 2;
                    break;
            }

            OnPageModeChanged(value);
        }
    }
    private ImageWindowPageMode _pageMode = ImageWindowPageMode.NotSet;

    private int _loadVersion;
    private CancellationTokenSource? _loadCts;
    private ImageViewState? _imageViewState;
    private ThumbnailItemViewModel? _currentLeft;
    private ThumbnailItemViewModel? _currentRight;
    private bool _initialized = false;

    private readonly DispatcherTimer _delayPageLoadTimer =
        new() { Interval = TimeSpan.FromMilliseconds(100) };

    private int _delayLoadPageIndex = -1;

    private int SelectedIndex
    {
        get => _imageViewState!.SelectedThumbnailIndex;
        set => _imageViewState!.SelectedThumbnailIndex = value;
    }

    public ImageWindow()
    {
        InitializeComponent();
    }

    private void OnLoad(object sender, RoutedEventArgs e)
    {
        // 設定の読み込み
        var config = IMVConfig.Shared.Load();

        // ウィンドウ状態を復元
        config.ImageWindowInfo.Restore(this);
        PageMode = config.ImageWindowPageMode;

        _delayPageLoadTimer.Tick += OnDelayPageLoadStart;

        _initialized = true;
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        // 現在の状態を Config オブジェクトに格納
        var config = IMVConfig.Shared.Load();
        config.ImageWindowInfo = WindowInfo.FromWindow(this);
        config.ImageWindowPageMode = PageMode;
        IMVConfig.Shared.Save(config);

        LeftImage.Clear();
        RightImage.Clear();

        base.OnClosing(e);
    }

    private void BeginDelayPageLoad(int index)
    {
        _delayLoadPageIndex = index;

        // タイトル更新（ロード開始）
        var pages = GetCurrentPageViewModel(index);
        UpdateTitle(index, pages, true);

        _delayPageLoadTimer.Stop();
        _delayPageLoadTimer.Start();
    }

    protected async void OnDelayPageLoadStart(object? sender, EventArgs e)
    {
        var page = Interlocked.Exchange(ref _delayLoadPageIndex, -1);
        if (page < 0)
            return;

        await SetPageIndexAsync(page);
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
        PageSlider.Maximum = (int)_items.Count - 1;

        if (_items.Count == 0)
        {
            SelectedIndex = 0;
            await ClearAsync();
            UpdateTitle(0, null);
            return;
        }

        await RefreshAsync();
    }

    /// <summary>
    /// 画像更新
    /// </summary>
    private async Task UpdateImagesAsync(int index, CurrentPages pages)
    {
        var token = _loadCts?.Token ?? CancellationToken.None;
        var version = _loadVersion;

        var left = pages.Left;
        var right = pages.Right;

        // 古い結果なら破棄
        if (version != _loadVersion)
            return;

        await LeftImage.ShowAsync(left, token);
        await RightImage.ShowAsync(right, token);

        _currentLeft = left;
        _currentRight = right;

        // タイトル更新（ロード完了）
        UpdateTitle(index, pages);
    }

    /// <summary>
    /// ページモードレイアウト反映
    /// </summary>
    private void ApplyPageModeLayout(ImageWindowPageMode pageMode)
    {
        switch (pageMode)
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
    private void UpdateTitle(int currentIndex, CurrentPages? pages, bool loading = false)
    {
        if (!pages.HasValue)
        {
            Title = "";
            return;
        }

        var left = pages.Value.Left;
        var right = pages.Value.Right;

        var total = _items.Count;
        var page = _items.Count == 0 ? 0 : currentIndex + 1;
        var strLoading = loading ? " (Loading...)" : "";

        if (left != null && right != null)
        {
            if (PageMode == ImageWindowPageMode.DoubleLR)
            {
                Title = $"({page}, {page + 1} / {total}) L:{left?.DisplayName} R:{right?.DisplayName}{strLoading}";
            }
            else
            {
                Title = $"({page + 1}, {page} / {total}) L:{left?.DisplayName} R:{right?.DisplayName}{strLoading}";
            }
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
        LeftImage.Clear();
        RightImage.Clear();
        return Task.CompletedTask;
    }

    private async Task SetPageIndexAsync(int index)
    {
        if (_items.Count == 0)
            return;

        if (SelectedIndex == index)
            return;

        SelectedIndex = index;
        await RefreshAsync();
    }

    private CurrentPages GetCurrentPageViewModel(int index)
    {
        ThumbnailItemViewModel? right = null;
        ThumbnailItemViewModel? left = null;
        switch (PageMode)
        {
            case ImageWindowPageMode.Single:
                left = GetItem(index);
                break;

            case ImageWindowPageMode.DoubleRL:
                left = GetItem(index + 1);
                right = GetItem(index);
                break;

            case ImageWindowPageMode.DoubleLR:
                left = GetItem(index);
                right = GetItem(index + 1);
                break;
        }

        return new CurrentPages(left, right);
    }

    /// <summary>
    /// 再描画
    /// </summary>
    private async Task RefreshAsync()
    {
        //バージョンを更新し、以前のロードをキャンセル
        Interlocked.Increment(ref _loadVersion);
        _loadCts?.Cancel();
        _loadCts?.Dispose();
        _loadCts = new CancellationTokenSource();

        var index = SelectedIndex;

        // スライダー位置の同期
        PageSlider.Value = index;

        var pages = GetCurrentPageViewModel(index);

        // タイトル更新（ロード開始）
        UpdateTitle(index, pages, true);

        await UpdateImagesAsync(index, pages);
    }

    /// <summary>
    /// キーボード操作
    /// </summary>
    protected override async void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);

        switch (e.Key)
        {
            case Key.Right:
                PageSlider.Value += PageSlider.SmallChange;
                break;

            case Key.Left:
                PageSlider.Value -= PageSlider.SmallChange;
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
            PageSlider.Value -= PageSlider.SmallChange;
        else
            PageSlider.Value += PageSlider.SmallChange;

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

    private async void ButtonPageMode_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not RadioButton rb)
            return;

        if (rb.Tag is not ImageWindowPageMode mode)
            return;

        PageMode = mode;
        await RefreshAsync();
    }

    private void OnPageModeChanged(ImageWindowPageMode newValue)
    {
        ApplyPageModeLayout(newValue);
    }

    private void Image_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        var image = (FrameworkElement)sender;
        var vm = image == LeftImage
            ? _currentLeft
            : _currentRight;
        if (vm == null)
            return;

        if( image.ContextMenu == null)
            image.ContextMenu = ImageContextMenu.Shared;

        var menu = (ImageContextMenu)image.ContextMenu!;
        menu.Show(vm);
    }

    private async void PageSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_initialized || !IsVisible)
            return;

        BeginDelayPageLoad((int)e.NewValue);
    }
}