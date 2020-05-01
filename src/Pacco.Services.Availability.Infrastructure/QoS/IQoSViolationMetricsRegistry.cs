namespace Pacco.Services.Availability.Infrastructure.QoS
{
    public interface IQoSViolationMetricsRegistry
    {
        void IncrementQoSViolation(ViolateType violateType);
    }
}
