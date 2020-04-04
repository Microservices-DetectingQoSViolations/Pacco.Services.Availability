namespace Pacco.Services.Availability.Infrastructure.QoS
{
    public interface IQoSTrackingSampler
    {
        bool DoWork();
    }
}