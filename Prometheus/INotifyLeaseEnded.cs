namespace Prometheus;

internal interface INotifyLeaseEnded
{
    void OnLeaseEnded(object child, ChildLifetimeInfo lifetime);
}
