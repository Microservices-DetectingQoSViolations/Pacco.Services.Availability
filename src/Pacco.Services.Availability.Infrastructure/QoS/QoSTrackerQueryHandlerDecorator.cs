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
        private readonly IQoSTimeViolationChecker _qoSViolateChecker;

        public QoSTrackerQueryHandlerDecorator(IQueryHandler<TQuery, TResult> handler,
            ITracer tracer, IQoSTrackingSampler trackingSampler, IQoSTimeViolationChecker qoSViolateChecker)
        {
            _handler = handler;
            _tracer = tracer;
            _trackingSampler = trackingSampler;
            _qoSViolateChecker = qoSViolateChecker;
        }

        public async Task<TResult> HandleAsync(TQuery query)
        {
            TResult queryResult;

            if (!_trackingSampler.DoWork())
            {
                return await _handler.HandleAsync(query);
            }

            var queryName = query.GetQueryName();
            using var scope = BuildScope(queryName);
            var span = scope.Span;

            _qoSViolateChecker
                .Build(span, queryName)
                .Run();

            try
            {
                queryResult = await _handler.HandleAsync(query);
            }
            catch (Exception exception)
            {
                span.Log(exception.Message);
                span.SetTag(Tags.Error, true);
                throw;
            }

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
