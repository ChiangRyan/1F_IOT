using System.Threading;
using System.Threading.Tasks;

namespace SANJET.Core.Interfaces
{
    public interface ILineNotificationChannel
    {
        string ChannelName { get; }
        bool IsEnabled { get; }
        bool IsConfigured { get; }
        Task SendTextMessageAsync(string message, CancellationToken cancellationToken = default);
    }
}
