namespace SimpleFramework.Net;

public interface INetEventDispatcher
{
    void Post(Action action);
}
