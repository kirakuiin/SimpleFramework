using System.Text;
using System.Text.Json;

namespace SimpleFramework.Utility;

/// <summary>
/// 序列化工具。
/// </summary>
public class SerializeUtil
{
    private static JsonSerializerOptions DefaultOptions => new() {IncludeFields = true};
    
    /// <summary>
    /// 序列化对象
    /// </summary>
    /// <param name="obj"></param>
    /// <param name="options"></param>
    /// <returns>返回字符串</returns>
    public static string Serialize<T>(T obj, JsonSerializerOptions? options = null)
    {
        options ??= DefaultOptions;
        return JsonSerializer.Serialize(obj, options);
    }
    
    /// <summary>
    /// 序列化对象
    /// </summary>
    /// <param name="obj"></param>
    /// <param name="options"></param>
    /// <returns>返回字节流</returns>
    public static byte[] SerializeBytes<T>(T obj, JsonSerializerOptions? options = null)
    {
        options ??= DefaultOptions;
        return Encoding.UTF8.GetBytes(Serialize(obj, options));
    }
    
    /// <summary>
    /// 反序列化对象
    /// </summary>
    /// <param name="bytes"></param>
    /// <param name="options"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public static T Deserialize<T>(string bytes,  JsonSerializerOptions? options = null)
    {
        options ??= DefaultOptions;
        return JsonSerializer.Deserialize<T>(bytes, options)!;
    }
    
    /// <summary>
    /// 反序列化对象
    /// </summary>
    /// <param name="bytes"></param>
    /// <param name="options"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public static T Deserialize<T>(byte[] bytes, JsonSerializerOptions? options = null)
    {
        options ??= DefaultOptions;
        return JsonSerializer.Deserialize<T>(Encoding.UTF8.GetString(bytes), options:options)!;
    }
    
    /// <summary>
    /// 根据指定类型反序列化字节流
    /// </summary>
    /// <param name="bytes"></param>
    /// <param name="type"></param>
    /// <param name="options"></param>
    /// <returns></returns>
    public static object Deserialize(byte[] bytes, Type type, JsonSerializerOptions? options = null)
    {
        options ??= DefaultOptions;
        return JsonSerializer.Deserialize(Encoding.UTF8.GetString(bytes), returnType:type, options:options)!;
    }
}