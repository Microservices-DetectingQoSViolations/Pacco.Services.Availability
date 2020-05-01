using App.Metrics;
using App.Metrics.Counter;

namespace Pacco.Services.Availability.Infrastructure.QoS
{
    public class QoSViolationMetricsRegistry : IQoSViolationMetricsRegistry
    {
        private readonly IMetricsRoot _metricsRoot;

        private static readonly CounterOptions CounterViolationOptions = new CounterOptions
        {
            Name = "QoS Violation",
            MeasurementUnit = Unit.Custom("QoSViolation")
        };
        
        public QoSViolationMetricsRegistry(IMetricsRoot metricsRoot)
        {
            _metricsRoot = metricsRoot;
        }

        public void IncrementQoSViolation(ViolateType violateType)
        {
            _metricsRoot.Measure.Counter.Increment(CounterViolationOptions, violateType.ToString());
        }
    }
}
