using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SANJET.Core.Interfaces;

namespace SANJET.Core.Services
{
    public class CompositeLineNotificationService : ILineNotificationService
    {
        private readonly IReadOnlyList<ILineNotificationChannel> _channels;
        private readonly ILogger<CompositeLineNotificationService> _logger;

        public CompositeLineNotificationService(
            IEnumerable<ILineNotificationChannel> channels,
            ILogger<CompositeLineNotificationService> logger)
        {
            _channels = channels?.ToArray() ?? throw new ArgumentNullException(nameof(channels));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public bool IsConfigured => _channels.Any(channel => channel.IsConfigured);

        public async Task SendTextMessageAsync(string message, CancellationToken cancellationToken = default)
        {
            var enabledChannels = _channels.Where(channel => channel.IsEnabled).ToArray();
            if (enabledChannels.Length == 0)
            {
                _logger.LogWarning("未啟用任何 LINE 通知通道，略過推播。訊息: {Message}", message);
                return;
            }

            foreach (var channel in enabledChannels)
            {
                if (!channel.IsConfigured)
                {
                    _logger.LogWarning("LINE 通知通道 {ChannelName} 已啟用但設定不完整，略過本通道。", channel.ChannelName);
                    continue;
                }

                try
                {
                    await channel.SendTextMessageAsync(message, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "LINE 通知通道 {ChannelName} 發送失敗。", channel.ChannelName);
                }
            }
        }
    }
}
