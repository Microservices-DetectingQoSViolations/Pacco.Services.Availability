using Convey.CQRS.Commands;
using OpenTracing;
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
        private readonly IQoSTimeViolationChecker<TCommand> _qoSViolateChecker;
        private readonly IQoSViolateRaiser _qoSViolateRaiser;

        private readonly bool _withTracing;

        public QoSTrackerCommandHandlerDecorator(ICommandHandler<TCommand> handler, ITracer tracer,
            IQoSTimeViolationChecker<TCommand> qoSViolateChecker, IQoSTrackingSampler trackingSampler,
            IQoSViolateRaiser qoSViolateRaiser, QoSTrackingOptions trackingOptions)
        {
            _handler = handler;
            _tracer = tracer;
            _trackingSampler = trackingSampler;
            _qoSViolateRaiser = qoSViolateRaiser;
            _qoSViolateChecker = qoSViolateChecker;
            _withTracing = trackingOptions.EnabledTracing && tracer is { };
        }

        public async Task HandleAsync(TCommand command)
        {
            if (!_trackingSampler.DoWork())
            {
                await _handler.HandleAsync(command);
                return;
            }

            using var scope = _withTracing ? BuildScope(command.GetCommandName()) : null;

            _qoSViolateChecker.Run();

            try
            {
                await _handler.HandleAsync(command);
            }
            catch (Exception exception)
            {
                switch (exception)
                {
                    case AppException _:
                        _qoSViolateRaiser.Raise(ViolateType.AmongServicesInconsistency);
                        break;
                }
                throw;
            }

            await _qoSViolateChecker.Analyze();
        }

        private IScope BuildScope(string commandName)
        {
            var scope = _tracer
                .BuildSpan($"handling {commandName}")
                .WithTag("message-type", "command");

            if (_tracer.ActiveSpan is { })
            {
                scope.AddReference(References.ChildOf, _tracer.ActiveSpan.Context);
            }

            return scope.StartActive(true);
        }
    }
}
