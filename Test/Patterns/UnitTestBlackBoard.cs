#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using SimpleFramework.Patterns;

namespace Test.Patterns;

[TestFixture]
public class TestBlackBoard
{
    private BlackBoard _blackBoard;

    [SetUp]
    public void Setup()
    {
        _blackBoard = new BlackBoard("TestBoard");
    }

    [Test]
    public void TestCreation()
    {
        Assert.IsNotNull(_blackBoard);
        Assert.AreEqual("TestBoard", _blackBoard.Name);
        Assert.IsNull(_blackBoard.Parent);
    }

    [Test]
    public void TestSetAndGet()
    {
        _blackBoard.Set("test", 42);
        Assert.AreEqual(42, _blackBoard.Get<int>("test"));
    }

    [Test]
    public void TestSetAndGetDifferentTypes()
    {
        _blackBoard.Set("int", 42);
        _blackBoard.Set("string", "test");
        _blackBoard.Set("double", 3.14);

        Assert.AreEqual(42, _blackBoard.Get<int>("int"));
        Assert.AreEqual("test", _blackBoard.Get<string>("string"));
        Assert.AreEqual(3.14, _blackBoard.Get<double>("double"));
    }

    [Test]
    public void TestGetNonExistentKey()
    {
        Assert.AreEqual(0, _blackBoard.Get<int>("nonExistent"));
    }

    [Test]
    public void TestTryGet()
    {
        _blackBoard.Set("test", 42);
        
        Assert.IsTrue(_blackBoard.TryGet<int>("test", out var value));
        Assert.AreEqual(42, value);
        
        Assert.IsFalse(_blackBoard.TryGet<int>("nonExistent", out _));
    }

    [Test]
    public void TestContains()
    {
        _blackBoard.Set("test", 42);
        
        Assert.IsTrue(_blackBoard.Contains("test"));
        Assert.IsFalse(_blackBoard.Contains("nonExistent"));
    }

    [Test]
    public void TestRemove()
    {
        _blackBoard.Set("test", 42);
        Assert.IsTrue(_blackBoard.Contains("test"));
        
        Assert.IsTrue(_blackBoard.Remove("test"));
        Assert.IsFalse(_blackBoard.Contains("test"));
        
        Assert.IsFalse(_blackBoard.Remove("nonExistent"));
    }

    [Test]
    public void TestClear()
    {
        _blackBoard.Set("test1", 42);
        _blackBoard.Set("test2", "test");
        
        _blackBoard.Clear();
        
        Assert.IsFalse(_blackBoard.Contains("test1"));
        Assert.IsFalse(_blackBoard.Contains("test2"));
    }

    [Test]
    public void TestDataChangedEvent()
    {
        string? changedKey = null;
        object? oldValue = null;
        object? newValue = null;

        _blackBoard.OnDataChanged += (sender, args) =>
        {
            changedKey = args.Key;
            oldValue = args.OldValue;
            newValue = args.NewValue;
        };

        _blackBoard.Set("test", 42);
        Assert.AreEqual("test", changedKey);
        Assert.Zero((int)oldValue!);
        Assert.AreEqual(42, newValue);

        _blackBoard.Set("test", 100);
        Assert.AreEqual("test", changedKey);
        Assert.AreEqual(42, oldValue);
        Assert.AreEqual(100, newValue);

        _blackBoard.Remove("test");
        Assert.AreEqual("test", changedKey);
        Assert.AreEqual(100, oldValue);
        Assert.IsNull(newValue);
    }

    [Test]
    public void TestHierarchy()
    {
        var parent = new BlackBoard("Parent");
        var child = new BlackBoard("Child", parent);
        
        Assert.AreEqual(parent, child.Parent);
        Assert.AreEqual("Child", child.Name);
    }

    [Test]
    public void TestGetKeys()
    {
        _blackBoard.Set("test1", 42);
        _blackBoard.Set("test2", "test");
        
        var keys = _blackBoard.GetKeys();
        Assert.AreEqual(2, keys.Count);
        Assert.Contains("test1", keys.ToList());
        Assert.Contains("test2", keys.ToList());
    }

    [Test]
    public void TestGetValues()
    {
        _blackBoard.Set("test1", 42);
        _blackBoard.Set("test2", "test");
        
        var values = _blackBoard.GetValues();
        Assert.AreEqual(2, values.Count);
        Assert.Contains(42, values.ToList());
        Assert.Contains("test", values.ToList());
    }

    [Test]
    public void TestGetEntries()
    {
        _blackBoard.Set("test1", 42);
        _blackBoard.Set("test2", "test");
        
        var entries = _blackBoard.GetEntries();
        Assert.AreEqual(2, entries.Count);
        
        var dict = entries.ToDictionary(e => e.Key, e => e.Value);
        Assert.AreEqual(42, dict["test1"]);
        Assert.AreEqual("test", dict["test2"]);
    }

    [Test]
    public void TestThreadSafety()
    {
        var tasks = new List<Task>();
        for (int i = 0; i < 1000; i++)
        {
            var value = i;
            tasks.Add(Task.Run(() =>
            {
                _blackBoard.Set($"key{value}", value);
                var result = _blackBoard.Get<int>($"key{value}");
                Assert.AreEqual(value, result);
            }));
        }
        
        Task.WaitAll(tasks.ToArray());
    }

    [Test]
    public void TestHierarchicalDataAccess()
    {
        var parent = new BlackBoard("Parent");
        var child = new BlackBoard("Child", parent);
        
        // 在父黑板中设置数据
        parent.Set("shared", 42);
        parent.Set("parentOnly", "parent");
        
        // 在子黑板中设置数据
        child.Set("childOnly", "child");
        
        // 测试子黑板可以访问父黑板的数据
        Assert.AreEqual(42, child.Get<int>("shared"));
        Assert.AreEqual("parent", child.Get<string>("parentOnly"));
        Assert.AreEqual("child", child.Get<string>("childOnly"));
        
        // 测试父黑板不能访问子黑板的数据
        Assert.IsNull(parent.Get<string>("childOnly"));
    }

    [Test]
    public void TestHierarchicalDataAccessWithTryGet()
    {
        var parent = new BlackBoard("Parent");
        var child = new BlackBoard("Child", parent);
        
        // 在父黑板中设置数据
        parent.Set("shared", 42);
        
        // 测试子黑板可以访问父黑板的数据
        Assert.IsTrue(child.TryGet<int>("shared", out var value));
        Assert.AreEqual(42, value);
        
        // 测试不存在的键
        Assert.IsFalse(child.TryGet<int>("nonExistent", out _));
    }

    [Test]
    public void TestHierarchicalDataAccessWithContains()
    {
        var parent = new BlackBoard("Parent");
        var child = new BlackBoard("Child", parent);
        
        // 在父黑板中设置数据
        parent.Set("shared", 42);
        
        // 在子黑板中设置数据
        child.Set("childOnly", "child");
        
        // 测试子黑板可以检查父黑板中的数据
        Assert.IsTrue(child.Contains("shared"));
        Assert.IsTrue(child.Contains("childOnly"));
        
        // 测试父黑板不能检查子黑板中的数据
        Assert.IsFalse(parent.Contains("childOnly"));
    }

    [Test]
    public void TestDeepHierarchy()
    {
        var grandParent = new BlackBoard("GrandParent");
        var parent = new BlackBoard("Parent", grandParent);
        var child = new BlackBoard("Child", parent);
        
        // 在祖父黑板中设置数据
        grandParent.Set("shared", 42);
        
        // 测试子黑板可以访问祖父黑板的数据
        Assert.AreEqual(42, child.Get<int>("shared"));
        Assert.IsTrue(child.Contains("shared"));
        Assert.IsTrue(child.TryGet<int>("shared", out var value));
        Assert.AreEqual(42, value);
    }

    [Test]
    public void TestHierarchicalDataOverride()
    {
        var parent = new BlackBoard("Parent");
        var child = new BlackBoard("Child", parent);
        
        // 在父黑板中设置数据
        parent.Set("shared", 42);
        
        // 在子黑板中覆盖相同键的数据
        child.Set("shared", 100);
        
        // 测试子黑板使用自己的数据
        Assert.AreEqual(100, child.Get<int>("shared"));
        
        // 测试父黑板的数据保持不变
        Assert.AreEqual(42, parent.Get<int>("shared"));
    }

    [Test]
    public void TestMultipleEventHandlers()
    {
        var eventCount = 0;
        var eventCount2 = 0;

        _blackBoard.OnDataChanged += (sender, args) => eventCount++;
        _blackBoard.OnDataChanged += (sender, args) => eventCount2++;

        _blackBoard.Set("test", 42);
        Assert.AreEqual(1, eventCount);
        Assert.AreEqual(1, eventCount2);

        _blackBoard.Set("test", 100);
        Assert.AreEqual(2, eventCount);
        Assert.AreEqual(2, eventCount2);
    }

    [Test]
    public void TestEventUnsubscribe()
    {
        var eventCount = 0;
        EventHandler<BlackBoardEventArgs> handler = (sender, args) => eventCount++;

        _blackBoard.OnDataChanged += handler;
        _blackBoard.Set("test", 42);
        Assert.AreEqual(1, eventCount);

        _blackBoard.OnDataChanged -= handler;
        _blackBoard.Set("test", 100);
        Assert.AreEqual(1, eventCount); // 事件计数不应该增加
    }
} 