using NUnit.Framework;
using SimpleFramework;
using SimpleFramework.FrameworkImpl;

namespace Test.Framework;

/// <summary>验证可绑定属性的通知、比较、取消订阅和重入约束。</summary>
[TestFixture]
public sealed class BindablePropertyTests
{
    [Test]
    public void ParameterlessRegistrationRejectsNullWithoutBreakingLaterNotifications()
    {
        var property = new BindableProperty<int>();
        var error = Assert.Throws<ArgumentNullException>(() => ((IEvent)property).Register(null!));
        Assert.That(error!.ParamName, Is.EqualTo("onEvent"));
        Assert.That(property.Value, Is.Zero);

        var calls = 0;
        using var token = ((IEvent)property).Register(() => calls++);
        Assert.DoesNotThrow(() => property.Value = 1);
        Assert.That(property.Value, Is.EqualTo(1));
        Assert.That(calls, Is.EqualTo(1));
    }

    [Test]
    public void TokenCancelsItsOwnDuplicateSubscriptionWithoutChangingRemainingOrder()
    {
        var property = new BindableProperty<int>();
        var calls = new List<string>();
        Action<int, int> firstHandler = (_, _) => calls.Add("A");
        var first = property.Register(firstHandler);
        property.Register((_, _) => calls.Add("B"));
        property.Register(firstHandler);

        first.UnRegister();
        first.UnRegister();
        property.Value = 1;

        Assert.That(calls, Is.EqualTo(new[] { "B", "A" }));
    }

    [Test]
    public void TokenAfterManualUnregisterDoesNotRemoveAnotherDuplicate()
    {
        var property = new BindableProperty<int>();
        var calls = 0;
        Action<int, int> handler = (_, _) => calls++;
        property.Register(handler);
        var last = property.Register(handler);

        property.UnRegister(handler);
        last.UnRegister();
        property.Value = 1;

        Assert.That(calls, Is.EqualTo(1));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void InitialNotificationRejectsDifferentWritesAndRecoversAfterFailure(bool silent)
    {
        var property = new BindableProperty<int>(10);
        var calls = 0;
        Assert.Throws<InvalidOperationException>(() => property.RegisterWithNotify((_, current) =>
        {
            calls++;
            if (silent) property.SetValueWithoutNotify(current - 1);
            else property.Value = current - 1;
        }));

        Assert.That(property.Value, Is.EqualTo(10));
        Assert.DoesNotThrow(() => property.Value = 9);
        Assert.That(calls, Is.EqualTo(1), "失败的首次通知不能留下订阅。");
    }

    [TestCase(false)]
    [TestCase(true)]
    public void NestedInitialNotificationPreservesOuterWriteGuard(bool throws)
    {
        var property = new BindableProperty<int>();
        using var token = property.Register((_, current) =>
        {
            if (throws)
                Assert.Throws<ApplicationException>(() => property.RegisterWithNotify((_, _) => throw new ApplicationException()));
            else
            {
                using var nested = property.RegisterWithNotify((_, value) => property.Value = value);
            }
            Assert.Throws<InvalidOperationException>(() => property.SetValueWithoutNotify(current + 1));
        });

        property.Value = 1;
        Assert.That(property.Value, Is.EqualTo(1));
        property.Value = 2;
        Assert.That(property.Value, Is.EqualTo(2));
    }

    [Test]
    public void ValueChangeReportsPreviousAndCurrentValues()
    {
        var property = new BindableProperty<int>(10);
        var changes = new List<(int Previous, int Current)>();
        using var token = property.Register((previous, current) => changes.Add((previous, current)));

        property.Value = 20;
        property.Value = 20;

        Assert.That(property.Value, Is.EqualTo(20));
        Assert.That(changes, Is.EqualTo(new[] { (10, 20) }));
    }

    [Test]
    public void RegisterWithNotifyReportsCurrentValueThenFutureChanges()
    {
        var property = new BindableProperty<string>("ready");
        var changes = new List<(string Previous, string Current)>();
        using var token = property.RegisterWithNotify((previous, current) => changes.Add((previous, current)));

        property.Value = "running";

        Assert.That(changes, Is.EqualTo(new[]
        {
            ("ready", "ready"),
            ("ready", "running")
        }));
    }

    [Test]
    public void TokenIsIdempotentAndStopsFutureNotifications()
    {
        var property = new BindableProperty<int>();
        var calls = 0;
        var token = property.Register((_, _) => calls++);

        property.Value = 1;
        token.UnRegister();
        token.UnRegister();
        property.Value = 2;

        Assert.That(calls, Is.EqualTo(1));
    }

    [Test]
    public void SetValueWithoutNotifyChangesValueWithoutCallingListeners()
    {
        var property = new BindableProperty<int>(1);
        var calls = 0;
        using var token = property.Register((_, _) => calls++);

        property.SetValueWithoutNotify(2);

        Assert.That(property.Value, Is.EqualTo(2));
        Assert.That(calls, Is.Zero);
    }

    [Test]
    public void ComparerOnlyAffectsTheConfiguredInstanceAndNullRestoresDefault()
    {
        var tolerant = new BindableProperty<int>(100)
            .WithComparer((previous, current) => Math.Abs(previous - current) < 5);
        var normal = new BindableProperty<int>(100);
        var tolerantCalls = 0;
        var normalCalls = 0;
        using var tolerantToken = tolerant.Register((_, _) => tolerantCalls++);
        using var normalToken = normal.Register((_, _) => normalCalls++);

        tolerant.Value = 103;
        normal.Value = 103;
        tolerant.WithComparer(null).Value = 103;

        Assert.That(tolerantCalls, Is.EqualTo(1));
        Assert.That(normalCalls, Is.EqualTo(1));
        Assert.That(tolerant.Value, Is.EqualTo(103));
    }

    [Test]
    public void DifferentReentrantWriteIsRejectedAndNotificationStateRecovers()
    {
        var property = new BindableProperty<int>(10);
        var token = property.Register((_, current) => property.Value = current - 1);

        Assert.Throws<InvalidOperationException>(() => property.Value = 9);
        Assert.That(property.Value, Is.EqualTo(9));

        token.UnRegister();
        Assert.DoesNotThrow(() => property.Value = 8);
        Assert.That(property.Value, Is.EqualTo(8));
    }

    [Test]
    public void EquivalentReentrantWriteIsANoOp()
    {
        var property = new BindableProperty<int>(1);
        using var token = property.Register((_, current) => property.Value = current);

        Assert.DoesNotThrow(() => property.Value = 2);
        Assert.That(property.Value, Is.EqualTo(2));
    }

    [Test]
    public void SubscriptionChangesDuringNotificationAffectOnlyFutureChanges()
    {
        var property = new BindableProperty<int>();
        var calls = new List<string>();
        IUnRegister? second = null;
        using var first = property.Register((_, _) =>
        {
            calls.Add("first");
            second?.UnRegister();
            property.Register((_, _) => calls.Add("late"));
        });
        second = property.Register((_, _) => calls.Add("second"));

        property.Value = 1;
        Assert.That(calls, Is.EqualTo(new[] { "first", "second" }));

        calls.Clear();
        property.Value = 2;
        Assert.That(calls, Is.EqualTo(new[] { "first", "late" }));
    }
}
