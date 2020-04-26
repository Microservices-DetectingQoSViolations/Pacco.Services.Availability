using OpenTracing;

namespace Pacco.Services.Availability.Infrastructure.QoS
{
    public interface IQoSViolateRaiser
    {
        void Raise(ViolateType violateType);
    }
}
