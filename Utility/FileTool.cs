using System.Text.Json;

namespace SimpleFramework.Utility;

/// <summary>
/// 文件读写工具, 提供了常用的文件读写接口
/// </summary>
public static class FileTool
{
    private static readonly JsonSerializerOptions DefaultOptions = new () { WriteIndented = true, IncludeFields = true };
    
    /// <summary>
    /// 以json的形式
    /// </summary>
    /// <param name="obj"></param>
    /// <param name="filePath"></param>
    /// <typeparam name="T"></typeparam>
    public static void SaveAsJson<T>(T obj, string filePath)
    {
        var result = SerializeTool.Serialize(obj, DefaultOptions);
        try
        {
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
    /// 从文件中加载json并转为对象
    /// </summary>
    /// <param name="filePath"></param>
    /// <param name="obj"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public static bool LoadFromJson<T>(string filePath, out T obj)
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
            obj = SerializeTool.Deserialize<T>(sr.ReadToEnd(), DefaultOptions);
            return true;
        }
        catch (Exception e)
        {
            Logging.Error($"读取类型{nameof(T)}从{filePath}失败", e);
            return false;
        }
    }

    /// <summary>
    /// 以二进制的形式存储对象
    /// </summary>
    /// <param name="obj"></param>
    /// <param name="filePath"></param>
    /// <typeparam name="T"></typeparam>
    public static void SaveAsBinary<T>(T obj, string filePath)
    {
        try
        {
            var bytes = SerializeTool.SerializeBytes(obj);
            using var fs = new FileStream(filePath, FileMode.Create);
            fs.Write(bytes, 0, bytes.Length);
        }
        catch (Exception e)
        {
            Logging.Error($"写入类型{nameof(T)}到{filePath}失败", e);
        }
    }


    /// <summary>
    /// 以二进制的形式读取文件，并转为对象
    /// </summary>
    /// <param name="filePath"></param>
    /// <param name="obj"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public static bool LoadFromBinary<T>(string filePath, out T obj)
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
            obj = SerializeTool.Deserialize<T>(bytes);
            return true;
        }
        catch (Exception e)
        {
            Logging.Error($"读取类型{nameof(T)}从{filePath}失败", e);
            return false;
        }
    }
}