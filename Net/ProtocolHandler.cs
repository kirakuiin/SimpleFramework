using System.Reflection;
using SimpleFramework.Utility;

namespace SimpleFramework.Net;


/// <summary>
/// 被此属性标记的结构体视作一个网络协议结构体
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public class ProtocolAttribute : Attribute;


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
    /// 存储所有协议的类型 - 基于类型哈希
    /// </summary>
    private Dictionary<ulong, Type> _protocols = new();

    /// <summary>
    /// 包含处理函数 - 基于类型哈希
    /// </summary>
    private Dictionary<ulong, Action<object>?> _handlers = new();

    /// <summary>
    /// 注册协议的处理函数
    /// </summary>
    /// <param name="handler"></param>
    /// <typeparam name="T"></typeparam>
    public void RegisterHandler<T>(Action<T> handler)
    {
        try
        {
            var type = typeof(T);
            var typeHash = MiscUtil.TypeHash<T>();

            // 检查协议类型是否已注册
            if (!_protocols.ContainsKey(typeHash))
            {
                NetLog.Warning($"协议 [{type.Name}][Hash:0x{typeHash:X16}]的类型信息尚未注册，请先调用{nameof(RegisterProtocol)}");
                return;
            }

            // 检查处理函数是否已存在
            if (_handlers.ContainsKey(typeHash) && _handlers[typeHash] != null)
            {
                NetLog.Warning($"协议 [{type.Name}][Hash:0x{typeHash:X16}] 处理函数已被注册，将被覆盖");
            }

            _handlers[typeHash] = data => handler((T)data);
            NetLog.Info($"注册处理函数 [{type.Name}][Hash:0x{typeHash:X16}]");
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
    public void UnRegisterHandler<T>()
    {
        var type = typeof(T);
        var typeHash = MiscUtil.TypeHash<T>();

        if (!_handlers.ContainsKey(typeHash)) return;
        _handlers[typeHash] = null;
        NetLog.Info($"[{type.Name}][Hash:0x{typeHash:X16}] 的 handler已被清除");
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

            // 查找所有协议类型（带有 ProtocolAttribute 的值类型）
            var protocolTypes = types.Where(type =>
                type is { IsValueType: true, IsEnum: false } &&
                type.GetCustomAttribute<ProtocolAttribute>() != null);

            // 遍历并注册每个协议类型
            foreach (var type in protocolTypes)
            {
                var typeHash = MiscUtil.TypeHash(type);

                // 检查哈希冲突
                if (_protocols.ContainsKey(typeHash))
                {
                    var existingType = _protocols[typeHash];
                    if (existingType != type)
                    {
                        NetLog.Error($"严重错误：类型哈希冲突！哈希值: 0x{typeHash:X16}");
                        NetLog.Error($"现有类型: {existingType.FullName}");
                        NetLog.Error($"新类型: {type.FullName}");
                        continue;
                    }
                    else
                    {
                        NetLog.Warning($"协议 [{type.Name}][Hash:0x{typeHash:X16}] 已被注册，将被覆盖");
                    }
                }

                // 注册到字典中
                _protocols[typeHash] = type;
                NetLog.Info($"注册协议 [{type.Name}][Hash:0x{typeHash:X16}]");
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
    /// <para>数据包的结构为 [guard][typeHash(8bytes)][data_len][data_byte][guard]
    /// </para>
    /// </summary>
    /// <param name="message">结构体对象</param>
    /// <param name="output">输出的byte数组</param>
    /// <typeparam name="T"></typeparam>
    /// <returns>是否成功</returns>
    public bool PackData<T>(T message, out byte[] output)
    {
        var type = typeof(T);
        var attribute = type.GetCustomAttribute<ProtocolAttribute>();
        if (attribute == null)
        {
            NetLog.Warning($"{type}的对象并未实现{nameof(ProtocolAttribute)}特性");
            output = [];
            return false;
        }

        var typeHash = MiscUtil.TypeHash<T>();

        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);

        bw.Write(GuardValue);                    // 前守卫值 (2 bytes)
        bw.Write(typeHash);                      // 类型哈希 (8 bytes)
        var data = SerializeUtil.SerializeBytes(message);
        bw.Write(data.Length);                   // 数据长度 (4 bytes)
        bw.Write(data);                          // 实际数据
        bw.Write(GuardValue);                    // 后守卫值 (2 bytes)

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
        ulong typeHash;
        byte[] bytes;
        try
        {
            var beforeGuard = br.ReadUInt16();
            if (beforeGuard != GuardValue)
            {
                NetLog.Warning("数据包前守卫值不匹配");
                return false;
            }

            typeHash = br.ReadUInt64();             // 读取类型哈希 (8 bytes)
            var length = br.ReadInt32();            // 读取数据长度 (4 bytes)
            bytes = br.ReadBytes(length);           // 读取实际数据
            var afterGuard = br.ReadUInt16();
            if (afterGuard != GuardValue)
            {
                NetLog.Warning("数据包后守卫值不匹配");
                return false;
            }
        }
        catch (EndOfStreamException)
        {
            NetLog.Warning("数据包格式不完整");
            return false;
        }

        return DispatchProtocol(typeHash, bytes);
    }

    private bool DispatchProtocol(ulong typeHash, byte[] bytes)
    {
        // 检查是否有对应的处理函数
        if (!_handlers.ContainsKey(typeHash) || _handlers[typeHash] == null)
        {
            NetLog.Warning($"未注册处理 Hash:0x{typeHash:X16} 的处理函数");
            return false;
        }

        // 获取协议类型
        if (!_protocols.TryGetValue(typeHash, out var type))
        {
            NetLog.Error($"内部错误：找到处理函数但找不到协议类型 Hash:0x{typeHash:X16}");
            return false;
        }

        var handler = _handlers[typeHash];

        try
        {
            var deserializedData = SerializeUtil.Deserialize(bytes, type);
            handler.Invoke(deserializedData);
            return true;
        }
        catch (Exception ex)
        {
            NetLog.Error($"处理协议数据时发生错误 Hash:0x{typeHash:X16} Type:{type.Name}", ex);
            return false;
        }
    }

    /// <summary>
    /// 获取所有已注册的协议信息
    /// </summary>
    /// <returns>协议调试信息列表</returns>
    public List<ProtocolDebugInfo> GetAllRegisteredProtocols()
    {
        var result = new List<ProtocolDebugInfo>();

        foreach (var kvp in _protocols)
        {
            var typeHash = kvp.Key;
            var type = kvp.Value;
            var hasHandler = _handlers.ContainsKey(typeHash) && _handlers[typeHash] != null;

            result.Add(new ProtocolDebugInfo
            {
                Hash = typeHash,
                TypeName = type.FullName ?? type.Name,
                AssemblyName = type.Assembly.GetName().Name!,
                HasHandler = hasHandler,
                RegisteredTime = DateTime.Now // 这里简化处理，实际可以在注册时记录时间
            });
        }

        return result.OrderBy(info => info.Hash).ToList();
    }

    /// <summary>
    /// 获取指定类型的协议信息
    /// </summary>
    /// <typeparam name="T">协议类型</typeparam>
    /// <returns>协议调试信息，如果未注册则返回null</returns>
    public ProtocolDebugInfo? GetProtocolInfo<T>()
    {
        return GetProtocolInfo(typeof(T));
    }

    /// <summary>
    /// 获取指定类型的协议信息
    /// </summary>
    /// <param name="type">协议类型</param>
    /// <returns>协议调试信息，如果未注册则返回null</returns>
    public ProtocolDebugInfo? GetProtocolInfo(Type type)
    {
        var typeHash = MiscUtil.TypeHash(type);

        if (!_protocols.ContainsKey(typeHash))
        {
            return null;
        }

        var hasHandler = _handlers.ContainsKey(typeHash) && _handlers[typeHash] != null;

        return new ProtocolDebugInfo
        {
            Hash = typeHash,
            TypeName = type.FullName ?? type.Name,
            AssemblyName = type.Assembly.GetName().Name!,
            HasHandler = hasHandler,
            RegisteredTime = DateTime.Now
        };
    }

    /// <summary>
    /// 打印调试信息到控制台和日志
    /// </summary>
    public void PrintDebugInfo()
    {
        var allProtocols = GetAllRegisteredProtocols();
        var withHandlers = allProtocols.Where(p => p.HasHandler).ToList();
        var withoutHandlers = allProtocols.Where(p => !p.HasHandler).ToList();

        NetLog.Info("=== ProtocolHandler 调试信息 ===");
        NetLog.Info($"总协议数: {allProtocols.Count}");
        NetLog.Info($"已注册处理函数: {withHandlers.Count}");
        NetLog.Info($"未注册处理函数: {withoutHandlers.Count}");
        NetLog.Info($"处理函数覆盖率: {(allProtocols.Count > 0 ? (double)withHandlers.Count / allProtocols.Count * 100 : 0):F1}%");

        if (withHandlers.Count > 0)
        {
            NetLog.Info("\n✅ 已注册处理函数的协议:");
            foreach (var protocol in withHandlers)
            {
                NetLog.Info($"  {protocol}");
            }
        }

        if (withoutHandlers.Count > 0)
        {
            NetLog.Info("\n⚠️ 已注册但无处理函数的协议:");
            foreach (var protocol in withoutHandlers)
            {
                NetLog.Info($"  {protocol}");
            }
        }

        if (allProtocols.Count == 0)
        {
            NetLog.Info("未注册任何协议");
        }

        NetLog.Info("=== 调试信息结束 ===");
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


/// <summary>
/// 协议调试信息结构体
/// </summary>
public struct ProtocolDebugInfo
{
    /// <summary>
    /// 类型哈希值
    /// </summary>
    public ulong Hash { get; set; }

    /// <summary>
    /// 完整类型名称
    /// </summary>
    public string TypeName { get; set; }

    /// <summary>
    /// 程序集名称
    /// </summary>
    public string AssemblyName { get; set; }

    /// <summary>
    /// 是否已注册处理函数
    /// </summary>
    public bool HasHandler { get; set; }

    /// <summary>
    /// 注册时间（如果可追踪）
    /// </summary>
    public DateTime? RegisteredTime { get; set; }

    public override string ToString()
    {
        return $"[{(HasHandler ? "✓" : "○")}] 0x{Hash:X16} {TypeName} ({AssemblyName})";
    }
}
