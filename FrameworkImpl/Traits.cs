namespace SimpleFramework.FrameworkImpl;

public interface IDomainConfigurable
{
    void SetDomain(IDomain domain);
}

public interface IDomainAccessible
{
    IDomain Domain { get; }
}

public interface IModelAccessible : IDomainAccessible
{
}

public interface ISystemAccessible : IDomainAccessible
{
}

public interface IUtilityAccessible : IDomainAccessible
{
}

public interface IEventRegistrable : IDomainAccessible
{
}

public interface IEventTransmittable : IDomainAccessible
{
}

public interface ICommandTransmittable : IDomainAccessible
{
}

public interface IQueryTransmittable: IDomainAccessible
{
}

public interface IConstructable
{
    void Initialize();
    
    void UnInitialize();
}
