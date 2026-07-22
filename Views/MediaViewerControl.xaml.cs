namespace IMV.Views;

using IMV.Common;
using IMV.IO;
using RadianTools.UI.WPF.Imaging;
using RadianTools.UI.WPF.IO;
using RadianTools.UI.WPF.Logging;
using RadianTools.UI.WPF.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using XamlAnimatedGif;

public partial class MediaViewerControl : UserControl
{
    private static readonly HashSet<string> VideoExtensions =
    [
        ".mp4",
        ".avi",
        ".wmv",
        ".mov",
        ".mpeg",
        ".mpg"
    ];

    private readonly IImageFactory _imageFactory = IMVImageFactory.Shared;

    private ThumbnailItemViewModel? _current;
    private string? _tempFilePath;

    public MediaViewerControl()
    {
        InitializeComponent();
    }

    public async Task ShowAsync(
        ThumbnailItemViewModel? item,
        CancellationToken token = default)
    {
        _current = item;

        if (item == null || item.FileEntry==null )
        {
            Clear();
            return;
        }

        var ext = Path.GetExtension(item.FileEntry.DisplayName)
                      .ToLowerInvariant();

        if (VideoExtensions.Contains(ext))
        {
            await ShowVideoAsync(item, token);
        }
        else
        {
            await ShowImageAsync(item, token);
        }
    }

    public void Clear()
    {
        ClearImageOnly();
        ClearVideoOnly();
    }

    private async Task ShowImageAsync(
        ThumbnailItemViewModel item,
        CancellationToken token)
    {
        ClearVideoOnly();

        PART_Video.Visibility = Visibility.Collapsed;
        PART_Image.Visibility = Visibility.Visible;

        try
        {
            if (item.FileEntry == null)
                return;

            var bytes = await item.FileEntry.ReadAllBytesAsync(token);
            if (bytes == null)
            {
                PART_Image.Source = null;
                return;
            }

            if (MediaFormatDetector.IsGif(bytes))
            {
                AnimationBehavior.SetSourceStream(
                    PART_Image,
                    new MemoryStream(bytes));
            }
            else
            {
                PART_Image.Source = await _imageFactory.CreateImageAsync(bytes, token);
            }
        }
        catch
        {
            PART_Image.Source = null;
        }
    }

    private async Task ShowVideoAsync(
        ThumbnailItemViewModel item,
        CancellationToken token)
    {
        ClearImageOnly();

        PART_Image.Visibility = Visibility.Collapsed;
        PART_Video.Visibility = Visibility.Visible;

        try
        {
            string path;

            // 直接再生可能パスがある場合
            if (File.Exists(item.FileEntry!.LogicalPath))
            {
                path = item.FileEntry.LogicalPath;
            }
            else
            {
                // Tempに展開して再生
                _tempFilePath =
                    await TempFileManager.Shared.CreateFromEntryAsync(
                        item.FileEntry,
                        token);

                path = _tempFilePath;
            }

            PART_Video.Stop();
            PART_Video.Source = new Uri(path, UriKind.Absolute);

            PART_Video.Position = TimeSpan.Zero;
            PART_Video.Play();
        }
        catch(Exception ex)
        {
            Logger.Shared.Error(ex.ToString());
        }
    }

    private async void Video_MediaEnded(object sender, RoutedEventArgs e)
    {
        PART_Video.Position = TimeSpan.Zero;
        PART_Video.Play();
    }

    private void ClearImageOnly()
    {
        AnimationBehavior.SetSourceStream(
            PART_Image,
            null);
        PART_Image.Source = null;
    }

    private void ClearVideoOnly()
    {
        try
        {
            PART_Video.Stop();
            PART_Video.Source = null;
        }
        catch
        {
            // ignore
        }

        PART_Video.Visibility = Visibility.Collapsed;
        PART_Image.Visibility = Visibility.Visible;

        // Temp削除
        if (_tempFilePath != null)
        {
            try
            {
                File.Delete(_tempFilePath);
            }
            catch { }

            _tempFilePath = null;
        }
    }
}