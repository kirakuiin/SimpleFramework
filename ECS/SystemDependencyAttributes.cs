namespace SimpleFramework.ECS;

/// <summary>
/// 声明当前系统应在指定系统之前运行。
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
public sealed class RunBeforeAttribute : Attribute
{
    /// <summary>
    /// 创建运行前依赖声明。
    /// </summary>
    /// <param name="systemType">应在其之前运行的系统类型。</param>
    public RunBeforeAttribute(Type systemType)
    {
        SystemType = systemType ?? throw new ArgumentNullException(nameof(systemType));
    }

    /// <summary>
    /// 目标系统类型。
    /// </summary>
    public Type SystemType { get; }
}

/// <summary>
/// 声明当前系统应在指定系统之后运行。
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
public sealed class RunAfterAttribute : Attribute
{
    /// <summary>
    /// 创建运行后依赖声明。
    /// </summary>
    /// <param name="systemType">应在其之后运行的系统类型。</param>
    public RunAfterAttribute(Type systemType)
    {
        SystemType = systemType ?? throw new ArgumentNullException(nameof(systemType));
    }

    /// <summary>
    /// 目标系统类型。
    /// </summary>
    public Type SystemType { get; }
}
