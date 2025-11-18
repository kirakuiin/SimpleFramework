using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SimpleFramework.Utility;

/// <summary>
/// 序列化工具。
/// </summary>
public static class SerializeTool
{
    /// <summary>
    /// 默认的序列化选项
    /// </summary>
    public static JsonSerializerOptions Options { set; get; } = new() { IncludeFields = true };
        
    /// <summary>
    /// 序列化对象。
    /// </summary>
    /// <param name="obj"></param>
    /// <returns>返回字符串</returns>
    public static string Serialize<T>(T obj)
    {
        return JsonSerializer.Serialize(obj, Options);
    }
        
    /// <summary>
    /// 序列化对象。
    /// </summary>
    /// <param name="obj"></param>
    /// <returns>返回字节流</returns>
    public static byte[] SerializeBytes<T>(T obj)
    {
        return Encoding.UTF8.GetBytes(Serialize(obj));
    }

    /// <summary>
    /// 反序列化对象。
    /// </summary>
    /// <param name="bytes"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public static T Deserialize<T>(string bytes)
    {
        return JsonSerializer.Deserialize<T>(bytes, Options);
    }
        
    /// <summary>
    /// 反序列化对象。
    /// </summary>
    /// <param name="bytes"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public static T Deserialize<T>(byte[] bytes)
    {
        return JsonSerializer.Deserialize<T>(Encoding.UTF8.GetString(bytes), options:Options);
    }

    public static object Deserialize(byte[] bytes, Type type)
    {
        return JsonSerializer.Deserialize(Encoding.UTF8.GetString(bytes), returnType:type, options:Options);
    }
}