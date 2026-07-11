#nullable enable
#pragma warning disable CS8602 // NUnit 的非空断言在运行时生效，编译器无法据此收窄类型。
using NUnit.Framework;
using SimpleFramework.Patterns;
using System;

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

        Assert.Throws<ArgumentNullException>(() => stateMachine.AddState(null!));
    }

    [Test]
    public void TestInitialStateMustBelongToMachine()
    {
        var stateMachine = new StateMachine();
        var foreignMachine = new StateMachine();
        var unregistered = new TestState("Unregistered");
        var foreign = new TestState("Foreign");
        foreignMachine.AddState(foreign);

        Assert.Throws<InvalidOperationException>(() => stateMachine.InitialState = unregistered);
        Assert.Throws<InvalidOperationException>(() => stateMachine.InitialState = foreign);
    }

    [Test]
    public void TestTransitionStatesMustBelongToMachine()
    {
        var stateMachine = new StateMachine();
        var owned = new TestState("Owned");
        var unregistered = new TestState("Unregistered");
        stateMachine.AddState(owned);

        Assert.Throws<InvalidOperationException>(() => stateMachine.AddTransition(unregistered, owned, "from"));
        Assert.Throws<InvalidOperationException>(() => stateMachine.AddTransition(owned, unregistered, "to"));
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
    public void TestExitCallbackCannotReenterTransition()
    {
        var stateMachine = new StateMachine();
        var source = new TestState("Source");
        var outerTarget = new TestState("OuterTarget");
        var nestedTarget = new TestState("NestedTarget");
        InvalidOperationException? reentrancyException = null;
        var attempted = false;
        source.CallOnExit(() =>
        {
            if (attempted) return;
            attempted = true;
            reentrancyException = Assert.Throws<InvalidOperationException>(() => stateMachine.Dispatch("nested"));
        });
        stateMachine.AddState(source);
        stateMachine.AddState(outerTarget);
        stateMachine.AddState(nestedTarget);
        stateMachine.AddTransition(source, outerTarget, "outer");
        stateMachine.AddTransition(source, nestedTarget, "nested");
        stateMachine.InitialState = source;
        stateMachine.SetActive(true);

        stateMachine.Dispatch("outer");

        Assert.That(reentrancyException, Is.Not.Null);
        Assert.That(stateMachine.CurrentState, Is.SameAs(outerTarget));
        Assert.That(nestedTarget.EnterCount, Is.Zero);
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

    [Test]
    public void TestTriggerOnce()
    {
        var stateMachine = new StateMachine
        {
            TriggerUpdateWhenStateChange = true
        };
        var state = new TestState("Test");
        var callbackDelta = 1.0f;

        state.CallOnUpdate(delta => callbackDelta = delta);

        stateMachine.AddState(state);
        stateMachine.InitialState = state;
        stateMachine.SetActive(true);

        Assert.AreEqual(0, callbackDelta);
    }

    [Test]
    public void TestConsume()
    {
        var stateMachine = new StateMachine();
        var s1 = new State().Named("state1");
        var s2 = new State().Named("state2");
        
        stateMachine.AddState(s1);
        stateMachine.AddState(s2);
        stateMachine.InitialState = s1;
        stateMachine.SetActive(true);
        
        stateMachine.AddTransition(s1, s2, StateEvents.EventFinished);
        stateMachine.AddTransition(s2, s1, "consume_event");
        
        Assert.IsTrue(stateMachine.Dispatch(StateEvents.EventFinished));
        Assert.IsFalse(stateMachine.Dispatch(StateEvents.EventFinished));
        Assert.IsTrue(stateMachine.Dispatch("consume_event"));
        Assert.AreEqual(s1, stateMachine.CurrentState);
    }

    [Test]
    public void TestInactiveSiblingEventHandlerDoesNotConsumeCurrentStateEvent()
    {
        var stateMachine = new StateMachine();
        var s1 = new State().Named("state1");
        var s2 = new State().Named("state2");

        s2.AddEventHandler("go", _ => true);
        stateMachine.AddState(s1);
        stateMachine.AddState(s2);
        stateMachine.AddTransition(s1, s2, "go");
        stateMachine.InitialState = s1;
        stateMachine.SetActive(true);

        Assert.IsTrue(stateMachine.Dispatch("go"));
        Assert.AreEqual(s2, stateMachine.CurrentState);
    }

    [Test]
    public void TestStateMachineEventHandlerConsumesBeforeTransition()
    {
        var stateMachine = new StateMachine();
        var s1 = new State().Named("state1");
        var s2 = new State().Named("state2");
        var consumed = false;

        stateMachine.AddEventHandler("go", _ =>
        {
            consumed = true;
            return true;
        });
        stateMachine.AddState(s1);
        stateMachine.AddState(s2);
        stateMachine.AddTransition(s1, s2, "go");
        stateMachine.InitialState = s1;
        stateMachine.SetActive(true);

        Assert.IsTrue(stateMachine.Dispatch("go"));
        Assert.IsTrue(consumed);
        Assert.AreEqual(s1, stateMachine.CurrentState);
    }

    [Test]
    public void TestNestedStateMachineBasics()
    {
        // 创建父状态机和子状态
        var parentStateMachine = new StateMachine();
        var parentState = new TestState("Parent");
        var childState1 = new TestState("Child1");
        var childState2 = new TestState("Child2");

        // 添加父状态到主状态机
        parentStateMachine.AddState(parentState);
        parentStateMachine.InitialState = parentState;

        // 验证初始状态
        Assert.AreEqual(1, parentState.Depth); // 根状态深度为1
        Assert.IsNull(parentState.ChildrenStateMachine); // 初始没有子状态机

        // 添加子状态
        parentState.AddState(childState1, true);
        parentState.AddState(childState2);
        
        parentStateMachine.SetActive(true);

        // 验证子状态机创建和状态添加
        Assert.IsNotNull(parentState.ChildrenStateMachine);
        Assert.IsTrue(parentState.ChildrenStateMachine.IsActive); // 父状态激活时子状态机也激活
        // 验证子状态数量 - 通过检查初始状态设置是否成功
        Assert.AreEqual(childState1, parentState.ChildrenStateMachine.CurrentState);

        // 验证深度计算
        Assert.AreEqual(1, parentState.Depth); // 父状态深度
        Assert.AreEqual(2, childState1.Depth); // 子状态深度
        Assert.AreEqual(2, childState2.Depth); // 子状态深度

        // 验证父子关系 - 通过Depth间接验证
        Assert.IsTrue(childState1.Depth > parentState.Depth);
        Assert.IsTrue(childState2.Depth > parentState.Depth);

        // 验证Update级联
        parentStateMachine.Update(0.016f);
        Assert.AreEqual(1, parentState.UpdateCount); // 父状态被更新
        Assert.AreEqual(1, childState1.UpdateCount); // 子状态也被更新（当前子状态）
        Assert.AreEqual(0, childState2.UpdateCount); // 非当前子状态不被更新

        // 验证Enter/Exit时的子状态机激活
        parentStateMachine.SetActive(false);
        Assert.IsFalse(parentState.ChildrenStateMachine.IsActive); // 父状态停用时子状态机也停用
    }

    [Test]
    public void TestNestedStatesExitChildBeforeParent()
    {
        var order = new List<string>();
        var stateMachine = new StateMachine();
        var parent = new State().Named("parent").CallOnExit(() => order.Add("parent-exit"));
        var child = new State().Named("child").CallOnExit(() => order.Add("child-exit"));
        parent.AddState(child, true);
        stateMachine.AddState(parent);
        stateMachine.InitialState = parent;
        stateMachine.SetActive(true);

        stateMachine.SetActive(false);

        Assert.That(order, Is.EqualTo(new[] { "child-exit", "parent-exit" }));
    }

    [Test]
    public void TestNestedStateMachineEventPropagation()
    {
        // 创建嵌套状态机结构
        var stateMachine = new StateMachine();
        var parentState = new TestState("Parent");
        var childState1 = new TestState("Child1");
        var childState2 = new TestState("Child2");

        stateMachine.AddState(parentState);
        stateMachine.InitialState = parentState;

        parentState.AddState(childState1, true);
        parentState.AddState(childState2);
        
        stateMachine.SetActive(true);

        var parentHandledEvent = false;
        var childHandledEvent = false;

        // 父状态事件处理器
        parentState.AddEventHandler("test_event", args => {
            parentHandledEvent = true;
            return false; // 不消耗事件，允许传播
        });
        parentState.AddEventHandler("test_event2", args => {
            parentHandledEvent = true;
            return false; // 不消耗事件，允许传播
        });

        // 子状态事件处理器
        childState1.AddEventHandler("test_event", args => {
            childHandledEvent = true;
            return true; // 消耗事件，阻止传播
        });

        // 分发事件 - 应被子状态处理并消耗
        childState1.Dispatch("test_event");

        Assert.IsTrue(childHandledEvent); // 子状态处理了事件
        Assert.IsFalse(parentHandledEvent); // 父状态未处理事件（被子状态消耗）

        // 重置标志
        parentHandledEvent = false;
        childHandledEvent = false;

        // 创建新的子状态来测试向上传播（避免清理事件处理器）
        var childState3 = new TestState("Child3");
        childState3.AddEventHandler("test_event2", args => false);
        parentState.AddState(childState3);

        // 重新分发事件 - 应传播到父状态（childState3不处理该事件）
        childState3.Dispatch("test_event2");
        Assert.IsTrue(parentHandledEvent); // 父状态处理了事件
    }

    [Test]
    public void TestComplexNestedStateMachine()
    {
        // 创建三层嵌套状态机
        var stateMachine = new StateMachine();
        var rootState = new TestState("Root");
        var parentState = new TestState("Parent");
        var childState1 = new TestState("Child1");
        var childState2 = new TestState("Child2");
        var grandChildState = new TestState("GrandChild");

        stateMachine.AddState(rootState);
        stateMachine.InitialState = rootState;

        // 第二层嵌套
        rootState.AddState(parentState, true);

        // 第三层嵌套
        parentState.AddState(childState1, true);
        parentState.AddState(childState2);

        // 第四层嵌套
        childState1.AddState(grandChildState, true);
        
        stateMachine.SetActive(true);

        // 验证深度计算
        Assert.AreEqual(1, rootState.Depth);
        Assert.AreEqual(2, parentState.Depth);
        Assert.AreEqual(3, childState1.Depth);
        Assert.AreEqual(3, childState2.Depth);
        Assert.AreEqual(4, grandChildState.Depth);

        // 验证级联Update
        stateMachine.Update(0.1f);

        Assert.AreEqual(1, rootState.UpdateCount);
        Assert.AreEqual(1, parentState.UpdateCount);
        Assert.AreEqual(1, childState1.UpdateCount);
        Assert.AreEqual(0, childState2.UpdateCount); // 非当前状态
        Assert.AreEqual(1, grandChildState.UpdateCount);

        // 验证深层事件传播
        var eventHandled = false;
        rootState.AddEventHandler("deep_event", args => {
            eventHandled = true;
            return true;
        });

        // 从最深层分发事件，应该向上传播到根状态
        grandChildState.Dispatch("deep_event");
        Assert.IsTrue(eventHandled);

        // 验证嵌套状态转换不影响父状态
        parentState.ChildrenStateMachine.AddTransition(childState1, childState2, "switch_child");
        parentState.ChildrenStateMachine.Dispatch("switch_child");

        // 父状态机状态不应改变
        Assert.AreEqual(rootState, stateMachine.CurrentState);
        Assert.AreEqual(parentState, rootState.ChildrenStateMachine.CurrentState);
        Assert.AreEqual(childState2, parentState.ChildrenStateMachine.CurrentState); // 只有子状态改变
    }
}
