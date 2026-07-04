namespace IMV.IO;

using RadianTools.UI.WPF.IO;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;

public sealed class TempFileManager
{
    private const string TempFolderName = "IMV_TempFiles";
    private readonly string _root;
    private readonly ConcurrentBag<string> _files = new();

    public static TempFileManager Shared { get; } = new TempFileManager();

    public TempFileManager()
    {
        var pid = Process.GetCurrentProcess().Id;
        _root = Path.Combine(Path.GetTempPath(), TempFolderName, pid.ToString());

        Directory.CreateDirectory(_root);
    }

    public string RootDirectory => _root;

    public async Task<string> CreateFromEntryAsync(
        IFileEntry entry,
        CancellationToken token = default)
    {
        await using var stream = await entry.OpenReadAsync(token);
        return await CreateFileAsync(entry, stream, token);
    }

    private async Task<string> CreateFileAsync(
        IFileEntry entry,
        Stream source,
        CancellationToken token = default)
    {
        var fileName = $"{DateTime.Now.Ticks}_{entry.DisplayName}";
        var path = Path.Combine(_root, fileName);

        await using (var fs = File.Create(path))
        {
            await source.CopyToAsync(fs, token);
        }

        _files.Add(path);
        return path;
    }

    public void Cleanup()
    {
        if (!Directory.Exists(_root))
            return;

        // まずフォルダ削除トライ
        try
        {
            Directory.Delete(_root, recursive: true);
            return;
        }
        catch
        {
            // fallback
        }

        // 個別削除
        foreach (var file in _files)
        {
            try
            {
                File.SetAttributes(file, FileAttributes.Normal);
                File.Delete(file);
            }
            catch { }
        }

        // もう一度フォルダ削除
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch { }
    }
}