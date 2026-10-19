namespace Wanetra.Application.Probes;

// Lets code outside the probe loop (the scheduler) ask whether the connection is currently down.
public sealed class ConnectivityState
{
    private volatile bool outageOpen;

    public bool OutageOpen => outageOpen;

    public void SetOutageOpen(bool open) => outageOpen = open;
}
