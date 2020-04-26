using Microsoft.Extensions.Logging;

namespace Pacco.Services.Availability.Infrastructure.QoS
{
    public class QoSViolateSimpleRaiser : IQoSViolateRaiser
    {
        private readonly ILogger<IQoSViolateRaiser> _logger;

        public QoSViolateSimpleRaiser(ILogger<IQoSViolateRaiser> logger)
        {
            _logger = logger;
        }

        public void Raise(ViolateType violateType)
        {
            _logger.LogWarning($"QoSViolation {violateType} raised.");
        }
    }
}
