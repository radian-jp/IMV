using RadianTools.UI.WPF.Imaging;
using RadianTools.UI.WPF.IO;
using RadianTools.UI.WPF.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace IMV.Views;

public partial class ImageWindow : Window
{
    private IReadOnlyList<ThumbnailItemViewModel> _items = Array.Empty<ThumbnailItemViewModel>();
    private int _index;
    private static IImageFactory _imageFactory = RsImageFactory.Shared;

    public ImageWindow()
    {
        InitializeComponent();

        KeyDown += ImageWindow_KeyDown;
        MouseWheel += ImageWindow_MouseWheel;
    }

    /// <summary>
    /// 外部から表示開始
    /// </summary>
    public async Task ShowImagesAsync(
        IReadOnlyList<ThumbnailItemViewModel> items,
        int index)
    {
        _items = items ?? Array.Empty<ThumbnailItemViewModel>();

        if (_items.Count == 0)
        {
            _index = 0;
            await ClearAsync();
            UpdateHeader();
            return;
        }

        _index = Clamp(index);

        UpdateHeader();
        await UpdateImagesAsync();
    }

    /// <summary>
    /// 画像更新（左右2枚）
    /// </summary>
    private async Task UpdateImagesAsync()
    {
        RightImage.Source = await LoadImageAsync(GetItem(_index));
        LeftImage.Source = await LoadImageAsync(GetItem(_index + 1));

        UpdateHeader();
    }

    /// <summary>
    /// ヘッダー更新（index / total）
    /// </summary>
    private void UpdateHeader()
    {
        if (_items.Count == 0)
        {
            IndexText.Text = "0 / 0";
            return;
        }

        IndexText.Text = $"{_index + 1} / {_items.Count}";
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
    /// インデックス安全取得
    /// </summary>
    private ThumbnailItemViewModel? GetItem(int index)
    {
        if ((uint)index >= (uint)_items.Count)
            return null;

        return _items[index];
    }

    /// <summary>
    /// 画像ロード
    /// </summary>
    private static async Task<ImageSource?> LoadImageAsync(
        ThumbnailItemViewModel? item)
    {
        if (item?.FileEntry == null)
            return null;

        var bytes = await item.FileEntry.ReadAllBytesAsync();
        var bmp = _imageFactory.CreateImage(bytes);

        return bmp;
    }

    /// <summary>
    /// キーボード操作（左右移動）
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
        }
    }

    private async void ImageWindow_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (e.Delta > 0)
            await PrevAsync();

        else if (e.Delta < 0)
            await NextAsync();

        e.Handled = true;
    }

    /// <summary>
    /// インデックス安全クランプ
    /// </summary>
    private int Clamp(int index)
    {
        if (_items.Count == 0)
            return 0;

        if (index < 0)
            return 0;

        if (index >= _items.Count)
            return _items.Count - 1;

        return index;
    }

    private async Task NextAsync()
    {
        if (_items.Count == 0)
            return;

        if (_index + 1 < _items.Count)
        {
            _index++;
            await UpdateImagesAsync();
        }
    }

    private async Task PrevAsync()
    {
        if (_items.Count == 0)
            return;

        if (_index > 0)
        {
            _index--;
            await UpdateImagesAsync();
        }
    }
}