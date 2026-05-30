using System.Text.Json;

namespace SimpleFramework.Net;

public interface INetCodec
{
    byte[] Encode<T>(T message);
    object? Decode(ReadOnlySpan<byte> payload, Type messageType);
}

public sealed class JsonNetCodec : INetCodec
{
    private readonly JsonSerializerOptions _options = new(JsonSerializerDefaults.Web);

    public byte[] Encode<T>(T message) => JsonSerializer.SerializeToUtf8Bytes(message, _options);

    public object? Decode(ReadOnlySpan<byte> payload, Type messageType) => JsonSerializer.Deserialize(payload, messageType, _options);
}
