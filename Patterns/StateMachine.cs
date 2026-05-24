namespace SimpleFramework.Patterns;

/// <summary>
/// 常用事件常量定义
/// </summary>
public static class StateEvents
{
    public const string EventFinished = "finished";

    /// <summary>
    /// ANY状态常量，用于添加从任意状态的转换
    /// </summary>
    public static readonly State? AnyState = null;
}

/// <summary>
/// 事件处理器委托
/// </summary>
/// <param name="args">事件参数</param>
/// <returns>是否消费了该事件</returns>
public delegate bool StateEventHandler(object? args = null);

/// <summary>
/// 状态基类
/// </summary>
public class State
{
    private string _name = "";
    private readonly Dictionary<string, StateEventHandler> _eventHandlers = new();
    private WeakReference<State>? _parentStateRef;

    /// <summary>
    /// 状态名称
    /// </summary>
    public string Name => string.IsNullOrEmpty(_name) ? GetType().Name : _name;

    /// <summary>
    /// 状态机引用
    /// </summary>
    public StateMachine? StateMachine { get; private set; }
    
    /// <summary>
    /// 如果有子状态，那么这个代表子状态使用的状态机
    /// </summary>
    public StateMachine? ChildrenStateMachine { get; private set; }
    
    /// <summary>
    /// 父状态
    /// </summary>
    private State? ParentState => _parentStateRef != null && _parentStateRef.TryGetTarget(out var state) ? state : null;

    /// <summary>
    /// 设置状态名称，返回自身支持链式调用
    /// </summary>
    public State Named(string name)
    {
        _name = name;
        return this;
    }

    /// <summary>
    /// 初始化状态，只调用一次
    /// </summary>
    public virtual void Setup()
    {
        _onSetupCallback?.Invoke();
    }

    /// <summary>
    /// 进入状态时调用
    /// </summary>
    public virtual void Enter()
    {
        _onEnterCallback?.Invoke();
    }

    /// <summary>
    /// 每帧更新时调用
    /// </summary>
    /// <param name="delta">帧间隔时间</param>
    public virtual void Update(float delta)
    {
        _onUpdateCallback?.Invoke(delta);
    }

    internal void EnterState()
    {
        Enter();
        ChildrenStateMachine?.SetActive(true);
    }

    internal void ExitState()
    {
        Exit();
        ChildrenStateMachine?.SetActive(false);
    }

    internal void UpdateState(float delta)
    {
        Update(delta);
        ChildrenStateMachine?.Update(delta);
    }

    /// <summary>
    /// 退出状态时调用
    /// </summary>
    public virtual void Exit()
    {
        _onExitCallback?.Invoke();
    }

    /// <summary>
    /// 分发事件, 如果事件未被处理会传递到父状态
    /// </summary>
    /// <param name="eventName">事件名称</param>
    /// <param name="args">事件参数</param>
    public void Dispatch(string eventName, object? args = null)
    {
        if (StateMachine is null) return;
        var isConsume = StateMachine.Dispatch(eventName, args);
        if (!isConsume)
        {
            ParentState?.Dispatch(eventName, args);
        }
    }
    
    /// <summary>
    /// 获得状态的深度，对于非子状态来说，深度是1, 嵌套越深，深度越大
    /// </summary>
    public int Depth
    {
        get
        {
            if (ParentState is null) return 1;
            return ParentState.Depth + 1;
        }
    }

    /// <summary>
    /// 添加子状态
    /// </summary>
    /// <param name="subState"></param>
    /// <param name="isInitState">是否为初始状态</param>
    public void AddState(State subState, bool isInitState=false)
    {
        ChildrenStateMachine ??= new StateMachine();
        subState._parentStateRef = new WeakReference<State>(this);
        ChildrenStateMachine.AddState(subState);
        if (isInitState)
        {
            ChildrenStateMachine.InitialState = subState;
        }
    }

    /// <summary>
    /// 添加事件处理器
    /// </summary>
    /// <param name="eventName">事件名称</param>
    /// <param name="handler">事件处理器</param>
    public void AddEventHandler(string eventName, StateEventHandler handler)
    {
        if (string.IsNullOrEmpty(eventName))
            throw new ArgumentException("Event name cannot be null or empty");

        ArgumentNullException.ThrowIfNull(handler);

        // 如果已存在同名事件处理器，发出警告但不阻止覆盖
        if (_eventHandlers.ContainsKey(eventName))
        {
            PatternLogger.Warning($"Duplicate event handler for {eventName}");
        }

        _eventHandlers[eventName] = handler;
    }

    /// <summary>
    /// 处理事件
    /// </summary>
    /// <param name="eventName">事件名称</param>
    /// <param name="args">事件参数</param>
    /// <returns>是否处理了事件</returns>
    public bool HandleEvent(string eventName, object? args = null)
    {
        // 先检查事件处理器
        return _eventHandlers.TryGetValue(eventName, out var handler) && handler(args);
    }

    private Action? _onSetupCallback;
    private Action? _onEnterCallback;
    private Action<float>? _onUpdateCallback;
    private Action? _onExitCallback;
    
    /// <summary>
    /// 设置初始化回调，支持链式调用
    /// </summary>
    public State CallOnSetup(Action callback)
    {
        _onSetupCallback = callback;
        return this;
    }

    /// <summary>
    /// 设置进入回调，支持链式调用
    /// </summary>
    public State CallOnEnter(Action callback)
    {
        _onEnterCallback = callback;
        return this;
    }

    /// <summary>
    /// 设置更新回调，支持链式调用
    /// </summary>
    public State CallOnUpdate(Action<float> callback)
    {
        _onUpdateCallback = callback;
        return this;
    }

    /// <summary>
    /// 设置退出回调，支持链式调用
    /// </summary>
    public State CallOnExit(Action callback)
    {
        _onExitCallback = callback;
        return this;
    }

    /// <summary>
    /// 内部设置状态机
    /// </summary>
    internal void SetStateMachine(StateMachine stateMachine)
    {
        StateMachine = stateMachine;
    }
}

/// <summary>
/// 状态转换定义
/// </summary>
public class Transition(State? fromState, State toState, string eventName)
{
    public State? FromState { get; } = fromState;
    public State ToState { get; } = toState;
    public string EventName { get; } = eventName;
}

/// <summary>
/// 一个基于事件和转换的简单状态机
/// </summary>
public class StateMachine
{
    private readonly List<State> _states = new();
    private readonly List<Transition> _transitions = new();
    private readonly Dictionary<string, StateEventHandler> _eventHandlers = new();
    private State? _currentState;
    private State? _initialState;
    private bool _isActive;

    /// <summary>
    /// 当前活动状态
    /// </summary>
    public State? CurrentState => _currentState;

    /// <summary>
    /// 是否活跃
    /// </summary>
    public bool IsActive => _isActive;

    /// <summary>
    /// 初始状态
    /// </summary>
    public State? InitialState
    {
        get => _initialState;
        set => _initialState = value;
    }

    /// <summary>
    /// 当切换状态时是否立即触发一次Update
    /// </summary>
    public bool TriggerUpdateWhenStateChange { set; get; } = false;

    /// <summary>
    /// 添加状态
    /// </summary>
    /// <param name="state">状态</param>
    public void AddState(State state)
    {
        if (_states.Contains(state))
            return; // 防止重复添加同一个状态

        _states.Add(state);
        state.SetStateMachine(this);
        state.Setup();
    }

    /// <summary>
    /// 添加转换
    /// </summary>
    /// <param name="fromState">源状态，使用StateEvents.AnyState表示任意状态</param>
    /// <param name="toState">目标状态</param>
    /// <param name="eventName">事件名称</param>
    public void AddTransition(State? fromState, State toState, string eventName)
    {
        ArgumentNullException.ThrowIfNull(toState);

        if (string.IsNullOrEmpty(eventName))
            throw new ArgumentException("Event name cannot be null or empty");

        _transitions.Add(new Transition(fromState, toState, eventName));
    }

    /// <summary>
    /// 添加状态机级别的事件处理器，无论当前状态是什么都会生效。
    /// </summary>
    /// <param name="eventName">事件名称</param>
    /// <param name="handler">事件处理器</param>
    public void AddEventHandler(string eventName, StateEventHandler handler)
    {
        if (string.IsNullOrEmpty(eventName))
            throw new ArgumentException("Event name cannot be null or empty");

        ArgumentNullException.ThrowIfNull(handler);

        if (_eventHandlers.ContainsKey(eventName))
        {
            PatternLogger.Warning($"Duplicate state machine event handler for {eventName}");
        }

        _eventHandlers[eventName] = handler;
    }

    /// <summary>
    /// 设置活跃状态
    /// </summary>
    /// <param name="active">是否活跃</param>
    public void SetActive(bool active)
    {
        if (active == _isActive) return;

        _isActive = active;

        if (active)
        {
            if (_initialState != null)
            {
                ChangeToState(_initialState);
            }
        }
        else
        {
            _currentState?.ExitState();
            _currentState = null;
        }
    }

    /// <summary>
    /// 分发事件
    /// </summary>
    /// <param name="eventName">事件名称</param>
    /// <param name="args">事件参数</param>
    /// <returns>事件是否被消耗</returns>
    public bool Dispatch(string eventName, object? args = null)
    {
        PatternLogger.Info($"状态事件: {eventName}");
        if (!_isActive || _currentState == null) return false;

        if (_currentState.HandleEvent(eventName, args))
        {
            return true;
        }

        if (_eventHandlers.TryGetValue(eventName, out var handler) && handler(args))
        {
            return true;
        }

        // 检查转换
        var matchingTransitions = _transitions
            .Where(t => t.EventName == eventName &&
                        (t.FromState == StateEvents.AnyState || t.FromState == _currentState))
            .ToList();

        if (matchingTransitions.Count <= 0) return false;
        ChangeToState(matchingTransitions[0].ToState);
        return true;
    }

    /// <summary>
    /// 更新状态机
    /// </summary>
    /// <param name="delta">帧间隔时间</param>
    public void Update(float delta)
    {
        if (_isActive && _currentState != null)
        {
            _currentState.UpdateState(delta);
        }
    }

    private void ChangeToState(State newState)
    {
        var previousState = _currentState;
        _currentState?.ExitState();
        _currentState = newState;
        _currentState.EnterState();
        
        PatternLogger.Info($"状态转移: [{previousState?.Name}]=>[{_currentState.Name}]");
        
        if (TriggerUpdateWhenStateChange)
        {
            _currentState.UpdateState(0);
        }
        
    }
}
