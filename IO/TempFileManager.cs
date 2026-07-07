namespace IMV.IO;

using RadianTools.UI.WPF.IO;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Threading;

public sealed class TempFileManager
{
    private const string TempFolderName = "IMV_TempFiles";

    private readonly string _root;
    private readonly ConcurrentBag<string> _files = new();
    private readonly ConcurrentDictionary<string, Lazy<Task<string>>> _cache =
        new(StringComparer.OrdinalIgnoreCase);

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
        var key = entry.LogicalPath;

        while (true)
        {
            var lazy = _cache.GetOrAdd(
                key,
                _ => new Lazy<Task<string>>(
                    () => CreateFileFromEntryAsync(entry, token),
                    LazyThreadSafetyMode.ExecutionAndPublication));

            var path = await lazy.Value;

            if (File.Exists(path))
                return path;

            // 消えていた場合はキャッシュ破棄
            _cache.TryRemove(
                new KeyValuePair<string, Lazy<Task<string>>>(key, lazy));
        }
    }

    private async Task<string> CreateFileFromEntryAsync(
        IFileEntry entry,
        CancellationToken token)
    {
        var fileName = $"{DateTime.Now.Ticks}_{entry.DisplayName}";
        var path = Path.Combine(_root, fileName);

        await using var source = await entry.OpenReadAsync(token);

        await using (var fs = File.Create(path))
        {
            await source.CopyToAsync(fs, token);
        }

        _files.Add(path);

        return path;
    }

    public void Cleanup()
    {
        _cache.Clear();

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
        }

        // 個別削除
        foreach (var file in _files)
        {
            try
            {
                File.SetAttributes(file, FileAttributes.Normal);
                File.Delete(file);
            }
            catch
            {
            }
        }

        // もう一度フォルダ削除
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch
        {
        }
    }
}