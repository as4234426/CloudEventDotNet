namespace DCA.DotNet.Extensions.CloudEvents;

public interface ICloudEventSubscriber
{
    // void Subscribe(CancellationToken token);
    Task StartAsync();
    Task StopAsync();
}
