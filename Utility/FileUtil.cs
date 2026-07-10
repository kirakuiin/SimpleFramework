using System.Text.Json;

namespace SimpleFramework.Utility;

/// <summary>
/// 文件读写工具；I/O 或序列化失败时记录日志，不向调用方传播异常。
/// </summary>
public static class FileUtil
{
    private static readonly JsonSerializerOptions DefaultOptions = new () { WriteIndented = true, IncludeFields = true };
    
    /// <summary>
    /// 将对象以格式化 JSON 写入文件，覆盖已有内容。
    /// </summary>
    /// <param name="obj">要保存的对象。</param>
    /// <param name="filePath">目标文件路径。</param>
    /// <typeparam name="T">对象类型。</typeparam>
    public static void SaveAsJson<T>(T obj, string filePath)
    {
        try
        {
            var result = SerializeUtil.Serialize(obj, DefaultOptions);
            using var fs = new FileStream(filePath, FileMode.Create);
            using var sw = new StreamWriter(fs);
            sw.Write(result);
        }
        catch (Exception e)
        {
            Logging.Error($"写入类型{nameof(T)}到{filePath}失败", e);
        }
    }

    /// <summary>
    /// 从文件读取 JSON 并反序列化对象。
    /// </summary>
    /// <param name="filePath">源文件路径。</param>
    /// <param name="obj">读取成功时为反序列化结果；JSON <see langword="null"/> 或失败时可为空。</param>
    /// <typeparam name="T">对象类型。</typeparam>
    /// <returns>文件存在且 JSON 成功解析时为 <see langword="true"/>。</returns>
    public static bool LoadFromJson<T>(string filePath, [System.Diagnostics.CodeAnalysis.MaybeNull] out T obj)
    {
        obj = default!;
        if (!File.Exists(filePath))
        {
            Logging.Warning($"文件:{filePath} 不存在");
            return false;
        }
        try
        {
            using var fs = new FileStream(filePath, FileMode.Open);
            using var sr = new StreamReader(fs);
            obj = SerializeUtil.Deserialize<T>(sr.ReadToEnd(), DefaultOptions);
            return true;
        }
        catch (Exception e)
        {
            Logging.Error($"读取类型{nameof(T)}从{filePath}失败", e);
            return false;
        }
    }

    /// <summary>
    /// 将对象序列化为 UTF-8 JSON 字节并写入文件，覆盖已有内容。
    /// </summary>
    /// <param name="obj">要保存的对象。</param>
    /// <param name="filePath">目标文件路径。</param>
    /// <typeparam name="T">对象类型。</typeparam>
    public static void SaveAsBinary<T>(T obj, string filePath)
    {
        try
        {
            var bytes = SerializeUtil.SerializeBytes(obj);
            using var fs = new FileStream(filePath, FileMode.Create);
            fs.Write(bytes, 0, bytes.Length);
        }
        catch (Exception e)
        {
            Logging.Error($"写入类型{nameof(T)}到{filePath}失败", e);
        }
    }


    /// <summary>
    /// 读取 UTF-8 JSON 字节文件并反序列化对象。
    /// </summary>
    /// <param name="filePath">源文件路径。</param>
    /// <param name="obj">读取成功时为反序列化结果；JSON <see langword="null"/> 或失败时可为空。</param>
    /// <typeparam name="T">对象类型。</typeparam>
    /// <returns>文件存在且内容成功解析时为 <see langword="true"/>。</returns>
    public static bool LoadFromBinary<T>(string filePath, [System.Diagnostics.CodeAnalysis.MaybeNull] out T obj)
    {
        obj = default!;
        if (!File.Exists(filePath))
        {
            Logging.Warning($"文件:{filePath} 不存在");
            return false;
        }

        try
        {
            using var fs = new FileStream(filePath, FileMode.Open);
            using var ms = new MemoryStream();
            fs.CopyTo(ms);
            var bytes = ms.ToArray();
            obj = SerializeUtil.Deserialize<T>(bytes);
            return true;
        }
        catch (Exception e)
        {
            Logging.Error($"读取类型{nameof(T)}从{filePath}失败", e);
            return false;
        }
    }
}
