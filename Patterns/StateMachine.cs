namespace SimpleFramework.Patterns;

/// <summary>
/// 常用事件常量定义
/// </summary>
public static class StateEvents
{
    /// <summary>表示当前状态已完成的常用事件名。</summary>
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
    internal sealed record SetupConfigurationSnapshot(
        string Name,
        KeyValuePair<string, StateEventHandler>[] EventHandlers,
        Action? OnSetupCallback,
        Action? OnEnterCallback,
        Action<float>? OnUpdateCallback,
        Action? OnExitCallback);

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
        ChildrenStateMachine?.SetActive(false);
        Exit();
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
    /// 添加子状态，并可将其设为子状态机的初始状态。
    /// </summary>
    /// <param name="subState">要添加的子状态。</param>
    /// <param name="isInitState">是否为初始状态</param>
    /// <exception cref="ArgumentNullException"><paramref name="subState"/> 为 <see langword="null"/>。</exception>
    /// <exception cref="InvalidOperationException"><paramref name="subState"/> 已属于其他状态机。</exception>
    /// <remarks>子状态初始化失败时原样传播异常，且不会发布父子关系或状态机所有权。</remarks>
    public void AddState(State subState, bool isInitState=false)
    {
        ArgumentNullException.ThrowIfNull(subState);
        if (subState.StateMachine is not null)
        {
            if (ReferenceEquals(subState.StateMachine, ChildrenStateMachine))
            {
                if (isInitState)
                {
                    ChildrenStateMachine.InitialState = subState;
                }

                return;
            }

            throw new InvalidOperationException("子状态已经属于另一个状态机。");
        }

        var childrenStateMachine = ChildrenStateMachine ?? new StateMachine(this);
        childrenStateMachine.AddState(subState);
        subState._parentStateRef = new WeakReference<State>(this);
        ChildrenStateMachine = childrenStateMachine;
        if (isInitState)
        {
            childrenStateMachine.InitialState = subState;
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
    internal void SetStateMachine(StateMachine? stateMachine)
    {
        StateMachine = stateMachine;
    }

    internal void ClearParentState()
    {
        _parentStateRef = null;
    }

    internal void SetParentState(State? parentState)
    {
        _parentStateRef = parentState is null ? null : new WeakReference<State>(parentState);
    }

    internal SetupConfigurationSnapshot CaptureSetupConfiguration() => new(
        _name,
        [.. _eventHandlers],
        _onSetupCallback,
        _onEnterCallback,
        _onUpdateCallback,
        _onExitCallback);

    internal void RestoreSetupConfiguration(SetupConfigurationSnapshot snapshot)
    {
        _name = snapshot.Name;
        _eventHandlers.Clear();
        foreach (var (eventName, handler) in snapshot.EventHandlers)
        {
            _eventHandlers.Add(eventName, handler);
        }

        _onSetupCallback = snapshot.OnSetupCallback;
        _onEnterCallback = snapshot.OnEnterCallback;
        _onUpdateCallback = snapshot.OnUpdateCallback;
        _onExitCallback = snapshot.OnExitCallback;
    }

    internal void RestoreSetupHierarchy(
        StateMachine? originalChildrenStateMachine,
        StateMachine.SetupSnapshot? originalChildrenSnapshot)
    {
        if (!ReferenceEquals(ChildrenStateMachine, originalChildrenStateMachine))
        {
            ChildrenStateMachine?.DetachAllStates();
            ChildrenStateMachine = originalChildrenStateMachine;
        }

        if (originalChildrenStateMachine is not null && originalChildrenSnapshot is not null)
        {
            originalChildrenStateMachine.RestoreSetupSnapshot(originalChildrenSnapshot);
        }
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
    internal sealed record StateHierarchySnapshot(
        State State,
        State.SetupConfigurationSnapshot Configuration,
        StateMachine? ChildrenStateMachine,
        SetupSnapshot? ChildrenSnapshot);

    internal sealed record SetupSnapshot(
        State[] States,
        Transition[] Transitions,
        KeyValuePair<string, StateEventHandler>[] EventHandlers,
        State? InitialState,
        State? CurrentState,
        bool IsActive,
        bool IsChangingState,
        int SetupDepth,
        bool TriggerUpdateWhenStateChange,
        StateHierarchySnapshot[] StateHierarchies);

    private readonly List<State> _states = new();
    private readonly List<Transition> _transitions = new();
    private readonly Dictionary<string, StateEventHandler> _eventHandlers = new();
    private State? _currentState;
    private State? _initialState;
    private bool _isActive;
    private bool _isChangingState;
    private int _setupDepth;
    private readonly State? _ownerState;

    /// <summary>创建根状态机。</summary>
    public StateMachine()
    {
    }

    internal StateMachine(State ownerState)
    {
        _ownerState = ownerState;
    }

    /// <summary>
    /// 当前活动状态
    /// </summary>
    public State? CurrentState => _currentState;

    /// <summary>
    /// 是否活跃
    /// </summary>
    public bool IsActive => _isActive;

    /// <summary>
    /// 获取或设置激活时进入的初始状态；非空状态必须已属于当前状态机。
    /// </summary>
    /// <exception cref="InvalidOperationException">设置的状态不属于当前状态机。</exception>
    public State? InitialState
    {
        get => _initialState;
        set
        {
            if (value is not null && value.StateMachine != this)
            {
                throw new InvalidOperationException("初始状态必须先添加到当前状态机。");
            }

            _initialState = value;
        }
    }

    /// <summary>
    /// 获取或设置切换状态后是否立即以零间隔更新一次新状态。
    /// </summary>
    public bool TriggerUpdateWhenStateChange { set; get; } = false;

    /// <summary>
    /// 添加状态
    /// </summary>
    /// <param name="state">状态</param>
    /// <exception cref="ArgumentNullException"><paramref name="state"/> 为 <see langword="null"/>。</exception>
    /// <exception cref="InvalidOperationException">状态已经属于另一个状态机。</exception>
    /// <remarks>状态初始化失败时原样传播异常，并移除其所有权、初始状态和关联转换。</remarks>
    public void AddState(State state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (_states.Contains(state))
            return; // 防止重复添加同一个状态
        if (state.StateMachine is not null)
        {
            throw new InvalidOperationException("状态已经属于另一个状态机。");
        }

        var machineSnapshot = CaptureSetupSnapshot();
        var stateConfigurationSnapshot = state.CaptureSetupConfiguration();
        var originalChildrenStateMachine = state.ChildrenStateMachine;
        var originalChildrenSnapshot = originalChildrenStateMachine?.CaptureSetupSnapshot();
        _states.Add(state);
        state.SetStateMachine(this);
        _setupDepth++;
        try
        {
            state.Setup();
        }
        catch
        {
            _setupDepth--;
            RestoreSetupSnapshot(machineSnapshot);
            state.RestoreSetupHierarchy(originalChildrenStateMachine, originalChildrenSnapshot);
            state.RestoreSetupConfiguration(stateConfigurationSnapshot);
            throw;
        }
        _setupDepth--;
    }

    /// <summary>
    /// 添加转换
    /// </summary>
    /// <param name="fromState">源状态，使用StateEvents.AnyState表示任意状态</param>
    /// <param name="toState">目标状态</param>
    /// <param name="eventName">事件名称</param>
    /// <exception cref="ArgumentNullException"><paramref name="toState"/> 为 <see langword="null"/>。</exception>
    /// <exception cref="ArgumentException"><paramref name="eventName"/> 为 <see langword="null"/> 或空字符串。</exception>
    /// <exception cref="InvalidOperationException">源状态或目标状态不属于当前状态机。</exception>
    public void AddTransition(State? fromState, State toState, string eventName)
    {
        ArgumentNullException.ThrowIfNull(toState);

        if (string.IsNullOrEmpty(eventName))
            throw new ArgumentException("Event name cannot be null or empty");

        if (fromState is not null && fromState.StateMachine != this)
        {
            throw new InvalidOperationException("源状态必须属于当前状态机。");
        }

        if (toState.StateMachine != this)
        {
            throw new InvalidOperationException("目标状态必须属于当前状态机。");
        }

        _transitions.Add(new Transition(fromState, toState, eventName));
    }

    /// <summary>
    /// 添加状态机级别的事件处理器，无论当前状态是什么都会生效。
    /// </summary>
    /// <param name="eventName">事件名称</param>
    /// <param name="handler">事件处理器</param>
    /// <exception cref="ArgumentException"><paramref name="eventName"/> 为 <see langword="null"/> 或空字符串。</exception>
    /// <exception cref="ArgumentNullException"><paramref name="handler"/> 为 <see langword="null"/>。</exception>
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
    /// 激活或停用状态机；激活时进入初始状态，停用时退出当前状态。
    /// </summary>
    /// <param name="active">是否活跃</param>
    /// <exception cref="InvalidOperationException">正在初始化状态或执行状态进入/退出回调，不能变更生命周期。</exception>
    /// <remarks>
    /// 进入或退出回调抛出的异常会原样传播。回调失败时状态机采用失败关闭策略，
    /// <see cref="IsActive"/> 为 <see langword="false"/>，<see cref="CurrentState"/> 为
    /// <see langword="null"/>；调用者可在修复回调条件后显式重新激活。
    /// </remarks>
    public void SetActive(bool active)
    {
        if (IsSetupInHierarchy())
        {
            throw new InvalidOperationException("状态初始化期间不能更改状态机的激活状态。");
        }

        if (_isChangingState)
        {
            throw new InvalidOperationException("状态进入或退出期间不能更改状态机的激活状态。");
        }

        if (active == _isActive) return;

        _isChangingState = true;
        try
        {
            if (active)
            {
                _isActive = true;
                if (_initialState != null)
                {
                    ChangeToState(_initialState);
                }
            }
            else
            {
                _currentState?.ExitState();
                _currentState = null;
                _isActive = false;
            }
        }
        catch
        {
            FailCloseHierarchy();
            throw;
        }
        finally
        {
            _isChangingState = false;
        }
    }

    /// <summary>
    /// 分发事件
    /// </summary>
    /// <param name="eventName">事件名称</param>
    /// <param name="args">事件参数</param>
    /// <returns>事件是否被消耗</returns>
    /// <exception cref="InvalidOperationException">正在初始化状态或执行状态进入/退出回调，不能分发事件。</exception>
    public bool Dispatch(string eventName, object? args = null)
    {
        if (IsSetupInHierarchy())
        {
            throw new InvalidOperationException("状态初始化期间不能分发事件。");
        }

        if (_isChangingState)
        {
            throw new InvalidOperationException("状态进入或退出期间不能分发新的转换事件。");
        }

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
        foreach (var transition in _transitions)
        {
            if (transition.EventName != eventName ||
                (transition.FromState != StateEvents.AnyState && transition.FromState != _currentState))
            {
                continue;
            }

            ChangeToState(transition.ToState);
            return true;
        }

        return false;
    }

    /// <summary>
    /// 更新状态机
    /// </summary>
    /// <param name="delta">帧间隔时间</param>
    /// <exception cref="InvalidOperationException">当前状态机或祖先状态机正在初始化状态。</exception>
    public void Update(float delta)
    {
        if (IsSetupInHierarchy())
        {
            throw new InvalidOperationException("状态初始化期间不能更新状态机。");
        }

        try
        {
            if (_isActive && _currentState != null)
            {
                _currentState.UpdateState(delta);
            }
        }
        catch
        {
            FailCloseHierarchy();
            throw;
        }
    }

    private void ChangeToState(State newState)
    {
        _isChangingState = true;
        try
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
        catch
        {
            FailCloseHierarchy();
            throw;
        }
        finally
        {
            _isChangingState = false;
        }
    }

    internal SetupSnapshot CaptureSetupSnapshot() => new(
        _states.ToArray(),
        _transitions.ToArray(),
        [.. _eventHandlers],
        _initialState,
        _currentState,
        _isActive,
        _isChangingState,
        _setupDepth,
        TriggerUpdateWhenStateChange,
        [.. _states.Select(state => new StateHierarchySnapshot(
            state,
            state.CaptureSetupConfiguration(),
            state.ChildrenStateMachine,
            state.ChildrenStateMachine?.CaptureSetupSnapshot()))]);

    internal void RestoreSetupSnapshot(SetupSnapshot snapshot)
    {
        foreach (var state in _states)
        {
            if (Array.IndexOf(snapshot.States, state) < 0)
            {
                state.ChildrenStateMachine?.DetachAllStates();
                state.SetStateMachine(null);
                state.ClearParentState();
            }
        }

        _states.Clear();
        _states.AddRange(snapshot.States);
        foreach (var state in _states)
        {
            state.SetStateMachine(this);
            state.SetParentState(_ownerState);
        }
        _transitions.Clear();
        _transitions.AddRange(snapshot.Transitions);
        _eventHandlers.Clear();
        foreach (var (eventName, handler) in snapshot.EventHandlers)
        {
            _eventHandlers.Add(eventName, handler);
        }
        _initialState = snapshot.InitialState;
        _currentState = snapshot.CurrentState;
        _isActive = snapshot.IsActive;
        _isChangingState = snapshot.IsChangingState;
        _setupDepth = snapshot.SetupDepth;
        TriggerUpdateWhenStateChange = snapshot.TriggerUpdateWhenStateChange;
        foreach (var stateHierarchy in snapshot.StateHierarchies)
        {
            stateHierarchy.State.RestoreSetupConfiguration(stateHierarchy.Configuration);
            stateHierarchy.State.RestoreSetupHierarchy(
                stateHierarchy.ChildrenStateMachine,
                stateHierarchy.ChildrenSnapshot);
        }
    }

    private bool IsSetupInHierarchy() =>
        _setupDepth > 0 || _ownerState?.StateMachine?.IsSetupInHierarchy() == true;

    private void FailCloseHierarchy()
    {
        _isActive = false;
        _currentState = null;
        _isChangingState = false;
        foreach (var state in _states)
        {
            state.ChildrenStateMachine?.FailCloseHierarchy();
        }
    }

    internal void DetachAllStates()
    {
        foreach (var state in _states)
        {
            state.ChildrenStateMachine?.DetachAllStates();
            state.SetStateMachine(null);
            state.ClearParentState();
        }

        _states.Clear();
        _transitions.Clear();
        _eventHandlers.Clear();
        _initialState = null;
        _currentState = null;
        _isActive = false;
        _isChangingState = false;
        _setupDepth = 0;
        TriggerUpdateWhenStateChange = false;
    }
}
