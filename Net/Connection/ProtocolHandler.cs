using System.Reflection;
using SimpleFramework.Collections;
using SimpleFramework.Utility;

namespace SimpleFramework.Net.Connection;

/// <summary>
/// 用来注册网络协议的处理函数
/// <remarks>通过传入不同的guardValue, 可以让用同一个网络通道，用同样的协议id的两个不同实例互不影响</remarks>
/// <param name="guardValue">标记</param>
/// </summary>
public class ProtocolHandler(ushort guardValue = 0xCafe) : IUtility
{
    /// <summary>
    /// 用来在数据包前后做标记
    /// </summary>
    private ushort GuardValue => guardValue;
    
    /// <summary>
    /// 存储所有协议的类型
    /// </summary>
    private DefaultDict<ushort, Dictionary<ushort, Type>> _protocols = new(() => new Dictionary<ushort, Type>());

    /// <summary>
    /// 包含处理函数
    /// </summary>
    private DefaultDict<ushort, DefaultDict<ushort, Action<object>?>> _handlers = new(() => new DefaultDict<ushort, Action<object>?>(() => null));

    /// <summary>
    /// 注册协议的处理函数
    /// </summary>
    /// <param name="handler"></param>
    /// <typeparam name="T"></typeparam>
    public void RegisterHandler<T>(Action<T> handler) where T : struct
    {
        try
        {
            var type = typeof(T);

            var attr = type.GetCustomAttribute<ProtocolAttribute>();
            if (attr == null)
            {
                NetLog.Warning($"{type.Name}对象未实现{nameof(ProtocolAttribute)}特性");
                return;
            }

            if (!_protocols[attr.MainId].ContainsKey(attr.SubId))
            {
                NetLog.Warning($"协议 [{type.Name}][{attr.MainId}:{attr.SubId}]的类型信息尚未注册，请先调用{nameof(RegisterProtocol)}");
                return;
            }

            if (_handlers[attr.MainId][attr.SubId] != null)
            {
                NetLog.Warning($"协议 [{type.Name}][{attr.MainId}:{attr.SubId}] 处理函数已被注册，将被覆盖");
            }

            _handlers[attr.MainId][attr.SubId] = data => handler((T)data);
            NetLog.Info($"注册处理函数 [{type.Name}][{attr.MainId}:{attr.SubId}]");
        }
        catch (Exception ex)
        {
            NetLog.Error("注册处理器时发生错误", ex);
        }
    }

    /// <summary>
    /// 取消注册某个协议的处理函数
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public void UnRegisterHandler<T>() where T : struct
    {
        var type = typeof(T);

        var attr = type.GetCustomAttribute<ProtocolAttribute>();
        if (attr == null)
        {
            NetLog.Warning($"{type.Name}对象未实现{nameof(ProtocolAttribute)}特性");
            return;
        }

        if (!_handlers[attr.MainId].ContainsKey(attr.SubId)) return;
        _handlers[attr.MainId][attr.SubId] = null;
        NetLog.Info($"[{type.Name}][{attr.MainId}:{attr.SubId}] 的 handler已被清除");
    }

    /// <summary>
    /// 注册程序集内所有的协议
    /// </summary>
    /// <param name="assembly"></param>
    public void RegisterProtocol(Assembly assembly)
    {
        try
        {
            var types = assembly.GetTypes();

            var protocolTypes = types.Where(type =>
                type is { IsValueType: true, IsEnum: false } &&
                type.GetCustomAttribute<ProtocolAttribute>() != null);

            // 3. 遍历并注册每个协议类型
            foreach (var type in protocolTypes)
            {
                var attr = type.GetCustomAttribute<ProtocolAttribute>();

                // 检查是否已注册
                if (_protocols[attr.MainId].ContainsKey(attr.SubId))
                {
                    NetLog.Warning($"协议 [{attr.MainId}:{attr.SubId}] 已被注册，将被覆盖, {_protocols[attr.MainId][attr.SubId]
                        .Name} => {type.Name}");
                }

                // 注册到字典中
                _protocols[attr.MainId][attr.SubId] = type;
                NetLog.Info($"注册协议 [{type.Name}][{attr.MainId}:{attr.SubId}]");
            }
        }
        catch (ReflectionTypeLoadException ex)
        {
            // 处理程序集加载异常
            NetLog.Error($"程序集加载失败: {assembly.GetName().Name}", ex);
        }
        catch (Exception ex)
        {
            NetLog.Error($"注册协议时发生错误", ex);
        }
    }

    /// <summary>
    /// 清理全部注册信息
    /// </summary>
    public void Clear()
    {
        _handlers.Clear();
        _protocols.Clear();
    }

    /// <summary>
    /// 将带有<see cref="ProtocolAttribute"/>特性的结构体对象打包
    /// <para>数据包的结构为 [mainId][subId][data_len][0xcafe][data_byte][0xcafe]
    /// </para>
    /// </summary>
    /// <param name="message">结构体对象</param>
    /// <param name="output">输出的byte数组</param>
    /// <typeparam name="T"></typeparam>
    /// <returns>是否成功</returns>
    public bool PackData<T>(T message, out byte[] output) where T : struct
    {
        var type = typeof(T);
        var attribute = type.GetCustomAttribute<ProtocolAttribute>();
        if (attribute == null)
        {
            NetLog.Warning($"{type}的对象并未无特性{nameof(ProtocolAttribute)}");
            output = [];
            return false;
        }
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);
        bw.Write(attribute.MainId);
        bw.Write(attribute.SubId);
        var data = SerializeTool.SerializeBytes(message);
        bw.Write(data.Length);
        bw.Write(GuardValue);
        bw.Write(data);
        bw.Write(GuardValue);
        output = ms.ToArray();
        return true;
    }

    /// <summary>
    /// 处理数据
    /// <remarks>只有数据结构解包后可以找到注册函数的会被处理</remarks>
    /// </summary>
    /// <param name="data"></param>
    /// <returns>如果返回假，则代表数据无法处理</returns>
    public bool HandleData(byte[] data)
    {
        using var ms = new MemoryStream(data);
        using var br = new BinaryReader(ms);
        ushort mainId;
        ushort subId;
        byte[] bytes;
        try
        {
            mainId = br.ReadUInt16();
            subId = br.ReadUInt16();
            var length = br.ReadInt32();
            var beforeGuard = br.ReadUInt16();
            bytes = br.ReadBytes(length);
            var afterGuard = br.ReadUInt16();
            if (beforeGuard != GuardValue || afterGuard != GuardValue)
            {
                return false;
            }
        }
        catch (EndOfStreamException)
        {
            return false;
        }
        
        return DispatchProtocol(mainId, subId, bytes);
    }

    private bool DispatchProtocol(ushort mainId, ushort subId, byte[] bytes)
    {
        var handler = _handlers[mainId][subId];
        if (handler == null)
        {
            NetLog.Warning($"未注册处理 {mainId}:{subId} 的处理函数");
            return false;
        }
        var type = _protocols[mainId][subId];
        handler.Invoke(SerializeTool.Deserialize(bytes, type));
        return true;
    }
}


public static class Extensions
{
    /// <summary>
    /// 注册当前代码所属程序集的协议
    /// </summary>
    /// <param name="handler"></param>
    public static void RegisterExecutingProtocol(this ProtocolHandler handler)
    {
        handler.RegisterProtocol(Assembly.GetExecutingAssembly());
    }

    /// <summary>
    /// 注册调用此接口所在程序集的所有协议
    /// </summary>
    /// <param name="handler"></param>
    public static void RegisterCallingProtocol(this ProtocolHandler handler)
    {
        handler.RegisterProtocol(Assembly.GetCallingAssembly());
    }
}