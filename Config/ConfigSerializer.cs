using System.Reflection;
using System.Text;
using System;
using System.IO;

namespace IMV.Config;

/// <summary>
/// 設定情報シリアライザ基本クラス
/// </summary>
/// <typeparam name="T">設定情報クラス</typeparam>
public abstract class ConfigSerializer<T>
    where T : new()
{
    public Encoding Encoding { get; }
    public string FilePath { get; }

    /// <summary>
    /// コンストラクタ
    /// </summary>
    /// <param name="filePath">設定ファイルのファイルパス</param>
    public ConfigSerializer(string filePath) : this(filePath, Encoding.UTF8) { }

    /// <summary>
    /// コンストラクタ
    /// </summary>
    /// <param name="filePath">設定ファイルのファイルパス</param>
    /// <param name="encoding">設定ファイルのエンコーディング</param>
    public ConfigSerializer(string filePath, Encoding encoding)
    {
        FilePath = filePath;
        Encoding = encoding;
    }

    /// <summary>
    /// エントリポイントアセンブリのCompany, Productから保存先ディレクトリを取得する。
    /// </summary>
    /// <returns>保存先ディレクトリのパス</returns>
    public static string GetPrivateDocumentDir()
    {
        var asm = Assembly.GetEntryAssembly()!;
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            asm.Company(),
            asm.Product()
            );
    }

    /// <summary>
    /// エントリポイントアセンブリのCompany, Productから保存先ファイル名を取得する。
    /// </summary>
    /// <returns>保存先ファイルのパス</returns>
    public static string GetPrivateDocumentFileName(string extention)
    {
        var asm = Assembly.GetEntryAssembly()!;
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            asm.Company(),
            asm.Product(),
            asm.Product() + extention
            );
    }

    /// <summary>
    /// 設定を読み込む。
    /// </summary>
    /// <returns>設定情報オブジェクト</returns>
    public abstract T Load();

    /// <summary>
    /// 設定を保存する。
    /// </summary>
    /// <param name="obj">設定情報オブジェクト</param>
    public abstract void Save(T obj);

    /// <summary>
    /// 設定ファイルを削除する。
    /// </summary>
    public void DeleteFile()
    {
        string filePath = FilePath;
        if (File.Exists(filePath))
            File.Delete(filePath);
    }
}
