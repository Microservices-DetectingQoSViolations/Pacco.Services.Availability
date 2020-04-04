using OpenTracing;

namespace Pacco.Services.Availability.Infrastructure.QoS
{
    public interface IQoSViolateRaiser
    {
        bool ShouldRaiseTimeViolation(long handlingTime, long requiredHandlingTime);
        void Raise(ISpan span, ViolateType violateType);
    }
}
