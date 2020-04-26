using Convey.CQRS.Queries;
using OpenTracing;
using OpenTracing.Tag;
using System;
using System.Threading.Tasks;

namespace Pacco.Services.Availability.Infrastructure.QoS
{
    public class QoSTrackerQueryHandlerDecorator<TQuery, TResult> : IQueryHandler<TQuery, TResult>
        where TQuery : class, IQuery<TResult>
    {
        private readonly IQueryHandler<TQuery, TResult> _handler;
        private readonly ITracer _tracer;
        private readonly IQoSTrackingSampler _trackingSampler;
        private readonly IQoSTimeViolationChecker<TQuery> _qoSViolateChecker;

        private readonly bool _withTracing;

        public QoSTrackerQueryHandlerDecorator(IQueryHandler<TQuery, TResult> handler,
            ITracer tracer, IQoSTrackingSampler trackingSampler, IQoSTimeViolationChecker<TQuery> qoSViolateChecker,
            QoSTrackingOptions trackingOptions)
        {
            _handler = handler;
            _tracer = tracer;
            _trackingSampler = trackingSampler;
            _qoSViolateChecker = qoSViolateChecker;
            _withTracing = trackingOptions.EnabledTracing && tracer is { };
        }

        public async Task<TResult> HandleAsync(TQuery query)
        {
            if (!_trackingSampler.DoWork())
            {
                return await _handler.HandleAsync(query);
            }

            using var scope = _withTracing ? BuildScope(query.GetQueryName()) : null;

            _qoSViolateChecker.Run();

            var queryResult = await _handler.HandleAsync(query);

            await _qoSViolateChecker.Analyze();

            return queryResult;
        }

        private IScope BuildScope(string queryName)
        {
            var scope = _tracer
                .BuildSpan($"handling {queryName}")
                .WithTag("message-type", "query");

            if (_tracer.ActiveSpan is { })
            {
                scope.AddReference(References.ChildOf, _tracer.ActiveSpan.Context);
            }

            return scope.StartActive(true);
        }
    }
}
