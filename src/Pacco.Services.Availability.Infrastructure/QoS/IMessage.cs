using Convey.CQRS.Commands;
using Convey.CQRS.Events;
using Convey.CQRS.Queries;

namespace Pacco.Services.Availability.Infrastructure.QoS
{
    public interface IMessage : ICommand, IQuery, IEvent
    {
        
    }
}