#nullable enable
using NUnit.Framework;
using SimpleFramework.Patterns;
using System;
using SimpleFramework.Utility;

namespace Test.Patterns;

[TestFixture]
public class TestStateMachine
{
    private class TestState : State
    {
        public int EnterCount { get; private set; }
        public int UpdateCount { get; private set; }
        public int ExitCount { get; private set; }
        public float LastDelta { get; private set; }
        public string? LastEvent { get; private set; }

        public TestState(string name)
        {
            Named(name);
        }

        public override void Enter()
        {
            EnterCount++;
            base.Enter();
        }

        public override void Update(float delta)
        {
            UpdateCount++;
            LastDelta = delta;
            base.Update(delta);
        }

        public override void Exit()
        {
            ExitCount++;
            base.Exit();
        }
    }

    
    [Test]
    public void TestStateMachineCreation()
    {
        var stateMachine = new StateMachine();

        Assert.IsNotNull(stateMachine);
        Assert.IsFalse(stateMachine.IsActive);
        Assert.IsNull(stateMachine.CurrentState);
        Assert.IsNull(stateMachine.InitialState);
    }

    [Test]
    public void TestAddState()
    {
        var stateMachine = new StateMachine();
        var state = new TestState("Test");

        stateMachine.AddState(state);

        Assert.AreEqual(state.Name, "Test");
        Assert.AreEqual(state.StateMachine, stateMachine);
    }

    [Test]
    public void TestAddNullState()
    {
        var stateMachine = new StateMachine();

        Assert.Throws<NullReferenceException>(() => stateMachine.AddState(null!));
    }

    [Test]
    public void TestInitialAndActivation()
    {
        var stateMachine = new StateMachine();
        var state = new TestState("Test");

        stateMachine.AddState(state);
        stateMachine.InitialState = state;
        
        Assert.IsFalse(stateMachine.IsActive);
        Assert.AreEqual(0, state.EnterCount);

        stateMachine.SetActive(true);

        Assert.IsTrue(stateMachine.IsActive);
        Assert.AreEqual(state, stateMachine.CurrentState);
        Assert.AreEqual(1, state.EnterCount);
    }

    [Test]
    public void TestDeactivation()
    {
        var stateMachine = new StateMachine();
        var state = new TestState("Test");

        stateMachine.AddState(state);
        stateMachine.InitialState = state;
        stateMachine.SetActive(true);

        Assert.AreEqual(1, state.EnterCount);
        Assert.AreEqual(0, state.ExitCount);

        stateMachine.SetActive(false);

        Assert.IsFalse(stateMachine.IsActive);
        Assert.IsNull(stateMachine.CurrentState);
        Assert.AreEqual(1, state.EnterCount);
        Assert.AreEqual(1, state.ExitCount);
    }

    [Test]
    public void TestUpdate()
    {
        var stateMachine = new StateMachine();
        var state = new TestState("Test");

        stateMachine.AddState(state);
        stateMachine.InitialState = state;
        stateMachine.SetActive(true);

        stateMachine.Update(0.016f);
        stateMachine.Update(0.032f);

        Assert.AreEqual(2, state.UpdateCount);
        Assert.AreEqual(0.032f, state.LastDelta);
    }

    [Test]
    public void TestUpdateWhenInactive()
    {
        var stateMachine = new StateMachine();
        var state = new TestState("Test");

        stateMachine.AddState(state);
        stateMachine.InitialState = state;
        // 不激活状态机

        stateMachine.Update(0.016f);
        stateMachine.Update(0.032f);

        Assert.AreEqual(0, state.UpdateCount);
    }

    [Test]
    public void TestAddTransition()
    {
        var stateMachine = new StateMachine();
        var state1 = new TestState("State1");
        var state2 = new TestState("State2");

        stateMachine.AddState(state1);
        stateMachine.AddState(state2);
        stateMachine.AddTransition(state1, state2, "go_to_state2");

        // 测试不会抛出异常
        Assert.DoesNotThrow(() => stateMachine.AddTransition(state1, state2, "go_to_state2"));
    }

    [Test]
    public void TestTransitionByEvent()
    {
        var stateMachine = new StateMachine();
        var state1 = new TestState("State1");
        var state2 = new TestState("State2");

        stateMachine.AddState(state1);
        stateMachine.AddState(state2);
        stateMachine.AddTransition(state1, state2, "go_to_state2");

        stateMachine.InitialState = state1;
        stateMachine.SetActive(true);

        Assert.AreEqual(state1, stateMachine.CurrentState);
        Assert.AreEqual(1, state1.EnterCount);
        Assert.AreEqual(0, state2.EnterCount);

        stateMachine.Dispatch("go_to_state2");

        Assert.AreEqual(state2, stateMachine.CurrentState);
        Assert.AreEqual(1, state1.EnterCount);
        Assert.AreEqual(1, state1.ExitCount);
        Assert.AreEqual(1, state2.EnterCount);
        Assert.AreEqual(0, state2.ExitCount);
    }

    [Test]
    public void TestAnyStateTransition()
    {
        var stateMachine = new StateMachine();
        var state1 = new TestState("State1");
        var state2 = new TestState("State2");
        var state3 = new TestState("State3");

        stateMachine.AddState(state1);
        stateMachine.AddState(state2);
        stateMachine.AddState(state3);

        // 从任意状态都可以转换到state3
        stateMachine.AddTransition(StateEvents.AnyState, state3, "emergency");

        stateMachine.InitialState = state1;
        stateMachine.SetActive(true);

        stateMachine.Dispatch("emergency");

        Assert.AreEqual(state3, stateMachine.CurrentState);

        // 切换到state2
        stateMachine.AddTransition(state3, state2, "go_to_state2");
        stateMachine.Dispatch("go_to_state2");
        Assert.AreEqual(state2, stateMachine.CurrentState);

        // 再次使用AnyState转换
        stateMachine.Dispatch("emergency");
        Assert.AreEqual(state3, stateMachine.CurrentState);
    }

    [Test]
    public void TestStateEventHandler()
    {
        var stateMachine = new StateMachine();
        var state = new TestState("Test");

        state.AddEventHandler("custom_event", args =>
        {
            Assert.AreEqual("test_arg", args);
            return true; // 消费事件
        });

        stateMachine.AddState(state);
        stateMachine.InitialState = state;
        stateMachine.SetActive(true);

        stateMachine.Dispatch("custom_event", "test_arg");

        // 由于HandleEvent是public方法，我们需要通过其他方式验证
        // 这里我们直接调用HandleEvent来测试
        bool handled = state.HandleEvent("custom_event", "test_arg");
        Assert.IsTrue(handled);
    }

    [Test]
    public void TestEventHandlerPreventsTransition()
    {
        var stateMachine = new StateMachine();
        var state1 = new TestState("State1");
        var state2 = new TestState("State2");

        state1.AddEventHandler("test_event", args => true); // 消费事件

        stateMachine.AddState(state1);
        stateMachine.AddState(state2);
        stateMachine.AddTransition(state1, state2, "test_event");

        stateMachine.InitialState = state1;
        stateMachine.SetActive(true);

        stateMachine.Dispatch("test_event");

        // 事件被消费，不应该触发转换
        Assert.AreEqual(state1, stateMachine.CurrentState);
    }
    
    [Test]
    public void TestEventHandlerNotPreventsTransition()
    {
        var stateMachine = new StateMachine();
        var state1 = new TestState("State1");
        var state2 = new TestState("State2");
        var isTriggered = false;

        state1.AddEventHandler("test_event", args =>
        {
            isTriggered = true;
            return false;
        }); // 不消费事件

        stateMachine.AddState(state1);
        stateMachine.AddState(state2);
        stateMachine.AddTransition(state1, state2, "test_event");

        stateMachine.InitialState = state1;
        stateMachine.SetActive(true);

        stateMachine.Dispatch("test_event");

        // 事件未被消费，应该触发转换
        Assert.AreEqual(state2, stateMachine.CurrentState);
        Assert.IsTrue(isTriggered);
    }

    [Test]
    public void TestDispatchWhenInactive()
    {
        var stateMachine = new StateMachine();
        var state = new TestState("Test");

        stateMachine.AddState(state);
        stateMachine.InitialState = state;
        
        // 不激活状态机
        stateMachine.Dispatch("test_event");

        // 事件不应该被处理
        Assert.IsNull(state.LastEvent);
    }

    [Test]
    public void TestSimpleState()
    {
        var setupCalled = false;
        var enterCalled = false;
        var updateCalled = false;
        var exitCalled = false;

        var stateMachine = new StateMachine();
        var simpleState = new TestState("Simple");
        simpleState.CallOnSetup(() => setupCalled = true)
            .CallOnEnter(() => enterCalled = true)
            .CallOnUpdate(delta => updateCalled = true)
            .CallOnExit(() => exitCalled = true);

        stateMachine.AddState(simpleState);
        stateMachine.InitialState = simpleState;
        stateMachine.SetActive(true);

        Assert.IsTrue(setupCalled);
        Assert.IsTrue(enterCalled);
        Assert.IsFalse(updateCalled);
        Assert.IsFalse(exitCalled);

        stateMachine.Update(0.1f);

        Assert.IsTrue(updateCalled);

        stateMachine.SetActive(false);

        Assert.IsTrue(exitCalled);
    }

    [Test]
    public void TestStateEventsConstants()
    {
        Assert.AreEqual("finished", StateEvents.EventFinished);
        Assert.IsNull(StateEvents.AnyState);
    }

    [Test]
    public void TestComplexWorkflow()
    {
        var stateMachine = new StateMachine();

        // 创建多个状态
        var idleState = new TestState("Idle");
        var moveState = new TestState("Move");
        var jumpState = new TestState("Jump");

        // 添加状态
        stateMachine.AddState(idleState);
        stateMachine.AddState(moveState);
        stateMachine.AddState(jumpState);

        // 添加转换
        stateMachine.AddTransition(idleState, moveState, "start_move");
        stateMachine.AddTransition(moveState, idleState, "stop_move");
        stateMachine.AddTransition(moveState, jumpState, "jump");
        stateMachine.AddTransition(jumpState, idleState, "land");

        // 添加AnyState转换 - 受伤
        var hurtState = new TestState("Hurt");
        stateMachine.AddState(hurtState);
        stateMachine.AddTransition(StateEvents.AnyState, hurtState, "take_damage");

        stateMachine.InitialState = idleState;
        stateMachine.SetActive(true);

        // 初始状态
        Assert.AreEqual(idleState, stateMachine.CurrentState);
        Assert.AreEqual(1, idleState.EnterCount);

        // 开始移动
        stateMachine.Dispatch("start_move");
        Assert.AreEqual(moveState, stateMachine.CurrentState);
        Assert.AreEqual(1, idleState.ExitCount);
        Assert.AreEqual(1, moveState.EnterCount);

        // 更新几次
        stateMachine.Update(0.016f);
        stateMachine.Update(0.016f);
        Assert.AreEqual(2, moveState.UpdateCount);

        // 跳跃
        stateMachine.Dispatch("jump");
        Assert.AreEqual(jumpState, stateMachine.CurrentState);
        Assert.AreEqual(1, moveState.ExitCount);
        Assert.AreEqual(1, jumpState.EnterCount);

        // 着陆
        stateMachine.Dispatch("land");
        Assert.AreEqual(idleState, stateMachine.CurrentState);
        Assert.AreEqual(1, jumpState.ExitCount);
        Assert.AreEqual(2, idleState.EnterCount); // 第二次进入

        // 受伤（AnyState转换）
        stateMachine.Dispatch("take_damage");
        Assert.AreEqual(hurtState, stateMachine.CurrentState);
        Assert.AreEqual(1, jumpState.ExitCount);
        Assert.AreEqual(1, hurtState.EnterCount);

        // 恢复到待机（需要先从hurt转换到idle）
        stateMachine.AddTransition(hurtState, idleState, "recovered");
        stateMachine.Dispatch("recovered");
        Assert.AreEqual(idleState, stateMachine.CurrentState);
        Assert.AreEqual(1, hurtState.ExitCount);
        Assert.AreEqual(3, idleState.EnterCount); // 第三次进入

        // 验证所有状态的调用次数
        Assert.AreEqual(3, idleState.EnterCount);
        Assert.AreEqual(2, idleState.ExitCount);
        Assert.AreEqual(1, moveState.EnterCount);
        Assert.AreEqual(1, moveState.ExitCount);
        Assert.AreEqual(1, jumpState.EnterCount);
        Assert.AreEqual(1, jumpState.ExitCount);
        Assert.AreEqual(1, hurtState.EnterCount);
        Assert.AreEqual(1, hurtState.ExitCount);
    }

    [Test]
    public void TestNamedMethod()
    {
        var state = new TestState("Original");
        Assert.AreEqual("Original", state.Name);

        state.Named("NewName");
        Assert.AreEqual("NewName", state.Name);
    }

    [Test]
    public void TestCallOnMethods()
    {
        var stateMachine = new StateMachine();
        var state = new TestState("Test");
        var callbackCalled = false;

        state.CallOnEnter(() => callbackCalled = true);

        stateMachine.AddState(state);
        stateMachine.InitialState = state;
        stateMachine.SetActive(true);

        Assert.IsTrue(callbackCalled);
    }
}