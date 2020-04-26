namespace Pacco.Services.Availability.Infrastructure.QoS
{
    public class QoSTimeViolationOptions
    {
        public double CommandExceedingCoefficient { get; set; }
        public double QueryExceedingCoefficient { get; set; }
        public double EventExceedingCoefficient { get; set; }
    }
}
