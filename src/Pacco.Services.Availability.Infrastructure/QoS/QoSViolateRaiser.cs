using OpenTracing;
using OpenTracing.Tag;

namespace Pacco.Services.Availability.Infrastructure.QoS
{
    public class QoSViolateRaiser : IQoSViolateRaiser
    {
        public static readonly StringTag Violation = new StringTag("violation");

        //logger

        public bool ShouldRaiseTimeViolation(long handlingTime, long requiredHandlingTime)
            => 0.3 * handlingTime > requiredHandlingTime;

        public void Raise(ISpan span, ViolateType violateType)
        {
            span.Log($"QoS Violate raised: {violateType}");
            span.SetTag(Violation, violateType.ToString());
        }
    }
}
