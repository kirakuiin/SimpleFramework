using System.Text.Json;

namespace SimpleFramework.Net;

/// <summary>
/// 定义网络消息载荷的编码和解码接口。
/// </summary>
public interface INetCodec
{
    /// <summary>
    /// 将消息对象编码为字节载荷。
    /// </summary>
    byte[] Encode<T>(T message);

    /// <summary>
    /// 将字节载荷解码为指定消息类型。
    /// </summary>
    object? Decode(ReadOnlySpan<byte> payload, Type messageType);
}

/// <summary>
/// 基于 System.Text.Json 的默认消息编解码器。
/// </summary>
public sealed class JsonNetCodec : INetCodec
{
    private readonly JsonSerializerOptions _options = new(JsonSerializerDefaults.Web);

    /// <inheritdoc />
    public byte[] Encode<T>(T message) => JsonSerializer.SerializeToUtf8Bytes(message, _options);

    /// <inheritdoc />
    public object? Decode(ReadOnlySpan<byte> payload, Type messageType) => JsonSerializer.Deserialize(payload, messageType, _options);
}
