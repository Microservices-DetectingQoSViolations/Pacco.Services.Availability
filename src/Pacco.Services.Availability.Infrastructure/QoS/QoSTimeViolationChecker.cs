using Convey.Types;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace Pacco.Services.Availability.Infrastructure.QoS
{
    public class QoSTimeViolationChecker<TMessage> : IQoSTimeViolationChecker<TMessage>
    {
        private readonly Stopwatch _stopwatch = new Stopwatch();

        private readonly IDistributedCache _distributedCache;
        private readonly IMemoryCache _memoryCache;
        private readonly IQoSCacheFormatter _qoSCacheFormatter;
        private readonly IQoSViolateRaiser _qoSViolateRaiser;
        private readonly ILogger<IQoSTimeViolationChecker<TMessage>> _logger;

        private readonly int _windowComparerSize;
        private string _cacheCommandName;
        private readonly double _timeViolationCoefficient;

        private const long LongElapsedMilliseconds = 60000;

        public QoSTimeViolationChecker(IQoSCacheFormatter qoSCacheFormatter, IQoSViolateRaiser qoSViolateRaiser, 
            QoSTrackingOptions options, AppOptions appOptions, IDistributedCache distributedCache, IMemoryCache memoryCache, 
            ILogger<IQoSTimeViolationChecker<TMessage>> logger)
        {
            _qoSCacheFormatter = qoSCacheFormatter;
            _qoSViolateRaiser = qoSViolateRaiser;
            _distributedCache = distributedCache;
            _memoryCache = memoryCache;
            _logger = logger;
            _windowComparerSize = options.WindowComparerSize;

            var appServiceName = appOptions.Service;
            var commandName = this.GetMessageName();
            var timeViolationOptions = options.QoSTimeViolationOptions;

            void AssignCacheMessageName()
            {
                _cacheCommandName = $"{appServiceName.ToLower()}_{commandName}";
            }

            double ChooseTimeCoefficient(char messageShort)
                => messageShort switch
                {
                    'c' => timeViolationOptions.CommandExceedingCoefficient,
                    'q' => timeViolationOptions.QueryExceedingCoefficient,
                    'e' => timeViolationOptions.EventExceedingCoefficient,
                    _ => 0.0
                };

            AssignCacheMessageName();
            _timeViolationCoefficient = ChooseTimeCoefficient(commandName.First());
        }

        public void Run()
        {
            _stopwatch.Start();
        }

        public async Task Analyze()
        {
            _stopwatch.Stop();

            var instanceWarmingUp = !_memoryCache.GetOrCreate(CacheEntries.InstanceWarmedUp, entry => false);

            if (instanceWarmingUp)
            {
                var warmUpMessagesNumber = _memoryCache.GetOrCreate(CacheEntries.WarmUpMessagesNumber, entry => 0);
                warmUpMessagesNumber += 1;

                if (warmUpMessagesNumber >= _windowComparerSize)
                {
                    _memoryCache.Set(CacheEntries.InstanceWarmedUp, true);
                }
                else
                {
                    _memoryCache.Set(CacheEntries.WarmUpMessagesNumber, warmUpMessagesNumber);
                }

                return;
            }


            var handlingTime = _stopwatch.ElapsedMilliseconds;
            try
            {
                var requiredHandlingTime = await GetRequiredTimeFromCache();

                if (RaiseTimeQoSViolationIfNeeded(handlingTime, requiredHandlingTime))
                {
                    return;
                }

                await SetValuesInCache(handlingTime, requiredHandlingTime);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Reading values from cache not working.");
            }
        }

        private bool RaiseTimeQoSViolationIfNeeded(long handlingTime, long requiredHandlingTime)
        {
            var shouldRaise = ShouldRaiseTimeViolation(handlingTime, requiredHandlingTime);

            if (shouldRaise)
            {
                _qoSViolateRaiser.Raise(ViolateType.HandlerTimeExceeded);
            }
            return shouldRaise;
        }

        private bool ShouldRaiseTimeViolation(long handlingTime, long requiredHandlingTime)
            => _timeViolationCoefficient * handlingTime > requiredHandlingTime;

        private async Task<long> GetRequiredTimeFromCache()
        {
            var cacheValue = await _distributedCache.GetAsync(_cacheCommandName);

            return _qoSCacheFormatter.DeserializeNumber(cacheValue);

        }

        private Task<int> GetActualIdxFromCache()
            => _memoryCache.GetOrCreateAsync(GetIndexName(), cacheEntry => Task.FromResult(0));

        private Task<long[]> GetActualArrayFromCache()
            => _memoryCache.GetOrCreateAsync(GetArrayName(),
                cacheEntry => Task.FromResult(Enumerable.Repeat(LongElapsedMilliseconds, _windowComparerSize).ToArray()));

        private async Task SetValuesInCache(long handlingTime, long requiredHandlingTime)
        {
            var actualIdx = await GetActualIdxFromCache();
            var cachedArray = await GetActualArrayFromCache();

            var nextIndex = (actualIdx + 1) % _windowComparerSize;
            cachedArray[nextIndex] = handlingTime;
            
            _memoryCache.Set(GetArrayName(), cachedArray);
            _memoryCache.Set(GetIndexName(), nextIndex);

            if (nextIndex == 0)
            {
                var meanHandlerTime = cachedArray.Average();
                if (meanHandlerTime < requiredHandlingTime)
                {
                    await _distributedCache.SetAsync(_cacheCommandName,
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
