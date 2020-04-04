using System.Threading.Tasks;
using OpenTracing;

namespace Pacco.Services.Availability.Infrastructure.QoS
{
    public interface IQoSTimeViolationChecker
    {
        IQoSTimeViolationChecker Build(ISpan span, string commandName);
        void Run();
        Task Analyze();
    }
}
