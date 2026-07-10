using System.Text;
using System.Text.Json;

namespace SimpleFramework.Utility;

/// <summary>
/// 序列化工具。
/// </summary>
public static class SerializeUtil
{
    private static readonly JsonSerializerOptions DefaultOptions = new() { IncludeFields = true };
    
    /// <summary>
    /// 将对象序列化为 JSON 字符串。
    /// </summary>
    /// <param name="obj">要序列化的对象。</param>
    /// <param name="options">序列化选项；为空时包含公共字段。</param>
    /// <returns>JSON 字符串。</returns>
    public static string Serialize<T>(T obj, JsonSerializerOptions? options = null)
    {
        options ??= DefaultOptions;
        return JsonSerializer.Serialize(obj, options);
    }
    
    /// <summary>
    /// 将对象直接序列化为 UTF-8 JSON 字节。
    /// </summary>
    /// <param name="obj">要序列化的对象。</param>
    /// <param name="options">序列化选项；为空时包含公共字段。</param>
    /// <returns>UTF-8 JSON 字节。</returns>
    public static byte[] SerializeBytes<T>(T obj, JsonSerializerOptions? options = null)
    {
        options ??= DefaultOptions;
        return JsonSerializer.SerializeToUtf8Bytes(obj, options);
    }
    
    /// <summary>
    /// 将 JSON 字符串反序列化为指定类型。
    /// </summary>
    /// <param name="bytes">JSON 字符串。</param>
    /// <param name="options">反序列化选项；为空时包含公共字段。</param>
    /// <typeparam name="T">目标类型。</typeparam>
    /// <returns>反序列化结果；JSON 为 <see langword="null"/> 时返回默认值。</returns>
    public static T? Deserialize<T>(string bytes, JsonSerializerOptions? options = null)
    {
        options ??= DefaultOptions;
        return JsonSerializer.Deserialize<T>(bytes, options);
    }

    /// <summary>
    /// 按运行时类型反序列化 JSON 字符串。
    /// </summary>
    /// <param name="bytes">JSON 字符串。</param>
    /// <param name="type">目标运行时类型。</param>
    /// <param name="options">反序列化选项；为空时包含公共字段。</param>
    /// <returns>反序列化结果；JSON 为 <see langword="null"/> 时返回空。</returns>
    public static object? Deserialize(string bytes, Type type, JsonSerializerOptions? options = null)
    {
        options ??= DefaultOptions;
        return JsonSerializer.Deserialize(bytes, returnType: type, options: options);
    }
    
    /// <summary>
    /// 将 UTF-8 JSON 字节反序列化为指定类型。
    /// </summary>
    /// <param name="bytes">UTF-8 JSON 字节。</param>
    /// <param name="options">反序列化选项；为空时包含公共字段。</param>
    /// <typeparam name="T">目标类型。</typeparam>
    /// <returns>反序列化结果；JSON 为 <see langword="null"/> 时返回默认值。</returns>
    public static T? Deserialize<T>(byte[] bytes, JsonSerializerOptions? options = null)
    {
        options ??= DefaultOptions;
        return JsonSerializer.Deserialize<T>(bytes, options);
    }
    
    /// <summary>
    /// 按运行时类型反序列化 UTF-8 JSON 字节。
    /// </summary>
    /// <param name="bytes">UTF-8 JSON 字节。</param>
    /// <param name="type">目标运行时类型。</param>
    /// <param name="options">反序列化选项；为空时包含公共字段。</param>
    /// <returns>反序列化结果；JSON 为 <see langword="null"/> 时返回空。</returns>
    public static object? Deserialize(byte[] bytes, Type type, JsonSerializerOptions? options = null)
    {
        options ??= DefaultOptions;
        return JsonSerializer.Deserialize(bytes, type, options);
    }
}
