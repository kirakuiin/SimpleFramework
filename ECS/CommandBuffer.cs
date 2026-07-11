namespace SimpleFramework.ECS;

/// <summary>
/// 延迟实体命令中的占位实体句柄，仅在创建它的命令批次中有效。
/// </summary>
public readonly struct BufferedEntity : IEquatable<BufferedEntity>
{
    internal BufferedEntity(int id)
        : this(0, id)
    {
    }

    internal BufferedEntity(long ownerId, int id)
    {
        OwnerId = ownerId;
        Id = id;
    }

    internal long OwnerId { get; }

    /// <summary>
    /// 占位实体在来源命令批次内的局部编号。
    /// </summary>
    public int Id { get; }

    /// <summary>
    /// 判断两个占位句柄是否来自同一命令批次且局部编号相同。
    /// </summary>
    /// <param name="other">要比较的占位句柄。</param>
    /// <returns>如果两个句柄标识同一来源批次内的占位实体则为 <c>true</c>。</returns>
    public bool Equals(BufferedEntity other)
    {
        return OwnerId == other.OwnerId && Id == other.Id;
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj)
    {
        return obj is BufferedEntity other && Equals(other);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        return HashCode.Combine(OwnerId, Id);
    }

    public static bool operator ==(BufferedEntity left, BufferedEntity right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(BufferedEntity left, BufferedEntity right)
    {
        return !left.Equals(right);
    }
}

/// <summary>
/// 命令批次播放结果，用于解析该次成功播放所创建的实体。
/// </summary>
public sealed class CommandBufferResult
{
    private readonly IReadOnlyDictionary<BufferedEntity, Entity> _createdEntities;

    internal CommandBufferResult(IReadOnlyDictionary<BufferedEntity, Entity> createdEntities)
    {
        _createdEntities = createdEntities;
    }

    /// <summary>
    /// 将占位实体解析为播放时创建的真实实体。
    /// </summary>
    /// <param name="entity">占位实体。</param>
    /// <returns>真实实体。</returns>
    /// <exception cref="InvalidOperationException"><paramref name="entity"/> 不是本次播放创建的占位实体。</exception>
    public Entity Resolve(BufferedEntity entity)
    {
        if (!TryResolve(entity, out var resolved))
        {
            throw new InvalidOperationException($"Buffered entity {entity.Id} was not created by this playback.");
        }

        return resolved;
    }

    /// <summary>
    /// 尝试将占位实体解析为真实实体。
    /// </summary>
    /// <param name="entity">占位实体。</param>
    /// <param name="resolved">找到时返回真实实体。</param>
    /// <returns>如果占位实体可解析则为 <c>true</c>。</returns>
    public bool TryResolve(BufferedEntity entity, out Entity resolved)
    {
        return _createdEntities.TryGetValue(entity, out resolved);
    }
}

/// <summary>
/// 延迟播放 ECS 结构变更的命令缓冲；每次播放尝试后开始新的命令批次。
/// </summary>
public sealed class CommandBuffer
{
    private static long _nextBufferId;

    private readonly List<ICommand> _commands = new();
    private long _bufferId;
    private readonly World _world;
    private int _nextBufferedEntityId;

    /// <summary>
    /// 创建绑定到指定世界的命令缓冲。
    /// </summary>
    /// <param name="world">命令播放的目标世界。</param>
    /// <exception cref="ArgumentNullException"><paramref name="world"/> 为 null。</exception>
    public CommandBuffer(World world)
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));
        _bufferId = Interlocked.Increment(ref _nextBufferId);
    }

    /// <summary>
    /// 延迟创建空实体。
    /// </summary>
    /// <returns>可在当前命令批次中继续使用的占位实体。</returns>
    public BufferedEntity CreateEntity()
    {
        return CreateEntity(Array.Empty<IComponent>());
    }

    /// <summary>
    /// 延迟使用一个组件创建实体。
    /// </summary>
    /// <typeparam name="T1">组件类型。</typeparam>
    /// <param name="c1">组件值。</param>
    /// <returns>可在当前命令批次中继续使用的占位实体。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="c1"/> 为 null。</exception>
    public BufferedEntity CreateEntity<T1>(T1 c1) where T1 : IComponent
    {
        ThrowIfNull(c1, nameof(c1));
        return CreateEntity(new IComponent[] { c1 });
    }

    /// <summary>
    /// 延迟使用两个组件创建实体。
    /// </summary>
    /// <typeparam name="T1">第一个组件类型。</typeparam>
    /// <typeparam name="T2">第二个组件类型。</typeparam>
    /// <param name="c1">第一个组件值。</param>
    /// <param name="c2">第二个组件值。</param>
    /// <returns>可在当前命令批次中继续使用的占位实体。</returns>
    /// <exception cref="ArgumentNullException">任一组件值为 null。</exception>
    public BufferedEntity CreateEntity<T1, T2>(T1 c1, T2 c2)
        where T1 : IComponent
        where T2 : IComponent
    {
        ThrowIfNull(c1, nameof(c1));
        ThrowIfNull(c2, nameof(c2));
        return CreateEntity(new IComponent[] { c1, c2 });
    }

    /// <summary>
    /// 延迟销毁真实实体。
    /// </summary>
    /// <param name="entity">真实实体。</param>
    public void DestroyEntity(Entity entity)
    {
        _commands.Add(new DestroyEntityCommand(new EntityTarget(entity)));
    }

    /// <summary>
    /// 延迟销毁占位实体。
    /// </summary>
    /// <param name="entity">占位实体。</param>
    /// <exception cref="InvalidOperationException"><paramref name="entity"/> 已过期或来自其他命令批次。</exception>
    public void DestroyEntity(BufferedEntity entity)
    {
        ValidateCurrentBatch(entity);
        _commands.Add(new DestroyEntityCommand(new EntityTarget(entity)));
    }

    /// <summary>
    /// 延迟添加或更新真实实体组件。
    /// </summary>
    /// <typeparam name="T">组件类型。</typeparam>
    /// <param name="entity">真实实体。</param>
    /// <param name="component">组件值。</param>
    /// <exception cref="ArgumentNullException"><paramref name="component"/> 为 null。</exception>
    public void Add<T>(Entity entity, T component) where T : IComponent
    {
        ThrowIfNull(component, nameof(component));
        _commands.Add(new AddCommand<T>(new EntityTarget(entity), component));
    }

    /// <summary>
    /// 延迟添加或更新占位实体组件。
    /// </summary>
    /// <typeparam name="T">组件类型。</typeparam>
    /// <param name="entity">占位实体。</param>
    /// <param name="component">组件值。</param>
    /// <exception cref="ArgumentNullException"><paramref name="component"/> 为 null。</exception>
    /// <exception cref="InvalidOperationException"><paramref name="entity"/> 已过期或来自其他命令批次。</exception>
    public void Add<T>(BufferedEntity entity, T component) where T : IComponent
    {
        ValidateCurrentBatch(entity);
        ThrowIfNull(component, nameof(component));
        _commands.Add(new AddCommand<T>(new EntityTarget(entity), component));
    }

    /// <summary>
    /// 延迟更新真实实体已有组件。
    /// </summary>
    /// <typeparam name="T">组件类型。</typeparam>
    /// <param name="entity">真实实体。</param>
    /// <param name="component">组件值。</param>
    /// <exception cref="ArgumentNullException"><paramref name="component"/> 为 null。</exception>
    public void Set<T>(Entity entity, T component) where T : IComponent
    {
        ThrowIfNull(component, nameof(component));
        _commands.Add(new SetCommand<T>(new EntityTarget(entity), component));
    }

    /// <summary>
    /// 延迟更新占位实体已有组件。
    /// </summary>
    /// <typeparam name="T">组件类型。</typeparam>
    /// <param name="entity">占位实体。</param>
    /// <param name="component">组件值。</param>
    /// <exception cref="ArgumentNullException"><paramref name="component"/> 为 null。</exception>
    /// <exception cref="InvalidOperationException"><paramref name="entity"/> 已过期或来自其他命令批次。</exception>
    public void Set<T>(BufferedEntity entity, T component) where T : IComponent
    {
        ValidateCurrentBatch(entity);
        ThrowIfNull(component, nameof(component));
        _commands.Add(new SetCommand<T>(new EntityTarget(entity), component));
    }

    /// <summary>
    /// 延迟移除真实实体组件。
    /// </summary>
    /// <typeparam name="T">组件类型。</typeparam>
    /// <param name="entity">真实实体。</param>
    public void Remove<T>(Entity entity) where T : IComponent
    {
        _commands.Add(new RemoveCommand<T>(new EntityTarget(entity)));
    }

    /// <summary>
    /// 延迟移除占位实体组件。
    /// </summary>
    /// <typeparam name="T">组件类型。</typeparam>
    /// <param name="entity">占位实体。</param>
    /// <exception cref="InvalidOperationException"><paramref name="entity"/> 已过期或来自其他命令批次。</exception>
    public void Remove<T>(BufferedEntity entity) where T : IComponent
    {
        ValidateCurrentBatch(entity);
        _commands.Add(new RemoveCommand<T>(new EntityTarget(entity)));
    }

    /// <summary>
    /// 按记录顺序播放当前批次，并在本次尝试结束后消费全部命令和占位句柄。
    /// </summary>
    /// <returns>成功播放的结果；结果仍可解析刚消费批次的占位句柄。</returns>
    /// <exception cref="Exception">命令异常会直接传播；此前已完成的世界变更不会回滚。</exception>
    /// <remarks>无论成功或失败，本次尝试都会消费当前批次；后续记录属于新的批次，旧句柄不能再用于记录命令。</remarks>
    public CommandBufferResult Playback()
    {
        var commands = _commands.ToArray();
        var context = new PlaybackContext(_world);
        try
        {
            foreach (var command in commands)
            {
                command.Playback(context);
            }

            return new CommandBufferResult(context.CreatedEntities);
        }
        finally
        {
            _commands.Clear();
            _nextBufferedEntityId = 0;
            _bufferId = Interlocked.Increment(ref _nextBufferId);
        }
    }

    private BufferedEntity CreateEntity(IReadOnlyCollection<IComponent> components)
    {
        var bufferedEntity = new BufferedEntity(_bufferId, _nextBufferedEntityId++);
        _commands.Add(new CreateEntityCommand(bufferedEntity, components.ToArray()));
        return bufferedEntity;
    }

    private void ValidateCurrentBatch(BufferedEntity entity)
    {
        if (entity.OwnerId != _bufferId)
        {
            throw new InvalidOperationException(
                $"Buffered entity {entity.Id} belongs to an expired or foreign command batch.");
        }
    }

    private static void ThrowIfNull<T>(T component, string paramName) where T : IComponent
    {
        if (component is null)
        {
            throw new ArgumentNullException(paramName);
        }
    }

    private interface ICommand
    {
        void Playback(PlaybackContext context);
    }

    private readonly struct EntityTarget
    {
        private readonly BufferedEntity _bufferedEntity;
        private readonly Entity _entity;
        private readonly bool _isBuffered;

        public EntityTarget(Entity entity)
        {
            _entity = entity;
            _bufferedEntity = default;
            _isBuffered = false;
        }

        public EntityTarget(BufferedEntity entity)
        {
            _bufferedEntity = entity;
            _entity = default;
            _isBuffered = true;
        }

        public Entity Resolve(PlaybackContext context)
        {
            return _isBuffered ? context.Resolve(_bufferedEntity) : _entity;
        }
    }

    private sealed class PlaybackContext
    {
        private readonly Dictionary<BufferedEntity, Entity> _createdEntities = new();

        public PlaybackContext(World world)
        {
            World = world;
        }

        public World World { get; }

        public IReadOnlyDictionary<BufferedEntity, Entity> CreatedEntities => _createdEntities;

        public void AddCreatedEntity(BufferedEntity bufferedEntity, Entity entity)
        {
            _createdEntities.Add(bufferedEntity, entity);
        }

        public Entity Resolve(BufferedEntity bufferedEntity)
        {
            if (!_createdEntities.TryGetValue(bufferedEntity, out var entity))
            {
                throw new InvalidOperationException($"Buffered entity {bufferedEntity.Id} has not been created yet.");
            }

            return entity;
        }
    }

    private sealed class CreateEntityCommand : ICommand
    {
        private readonly BufferedEntity _bufferedEntity;
        private readonly IComponent[] _components;

        public CreateEntityCommand(BufferedEntity bufferedEntity, IComponent[] components)
        {
            _bufferedEntity = bufferedEntity;
            _components = components;
        }

        public void Playback(PlaybackContext context)
        {
            var entity = context.World.CreateEntityWithBufferedComponents(_components);
            context.AddCreatedEntity(_bufferedEntity, entity);
        }
    }

    private sealed class DestroyEntityCommand : ICommand
    {
        private readonly EntityTarget _target;

        public DestroyEntityCommand(EntityTarget target)
        {
            _target = target;
        }

        public void Playback(PlaybackContext context)
        {
            context.World.DestroyEntity(_target.Resolve(context));
        }
    }

    private sealed class AddCommand<T> : ICommand where T : IComponent
    {
        private readonly T _component;
        private readonly EntityTarget _target;

        public AddCommand(EntityTarget target, T component)
        {
            _target = target;
            _component = component;
        }

        public void Playback(PlaybackContext context)
        {
            context.World.Add(_target.Resolve(context), _component);
        }
    }

    private sealed class SetCommand<T> : ICommand where T : IComponent
    {
        private readonly T _component;
        private readonly EntityTarget _target;

        public SetCommand(EntityTarget target, T component)
        {
            _target = target;
            _component = component;
        }

        public void Playback(PlaybackContext context)
        {
            context.World.Set(_target.Resolve(context), _component);
        }
    }

    private sealed class RemoveCommand<T> : ICommand where T : IComponent
    {
        private readonly EntityTarget _target;

        public RemoveCommand(EntityTarget target)
        {
            _target = target;
        }

        public void Playback(PlaybackContext context)
        {
            context.World.Remove<T>(_target.Resolve(context));
        }
    }
}
