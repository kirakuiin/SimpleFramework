using NUnit.Framework;
using SimpleFramework;

namespace Test.Framework;

/// <summary>验证可绑定属性的通知、比较、取消订阅和重入约束。</summary>
[TestFixture]
public sealed class BindablePropertyTests
{
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
