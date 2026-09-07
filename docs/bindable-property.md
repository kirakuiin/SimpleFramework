# BindableProperty 使用说明

`BindableProperty<T>` 用于保存一个值，并在值发生变化时同步通知监听器。它适合放在 Model 或普通业务对象中表达可观察状态，不依赖 Domain，也不负责线程调度。

## 创建、读取与写入

```csharp
var hp = new BindableProperty<int>(100);

Console.WriteLine(hp.Value); // 100
hp.Value = 80;
```

`IBindableProperty<T>` 暴露读写能力，`IReadonlyBindableProperty<T>` 只暴露读取和订阅能力。公开只读接口可以避免外部调用者直接修改状态：

```csharp
public sealed class PlayerModel : AbstractModel
{
    private readonly BindableProperty<int> _hp = new(100);

    public IReadonlyBindableProperty<int> Hp => _hp;

    public void TakeDamage(int amount)
    {
        _hp.Value -= amount;
    }

    protected override void OnInitialize() { }
}
```

## 订阅与取消订阅

`Register` 只订阅未来变化，回调参数依次为旧值和新值：

```csharp
var hp = new BindableProperty<int>(100);
using var token = hp.Register((previous, current) =>
    Console.WriteLine($"HP: {previous} -> {current}"));

hp.Value = 80; // 输出 HP: 100 -> 80
```

`RegisterWithNotify` 会先用当前值同步调用一次回调，再完成订阅。第一次回调的旧值和新值相同：

```csharp
using var token = hp.RegisterWithNotify((previous, current) =>
    RenderHp(current));
```

返回的 `IUnRegister` 同时实现 `IDisposable`。调用 `UnRegister()`、`Dispose()` 或离开 `using` 作用域都会取消该 token 对应的那次订阅；重复取消没有副作用。同一个委托可以多次注册，各 token 的身份独立。也可以把原委托传给 `UnRegister`，取消最后一次匹配的注册；之后使用该注册的旧 token 不会误删其他订阅。

通知是同步、按注册顺序执行的。监听器抛出异常时，异常直接返回给写入方，后续监听器不会继续执行。通知期间新增或移除监听器只影响之后的写入。

## 自定义相等判断

只有比较器判定为不相等时才写入和通知。默认使用 `EqualityComparer<T>.Default`：

```csharp
var position = new BindableProperty<float>(0f)
    .WithComparer((previous, current) =>
        Math.Abs(previous - current) < 0.01f);
```

比较器属于当前 `BindableProperty` 实例，不会影响相同泛型类型的其他实例。向 `WithComparer` 传入 `null` 会恢复默认比较器。

## 不发送通知地写入

`SetValueWithoutNotify` 使用相同的比较器和重入保护更新值，但不会调用监听器：

```csharp
hp.SetValueWithoutNotify(100);
```

它适合加载初始快照等明确不应触发业务反应的场景。正常状态变化仍应优先写入 `Value`，以免观察者状态不同步。

## 通知期间的写入限制

监听器执行期间（包括 `RegisterWithNotify` 的首次通知）再次写入不同值会抛出 `InvalidOperationException`，防止嵌套通知让其他监听器观察到错乱顺序。首次通知抛出异常时不会留下订阅；嵌套首次通知结束后仍保留外层通知的写入保护。按当前比较器判定相等的写入仍是无操作：

```csharp
hp.Register((_, current) =>
{
    hp.Value = current;     // 相等，无操作
    // hp.Value = current - 1; // 不同，抛出 InvalidOperationException
});
```

如果一次变化需要引发另一次变化，应在当前同步通知完成后，由业务调度下一次写入。

## 继承扩展

需要把值映射到外部存储时，可以继承 `BindableProperty<T>` 并重写 `GetValue`、`SetValue`。比较、通知和重入约束仍由基类统一处理：

```csharp
public sealed class SettingsVolumeProperty : BindableProperty<float>
{
    protected override float GetValue() => Settings.Volume;
    protected override void SetValue(float value) => Settings.Volume = value;
}
```

`BindableProperty<T>` 本身不加锁。与 Domain 一样，应用应在所属对象的拥有线程上串行读取、写入和管理订阅。
