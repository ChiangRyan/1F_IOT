using System.Threading;
using System.Threading.Tasks;

namespace SANJET.Core.Interfaces
{
    public interface ILineNotificationService
    {
        bool IsConfigured { get; }
        Task SendTextMessageAsync(string message, CancellationToken cancellationToken = default);
    }
}
