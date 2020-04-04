using System;

namespace Pacco.Services.Availability.Infrastructure.QoS
{
    public class QoSTrackingSampler : IQoSTrackingSampler
    {
        private readonly double _samplingRate;
        private readonly Random _random = new Random();

        public QoSTrackingSampler(QoSTrackingOptions options)
        {
            _samplingRate = options.SamplingRate;
        }

        public bool DoWork()
        {
            return _random.NextDouble() <= _samplingRate;
        }
    }
}
