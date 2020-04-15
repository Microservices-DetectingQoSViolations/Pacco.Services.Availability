using Convey.CQRS.Events;
using OpenTracing;
using OpenTracing.Tag;
using Pacco.Services.Availability.Application.Exceptions;
using System;
using System.Threading.Tasks;

namespace Pacco.Services.Availability.Infrastructure.QoS
{
    public class QoSTrackerEventHandlerDecorator<TEvent> : IEventHandler<TEvent>
        where TEvent : class, IEvent
    {
        private readonly IEventHandler<TEvent> _handler;
        private readonly ITracer _tracer;
        private readonly IQoSTrackingSampler _trackingSampler;
        private readonly IQoSTimeViolationChecker _qoSViolateChecker;
        private readonly IQoSViolateRaiser _qoSViolateRaiser;

        public QoSTrackerEventHandlerDecorator(IEventHandler<TEvent> handler, ITracer tracer,
            IQoSTrackingSampler trackingSampler, IQoSTimeViolationChecker qoSViolateChecker, IQoSViolateRaiser qoSViolateRaiser)
        {
            _handler = handler;
            _tracer = tracer;
            _trackingSampler = trackingSampler;
            _qoSViolateChecker = qoSViolateChecker;
            _qoSViolateRaiser = qoSViolateRaiser;
        }

        public async Task HandleAsync(TEvent @event)
        {
            if (!_trackingSampler.DoWork())
            {
                await _handler.HandleAsync(@event);
                return;
            }

            var eventName = @event.GetEventName();
            using var scope = BuildScope(eventName);
            var span = scope.Span;

            _qoSViolateChecker
                .Build(span, eventName)
                .Run();

            try
            {
                await _handler.HandleAsync(@event);
            }
            catch (Exception exception)
            {
                switch (exception)
                {
                    case AppException _:
                        _qoSViolateRaiser.Raise(span, ViolateType.AmongServicesInconsistency);
                        break;
                }
                span.Log(exception.Message);
                span.SetTag(Tags.Error, true);
                throw;
            }

            await _qoSViolateChecker.Analyze();
        }

        private IScope BuildScope(string eventName)
        {
            var scope = _tracer
                .BuildSpan($"handling {eventName}")
                .WithTag("message-type", "event");

            if (_tracer.ActiveSpan is { })
            {
                scope.AddReference(References.ChildOf, _tracer.ActiveSpan.Context);
            }

            return scope.StartActive(true);
        }
    }
}
