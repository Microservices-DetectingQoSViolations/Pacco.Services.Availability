using Convey.CQRS.Commands;
using OpenTracing;
using OpenTracing.Tag;
using Pacco.Services.Availability.Application.Exceptions;
using System;
using System.Threading.Tasks;

namespace Pacco.Services.Availability.Infrastructure.QoS
{
    public class QoSTrackerCommandHandlerDecorator<TCommand> : ICommandHandler<TCommand> 
        where TCommand : class, ICommand
    {
        private readonly ICommandHandler<TCommand> _handler;
        private readonly ITracer _tracer;
        private readonly IQoSTrackingSampler _trackingSampler;
        private readonly IQoSTimeViolationChecker _qoSViolateChecker;
        private readonly IQoSViolateRaiser _qoSViolateRaiser;

        public QoSTrackerCommandHandlerDecorator(ICommandHandler<TCommand> handler, ITracer tracer,
            IQoSTimeViolationChecker qoSViolateChecker, IQoSTrackingSampler trackingSampler,
            IQoSViolateRaiser qoSViolateRaiser)
        {
            _handler = handler;
            _tracer = tracer;
            _trackingSampler = trackingSampler;
            _qoSViolateRaiser = qoSViolateRaiser;
            _qoSViolateChecker = qoSViolateChecker;
        }

        public async Task HandleAsync(TCommand command)
        {
            if (!_trackingSampler.DoWork())
            {
                await _handler.HandleAsync(command);
                return;
            }

            var commandName = command.GetCommandName();
            using var scope = BuildScope(commandName);
            var span = scope.Span;

            _qoSViolateChecker
                .Build(span, commandName)
                .Run();

            try
            {
                await _handler.HandleAsync(command);
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

        private IScope BuildScope(string commandName)
        {
            var scope = _tracer
                .BuildSpan($"handling {commandName}")
                .WithTag("message-type", commandName);

            if (_tracer.ActiveSpan is { })
            {
                scope.AddReference(References.ChildOf, _tracer.ActiveSpan.Context);
            }

            return scope.StartActive(true);
        }
    }
}
