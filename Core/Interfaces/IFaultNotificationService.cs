using System;
using System.Threading;
using System.Threading.Tasks;
using SANJET.Core.Models;

namespace SANJET.Core.Interfaces
{
    public interface IFaultNotificationService
    {
        Task NotifyStatusChangedAsync(Device device, string oldStatus, string newStatus, DateTime occurredAt, CancellationToken cancellationToken = default);
    }
}
