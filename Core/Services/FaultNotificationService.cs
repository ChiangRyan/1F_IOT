using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SANJET.Core.Configuration;
using SANJET.Core.Interfaces;
using SANJET.Core.Models;

namespace SANJET.Core.Services
{
    public class FaultNotificationService : IFaultNotificationService
    {
        private readonly ILineNotificationService _lineNotificationService;
        private readonly FaultNotificationOptions _options;
        private readonly ILogger<FaultNotificationService> _logger;
        private readonly ConcurrentDictionary<int, DateTime> _lastFaultNotificationTimes = new();

        public FaultNotificationService(
            ILineNotificationService lineNotificationService,
            FaultNotificationOptions options,
            ILogger<FaultNotificationService> logger)
        {
            _lineNotificationService = lineNotificationService;
            _options = options;
            _logger = logger;
        }

        public async Task NotifyStatusChangedAsync(Device device, string oldStatus, string newStatus, DateTime occurredAt, CancellationToken cancellationToken = default)
        {
            if (!_options.Enabled || !_lineNotificationService.IsConfigured)
            {
                return;
            }

            bool wasFault = IsFaultStatus(oldStatus);
            bool isFault = IsFaultStatus(newStatus);

            if (!wasFault && isFault)
            {
                if (IsInCooldown(device.Id, occurredAt))
                {
                    _logger.LogInformation("設備 {DeviceName} 仍在 LINE 故障通知冷卻時間內，略過本次推播。", device.Name);
                    return;
                }

                await _lineNotificationService.SendTextMessageAsync(BuildFaultMessage(device, oldStatus, newStatus, occurredAt), cancellationToken);
                _lastFaultNotificationTimes[device.Id] = occurredAt;
                return;
            }

            if (wasFault && !isFault)
            {
                _lastFaultNotificationTimes.TryRemove(device.Id, out _);

                if (_options.NotifyRecovery)
                {
                    await _lineNotificationService.SendTextMessageAsync(BuildRecoveryMessage(device, oldStatus, newStatus, occurredAt), cancellationToken);
                }
            }
        }

        private bool IsInCooldown(int deviceId, DateTime occurredAt)
        {
            var cooldown = TimeSpan.FromMinutes(Math.Max(0, _options.CooldownMinutes));
            return cooldown > TimeSpan.Zero &&
                   _lastFaultNotificationTimes.TryGetValue(deviceId, out var lastNotificationAt) &&
                   occurredAt - lastNotificationAt < cooldown;
        }

        private static bool IsFaultStatus(string? status)
        {
            if (string.IsNullOrWhiteSpace(status))
            {
                return false;
            }

            return status.Contains("故障", StringComparison.OrdinalIgnoreCase) ||
                   status.Contains("通訊失敗", StringComparison.OrdinalIgnoreCase) ||
                   status.Contains("錯誤", StringComparison.OrdinalIgnoreCase) ||
                   status.Contains("失敗", StringComparison.OrdinalIgnoreCase);
        }

        private static string BuildFaultMessage(Device device, string oldStatus, string newStatus, DateTime occurredAt)
        {
            return $"⚠️ 設備故障通知\n\n" +
                   $"設備：{device.Name}\n" +
                   $"區域：{device.Area}\n" +
                   $"ESP32：{device.ControllingEsp32MqttId}\n" +
                   $"Slave ID：{device.SlaveId}\n" +
                   $"狀態：{oldStatus} → {newStatus}\n" +
                   $"時間：{occurredAt:yyyy-MM-dd HH:mm:ss}";
        }

        private static string BuildRecoveryMessage(Device device, string oldStatus, string newStatus, DateTime occurredAt)
        {
            return $"✅ 設備恢復通知\n\n" +
                   $"設備：{device.Name}\n" +
                   $"區域：{device.Area}\n" +
                   $"ESP32：{device.ControllingEsp32MqttId}\n" +
                   $"Slave ID：{device.SlaveId}\n" +
                   $"狀態：{oldStatus} → {newStatus}\n" +
                   $"時間：{occurredAt:yyyy-MM-dd HH:mm:ss}";
        }
    }
}
