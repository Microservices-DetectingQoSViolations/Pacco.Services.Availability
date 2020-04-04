using System;
using Convey.Types;
using Microsoft.Extensions.Caching.Distributed;
using OpenTracing;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace Pacco.Services.Availability.Infrastructure.QoS
{
    public class QoSTimeViolationChecker : IQoSTimeViolationChecker
    {
        private readonly Stopwatch _stopwatch = new Stopwatch();

        private readonly IDistributedCache _cache;
        private readonly IQoSCacheFormatter _qoSCacheFormatter;
        private readonly IQoSViolateRaiser _qoSViolateRaiser;

        private ISpan _span;

        private readonly int _windowComparerSize;
        private readonly string _appServiceName;
        private string _cacheCommandName;
        private string _commandName;

        public QoSTimeViolationChecker(IDistributedCache cache, IQoSCacheFormatter qoSCacheFormatter, IQoSViolateRaiser qoSViolateRaiser, 
            QoSTrackingOptions options, AppOptions appOptions)
        {
            _cache = cache;
            _qoSCacheFormatter = qoSCacheFormatter;
            _qoSViolateRaiser = qoSViolateRaiser;
            _windowComparerSize = options.WindowComparerSize;
            _appServiceName = appOptions.Service;
        }

        public IQoSTimeViolationChecker Build(ISpan span, string commandName)
        {
            _span = span;
            _commandName = commandName;
            _cacheCommandName = $"{_appServiceName.ToLower()}_{_commandName}";

            return this;
        }

        public void Run()
        {
            _stopwatch.Start();
        }

        public async Task Analyze()
        {
            _stopwatch.Stop();
            var handlingTime = _stopwatch.ElapsedMilliseconds;
            try
            {
                var requiredHandlingTime = await GetRequiredTimeFromCache();

                if (RaiseTimeQoSViolationIfNeeded(handlingTime, requiredHandlingTime))
                {
                    // log Message

                    return;
                }

                await SetValuesInCache(handlingTime, requiredHandlingTime);
            }
            catch (Exception e)
            {
                // log msg
            }
        }

        private bool RaiseTimeQoSViolationIfNeeded(long handlingTime, long requiredHandlingTime)
        {
            var shouldRaise = _qoSViolateRaiser.ShouldRaiseTimeViolation(handlingTime, requiredHandlingTime);

            if (shouldRaise)
            {
                _qoSViolateRaiser.Raise(_span, ViolateType.HandlerTimeExceeded);
            }
            return shouldRaise;
        }

        private async Task<long> GetRequiredTimeFromCache()
        {
            var cacheValue = await _cache.GetAsync(_cacheCommandName);

            return _qoSCacheFormatter.DeserializeNumber(cacheValue);

        }

        private async Task<int> GetActualIdxFromCache()
        {
            var cacheIdx = await _cache.GetAsync(GetIndexName());

            return _qoSCacheFormatter.DeserializeInt32(cacheIdx);
        }

        private async Task<long[]> GetActualArrayFromCache()
        {
            var cacheIdx = await _cache.GetAsync(GetArrayName());

            return _qoSCacheFormatter.DeserializeArrayNumber(cacheIdx);
        }

        private async Task SetValuesInCache(long handlingTime, long requiredHandlingTime)
        {
            var actualIdx = await GetActualIdxFromCache();
            var cachedArray = await GetActualArrayFromCache();

            var nextIndex = (actualIdx + 1) % _windowComparerSize;
            cachedArray[nextIndex] = handlingTime;

            await _cache.SetAsync(GetArrayName(), _qoSCacheFormatter.SerializeArrayNumber(cachedArray));
            await _cache.SetAsync(GetIndexName(), _qoSCacheFormatter.SerializeInt32(nextIndex));

            if (nextIndex == 0)
            {
                var meanHandlerTime = cachedArray.Average();
                if (meanHandlerTime < requiredHandlingTime)
                {
                    await _cache.SetAsync(_cacheCommandName,
                        _qoSCacheFormatter.SerializeNumber((long) meanHandlerTime));
                }
            }
        }

        private string GetArrayName() 
            => _cacheCommandName + "_arr";
        private string GetIndexName() 
            => _cacheCommandName + "_idx";
    }
}
