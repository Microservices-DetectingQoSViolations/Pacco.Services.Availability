using System.Threading.Tasks;
using OpenTracing;

namespace Pacco.Services.Availability.Infrastructure.QoS
{
    public interface IQoSTimeViolationChecker<TMessage>
    {
        void Run();
        Task Analyze();
    }
}
