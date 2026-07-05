using IMV.IO;
using RadianTools.UI.WPF.Imaging;
using RadianTools.UI.WPF.IO;
using RadianTools.UI.WPF.Logging;
using RadianTools.UI.WPF.ViewModels;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace IMV.Views;

public class ImageContextMenu : ContextMenu
{
    public static ImageContextMenu Shared { get; } = new();

    private static IImageFactory _imageFactory = RsImageFactory.Shared;
    private ThumbnailItemViewModel? _vm;

    public ImageContextMenu()
    {
        var itemOpen = new MenuItem { Header = "関連付けで開く" };
        itemOpen.Click += async (_, __) =>
        {
            await OpenWithAssociatedAppAsync();
        };

        var itemImageCopy = new MenuItem { Header = "イメージをクリップボードにコピー" };
        itemImageCopy.Click += async (_, __) =>
        {
            await CopyImageToClipboard();
        };

        Items.Add(itemOpen);
        Items.Add(itemImageCopy);
    }

    public void Show(ThumbnailItemViewModel vm)
    {
        _vm = vm;
        this.IsOpen = true;
    }

    private async Task OpenWithAssociatedAppAsync()
    {
        var entry = _vm?.FileEntry;
        if (entry == null)
            return;

        try
        {
            string path = entry.LogicalPath;

            // すでに実ファイルならそのまま
            if (File.Exists(path))
            {
                Process.Start(new ProcessStartInfo(path)
                {
                    UseShellExecute = true
                });
                return;
            }

            // ZIP内部など → テンポラリ展開
            var tempPath = await TempFileManager.Shared.CreateFromEntryAsync(entry);
            await using (var stream = await entry.OpenReadAsync())
            await using (var fs = File.Create(tempPath))
            {
                await stream.CopyToAsync(fs);
            }

            Process.Start(new ProcessStartInfo(tempPath)
            {
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            Logger.Shared.Error(ex.ToString());
        }
    }

    private async Task CopyImageToClipboard()
    {
        var bmp = await LoadImageAsync(_vm);
        if (bmp != null)
        {
            Clipboard.SetImage((BitmapSource)bmp);
        }
    }

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

            var bytes = await item.FileEntry.ReadAllBytesAsync(token)
                .ConfigureAwait(false);
            if (bytes == null)
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

}
