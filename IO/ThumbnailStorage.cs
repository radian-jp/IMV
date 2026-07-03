using Microsoft.Data.Sqlite;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace IMV.IO;

public class ThumbnailStorage : IDisposable
{
    private record EncodeSrc(
        string Path,
        long LastWriteTime,
        WriteableBitmap Bitmap
    ) : IDisposable
    {
        public void Dispose() => Bitmap?.Freeze();
    }

    private readonly SqliteConnection _connectionWrite;
    private readonly BlockingCollection<EncodeSrc> _queueEncode = new();
    private readonly Task _worker;
    private readonly CancellationTokenSource _cts = new();
    private readonly object _dbWriteLock = new();

    private static readonly Lazy<ThumbnailStorage> _shared =
        new(() => new ThumbnailStorage("IMVCache.sqlite"));

    public static ThumbnailStorage Shared => _shared.Value;

    public int ThumbnailWidth { get; set; } = 120;
    public int ThumbnailHeight { get; set; } = 120;
    public int JpegQuality { get; set; } = 85;

    private ThumbnailStorage(string dbPath)
    {
        bool createNew = !File.Exists(dbPath);

        var cs = new SqliteConnectionStringBuilder
        {
            DataSource = dbPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Default
        };

        _connectionWrite = new SqliteConnection(cs.ToString());
        _connectionWrite.Open();

        if (createNew)
            CreateSchema();

        using var cmd = _connectionWrite.CreateCommand();
        cmd.CommandText = "PRAGMA journal_mode=WAL;";
        cmd.ExecuteNonQuery();

        _worker = Task.Factory.StartNew(
            ProcessQueue,
            _cts.Token,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default
        );
    }

    private void CreateSchema()
    {
        lock (_dbWriteLock)
        {
            using var cmd = _connectionWrite.CreateCommand();
            cmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS Thumbnails (
                    Path TEXT PRIMARY KEY,
                    Folder TEXT NOT NULL,
                    LastWriteTime INTEGER NOT NULL,
                    Size INTEGER NOT NULL,
                    Thumbnail BLOB NOT NULL
                );
                CREATE INDEX IF NOT EXISTS idx_folder ON Thumbnails(Folder);
            ";
            cmd.ExecuteNonQuery();
        }
    }

    public BitmapSource GetThumbnail(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"{path} not found.", path);

        long lastWriteTime = File.GetLastWriteTimeUtc(path).Ticks;

        using (var readConn = new SqliteConnection(
            new SqliteConnectionStringBuilder
            {
                DataSource = _connectionWrite.DataSource,
                Mode = SqliteOpenMode.ReadOnly,
                Cache = SqliteCacheMode.Default
            }.ToString()))
        {
            readConn.Open();

            using var cmd = readConn.CreateCommand();
            cmd.CommandText = "SELECT Thumbnail, LastWriteTime FROM Thumbnails WHERE Path = $path";
            cmd.Parameters.AddWithValue("$path", path);
            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                long dbTime = reader.GetInt64(1);
                if (dbTime == lastWriteTime)
                {
                    var size = (int)reader.GetBytes(0, 0, null, 0, 0);
                    byte[] buffer = new byte[size];
                    reader.GetBytes(0, 0, buffer, 0, size);

                    using var ms = new MemoryStream(buffer);
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.StreamSource = ms;
                    bmp.EndInit();
                    bmp.Freeze();
                    return bmp;
                }
            }
        }

        // キャッシュがない → 新規生成
        var srcBmp = new BitmapImage(new Uri(path));
        srcBmp.Freeze();
        var thumb = CreateThumbnail(srcBmp, ThumbnailWidth, ThumbnailHeight);

        // 保存用に WriteableBitmap を生成
        var wb = new WriteableBitmap(thumb.PixelWidth, thumb.PixelHeight, thumb.DpiX, thumb.DpiY, thumb.Format, null);
        var rect = new Int32Rect(0, 0, thumb.PixelWidth, thumb.PixelHeight);
        var stride = thumb.PixelWidth * (thumb.Format.BitsPerPixel / 8);
        byte[] pixels = new byte[stride * thumb.PixelHeight];
        thumb.CopyPixels(pixels, stride, 0);
        wb.WritePixels(rect, pixels, stride, 0);
        wb.Freeze();

        _queueEncode.Add(new EncodeSrc(path, lastWriteTime, wb));

        return thumb;
    }

    private BitmapSource CreateThumbnail(BitmapImage bmp, int maxW, int maxH)
    {
        double ratio = Math.Min((double)maxW / bmp.PixelWidth, (double)maxH / bmp.PixelHeight);
        var transform = new ScaleTransform(ratio, ratio);
        var scaled = new TransformedBitmap(bmp, transform);
        scaled.Freeze();
        return scaled;
    }

    private void WriteThumbnail(EncodeSrc src)
    {
        var folder = Path.GetDirectoryName(src.Path);
        var thumbBytes = EncodeToJpeg(src.Bitmap);

        lock (_dbWriteLock)
        {
            using var cmd = _connectionWrite.CreateCommand();
            cmd.CommandText = @"
                REPLACE INTO Thumbnails (Path, Folder, LastWriteTime, Size, Thumbnail)
                VALUES ($path, $folder, $time, $size, $thumb)
            ";
            cmd.Parameters.AddWithValue("$path", src.Path);
            cmd.Parameters.AddWithValue("$folder", folder);
            cmd.Parameters.AddWithValue("$time", src.LastWriteTime);
            cmd.Parameters.AddWithValue("$size", thumbBytes.Length);
            cmd.Parameters.AddWithValue("$thumb", thumbBytes);
            cmd.ExecuteNonQuery();
        }
    }

    private static byte[] EncodeToJpeg(WriteableBitmap wb)
    {
        var encoder = new JpegBitmapEncoder { QualityLevel = 85 };
        encoder.Frames.Add(BitmapFrame.Create(wb));
        using var ms = new MemoryStream();
        encoder.Save(ms);
        return ms.ToArray();
    }

    private void ProcessQueue()
    {
        try
        {
            foreach (var image in _queueEncode.GetConsumingEnumerable(_cts.Token))
            {
                using (image)
                {
                    WriteThumbnail(image);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // 正常終了
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Thumbnail worker error: {ex}");
        }
    }

    public void DeleteCacheFromFile(string path)
    {
        lock (_dbWriteLock)
        {
            using var cmd = _connectionWrite.CreateCommand();
            cmd.CommandText = "DELETE FROM Thumbnails WHERE Path = $path";
            cmd.Parameters.AddWithValue("$path", path);
            cmd.ExecuteNonQuery();
        }
    }

    public void DeleteCacheFromFolder(string folderPath)
    {
        lock (_dbWriteLock)
        {
            using var cmd = _connectionWrite.CreateCommand();
            cmd.CommandText = "DELETE FROM Thumbnails WHERE Folder = $folder";
            cmd.Parameters.AddWithValue("$folder", folderPath);
            cmd.ExecuteNonQuery();
        }
    }

    public void DeleteCacheFromWriteTime(DateTime time)
        => DeleteCacheFromWriteTime(time.Ticks);

    public void DeleteCacheFromWriteTime(long tick)
    {
        lock (_dbWriteLock)
        {
            using var cmd = _connectionWrite.CreateCommand();
            cmd.CommandText = "DELETE FROM Thumbnails WHERE LastWriteTime < $time";
            cmd.Parameters.AddWithValue("$time", tick);
            cmd.ExecuteNonQuery();
        }
    }

    public void Vacuum()
    {
        lock (_dbWriteLock)
        {
            using var cmd = _connectionWrite.CreateCommand();
            cmd.CommandText = "VACUUM;";
            cmd.ExecuteNonQuery();
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        _queueEncode.CompleteAdding();
        try
        {
            _worker.Wait(10000);
        }
        catch { }

        while (_queueEncode.TryTake(out var encSrc))
            encSrc.Dispose();

        _connectionWrite.Dispose();
        _cts.Dispose();
    }
}
