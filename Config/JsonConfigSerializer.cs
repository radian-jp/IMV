using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;
using System.IO;

namespace IMV.Config;

public class JsonConfigSerializer<T> : ConfigSerializer<T>
    where T : new()
{
    private readonly static JsonSerializerOptions DefaultOptions = new JsonSerializerOptions()
    {
        Converters = {
                    new JsonStringEnumConverter()
                },
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
        WriteIndented = true
    };

    /// <summary>
    /// コンストラクタ
    /// </summary>
    public JsonConfigSerializer() : base(GetPrivateDocumentFileName(".config.json"))
    {
    }

    /// <summary>
    /// <inheritdoc/>
    /// </summary>
    public JsonConfigSerializer(string filePath) : base(filePath)
    {
    }

    /// <summary>
    /// <inheritdoc/>
    /// </summary>
    public JsonConfigSerializer(string filePath, Encoding encoding) : base(filePath, encoding)
    {
    }

    /// <summary>
    /// <inheritdoc/>
    /// </summary>
    /// <returns></returns>
    public override T Load()
    {
        if (!File.Exists(FilePath))
        {
            return new T();
        }

        using (var stream = new FileStream(FilePath, FileMode.Open, FileAccess.Read))
        {
            var result = JsonSerializer.Deserialize<T>(stream, DefaultOptions);
            return  result ?? new T();
        }
    }

    /// <summary>
    /// <inheritdoc/>
    /// </summary>
    /// <param name="config"></param>
    public override void Save(T config)
    {
        var dirName = Path.GetDirectoryName(FilePath);
        if (dirName != null && !Directory.Exists(dirName))
        {
            Directory.CreateDirectory(dirName);
        }

        using (var stream = new FileStream(FilePath, FileMode.Create, FileAccess.Write))
        {
            JsonSerializer.Serialize(stream, config, DefaultOptions);
        }
    }
}
