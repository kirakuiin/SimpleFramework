namespace SimpleFramework.Patterns;

/// <summary>
/// 代表FSM中的一个状态
/// </summary>
public interface IState
{
    /// <summary>
    /// 进入状态时执行的操作
    /// </summary>
    void Enter();

    /// <summary>
    /// 退出状态机执行的操作
    /// </summary>
    void Exit();
}

/// <summary>
/// 简单的有限状态机实现
/// </summary>
public class FiniteStateMachine
{
}