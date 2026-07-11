namespace SimpleFramework.Patterns;

/// <summary>
/// 实现单例模式必须实现的接口
/// </summary>
public interface ISingleton
{
    /// <summary>
    /// 初始化单例对象
    /// </summary>
    public void Initialize();

    /// <summary>
    /// 清除单例对象
    /// </summary>
    public void Clear();
}
    
/// <summary>
/// 代表单例对象的初始化状态
/// </summary>
public enum SingletonInitializationStatus
{
    /// <summary>
    /// 未初始化
    /// </summary>
    Uninitialize,
    /// <summary>
    /// 初始化中
    /// </summary>
    Initializing,
    /// <summary>
    /// 已经初始化
    /// </summary>
    Initialized,
}

/// <summary>
/// 为一般c#类型使用的单例模式
/// </summary>
/// <typeparam name="T">具体的单例类型。</typeparam>
public abstract class Singleton<T> : ISingleton where T : Singleton<T>, new()
{
    private static T? _instance;

    private SingletonInitializationStatus _status;

    // ReSharper disable once StaticMemberInGenericType
    private static readonly object LockObj = new();

    /// <summary>
    /// 返回单例模式实例
    /// </summary>
    /// <value><c>T</c>.</value>
    public static T Instance
    {
        get
        {
            if (_instance != null) return _instance;
                
            lock (LockObj)
            {
                if (_instance == null)
                {
                    var candidate = new T();
                    candidate.Initialize();
                    _instance = candidate;
                }
            }
            return _instance;
        }
    }

    /// <summary>
    /// 判断单例是否已经初始化
    /// </summary>
    /// <returns>如果初始化完毕的话返回<c>true</c></returns>
    public virtual bool IsInitialized() => _status == SingletonInitializationStatus.Initialized;

    /// <summary>
    /// 初始化当前实例；重复调用不会再次执行初始化回调，失败后允许重试。
    /// </summary>
    public virtual void Initialize()
    {
        if (_status != SingletonInitializationStatus.Uninitialize) return;

        _status = SingletonInitializationStatus.Initializing;
        try
        {
            OnInitializing();
            OnInitialized();
            _status = SingletonInitializationStatus.Initialized;
        }
        catch
        {
            _status = SingletonInitializationStatus.Uninitialize;
            throw;
        }
    }

    /// <summary>
    /// 初始化过程中调用
    /// </summary>
    protected virtual void OnInitializing()
    {
    }
        
    /// <summary>
    /// 初始化完毕调用
    /// </summary>
    protected virtual void OnInitialized()
    {
    }

    /// <summary>
    /// 销毁已有实例并创建一个完成初始化的新实例。
    /// </summary>
    public static void Create()
    {
        Destroy();
        _ = Instance;
    }

    /// <summary>
    /// 清理并移除已经存在的单例；实例不存在时不执行操作。
    /// </summary>
    public static void Destroy()
    {
        if (_instance is null) return;

        _instance.Clear();
        _instance = null;
    }

    /// <summary>
    /// 清理当前实例持有的状态。
    /// </summary>
    public virtual void Clear()
    {
    }
}
