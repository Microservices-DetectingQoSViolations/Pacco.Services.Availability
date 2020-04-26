using Microsoft.Extensions.Logging;
using OpenTracing;
using OpenTracing.Tag;

namespace Pacco.Services.Availability.Infrastructure.QoS
{
    public class QoSViolateTracerRaiser : IQoSViolateRaiser
    {
        public static readonly StringTag Violation = new StringTag("violation");

        private readonly ITracer _tracer;
        private readonly ILogger<IQoSViolateRaiser> _logger;

        public QoSViolateTracerRaiser(ITracer tracer, ILogger<IQoSViolateRaiser> logger)
        {
            _tracer = tracer;
            _logger = logger;
        }

        public void Raise(ViolateType violateType)
        {
            RaiseInLogger(violateType);
            RaiseInTracer(violateType);
        }

        private void RaiseInLogger(ViolateType violateType)
        {
            _logger.LogWarning($"QoSViolation {violateType} raised.");
        }

        private void RaiseInTracer(ViolateType violateType)
        {
            var span = _tracer.ActiveSpan;
            if (span is null)
            {
                _logger.LogDebug("There is no active span in tracer.");
                return;
            }

            span.Log($"QoS Violate raised: {violateType}");
            span.SetTag(Violation, violateType.ToString());
        }
    }
}
