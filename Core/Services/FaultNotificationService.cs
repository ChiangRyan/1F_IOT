using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
        private readonly ConcurrentQueue<PendingFaultNotification> _pendingFaultNotifications = new();
        private readonly ConcurrentQueue<PendingRecoveryNotification> _pendingRecoveryNotifications = new();
        private readonly object _faultBatchLock = new();
        private readonly object _recoveryBatchLock = new();
        private int _pendingFaultNotificationCount;
        private int _pendingRecoveryNotificationCount;
        private Task? _scheduledFaultFlushTask;
        private Task? _scheduledRecoveryFlushTask;

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

                _lastFaultNotificationTimes[device.Id] = occurredAt;

                var batchWindow = TimeSpan.FromSeconds(Math.Max(0, _options.BatchWindowSeconds));
                if (batchWindow == TimeSpan.Zero)
                {
                    await _lineNotificationService.SendTextMessageAsync(BuildFaultMessage(device, oldStatus, newStatus, occurredAt), cancellationToken);
                    return;
                }

                EnqueueFaultNotification(device, oldStatus, newStatus, occurredAt, batchWindow);
                return;
            }

            if (wasFault && !isFault)
            {
                _lastFaultNotificationTimes.TryRemove(device.Id, out _);

                if (_options.NotifyRecovery)
                {
                    var batchWindow = TimeSpan.FromSeconds(Math.Max(0, _options.BatchWindowSeconds));
                    if (batchWindow == TimeSpan.Zero)
                    {
                        await _lineNotificationService.SendTextMessageAsync(BuildRecoveryMessage(device, oldStatus, newStatus, occurredAt), cancellationToken);
                        return;
                    }

                    EnqueueRecoveryNotification(device, oldStatus, newStatus, occurredAt, batchWindow);
                }
            }
        }

        private void EnqueueFaultNotification(Device device, string oldStatus, string newStatus, DateTime occurredAt, TimeSpan batchWindow)
        {
            var pendingFaultNotification = new PendingFaultNotification(
                device.Name,
                device.Area,
                device.ControllingEsp32MqttId,
                device.SlaveId,
                oldStatus,
                newStatus,
                occurredAt);

            _pendingFaultNotifications.Enqueue(pendingFaultNotification);
            var count = Interlocked.Increment(ref _pendingFaultNotificationCount);

            _logger.LogInformation(
                "設備 {DeviceName} 故障通知已加入批次佇列，目前待送 {PendingCount} 筆，等待 {BatchWindowSeconds} 秒後合併推播。",
                device.Name,
                count,
                batchWindow.TotalSeconds);

            var maxBatchSize = Math.Max(1, _options.MaxBatchSize);
            if (count >= maxBatchSize)
            {
                _logger.LogInformation("LINE 故障通知批次已達上限 {MaxBatchSize} 筆，立即合併推播。", maxBatchSize);
                _ = FlushFaultBatchSafelyAsync();
                return;
            }

            lock (_faultBatchLock)
            {
                if (_scheduledFaultFlushTask is null || _scheduledFaultFlushTask.IsCompleted)
                {
                    _scheduledFaultFlushTask = ScheduleFaultBatchFlushAsync(batchWindow);
                }
            }
        }

        private void EnqueueRecoveryNotification(Device device, string oldStatus, string newStatus, DateTime occurredAt, TimeSpan batchWindow)
        {
            var pendingRecoveryNotification = new PendingRecoveryNotification(
                device.Name,
                device.Area,
                device.ControllingEsp32MqttId,
                device.SlaveId,
                oldStatus,
                newStatus,
                occurredAt);

            _pendingRecoveryNotifications.Enqueue(pendingRecoveryNotification);
            var count = Interlocked.Increment(ref _pendingRecoveryNotificationCount);

            _logger.LogInformation(
                "設備 {DeviceName} 恢復通知已加入批次佇列，目前待送 {PendingCount} 筆，等待 {BatchWindowSeconds} 秒後合併推播。",
                device.Name,
                count,
                batchWindow.TotalSeconds);

            var maxBatchSize = Math.Max(1, _options.MaxBatchSize);
            if (count >= maxBatchSize)
            {
                _logger.LogInformation("LINE 恢復通知批次已達上限 {MaxBatchSize} 筆，立即合併推播。", maxBatchSize);
                _ = FlushRecoveryBatchSafelyAsync();
                return;
            }

            lock (_recoveryBatchLock)
            {
                if (_scheduledRecoveryFlushTask is null || _scheduledRecoveryFlushTask.IsCompleted)
                {
                    _scheduledRecoveryFlushTask = ScheduleRecoveryBatchFlushAsync(batchWindow);
                }
            }
        }

        private async Task ScheduleFaultBatchFlushAsync(TimeSpan batchWindow)
        {
            await Task.Delay(batchWindow);
            await FlushFaultBatchSafelyAsync();
        }

        private async Task ScheduleRecoveryBatchFlushAsync(TimeSpan batchWindow)
        {
            await Task.Delay(batchWindow);
            await FlushRecoveryBatchSafelyAsync();
        }

        private async Task FlushFaultBatchSafelyAsync()
        {
            try
            {
                var notifications = DrainPendingFaultNotifications();
                if (notifications.Count == 0)
                {
                    return;
                }

                await _lineNotificationService.SendTextMessageAsync(BuildFaultBatchMessage(notifications));
                _logger.LogInformation("LINE 故障通知已合併推播，共 {NotificationCount} 筆。", notifications.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "LINE 故障通知合併推播失敗。");
            }
        }

        private async Task FlushRecoveryBatchSafelyAsync()
        {
            try
            {
                var notifications = DrainPendingRecoveryNotifications();
                if (notifications.Count == 0)
                {
                    return;
                }

                await _lineNotificationService.SendTextMessageAsync(BuildRecoveryBatchMessage(notifications));
                _logger.LogInformation("LINE 恢復通知已合併推播，共 {NotificationCount} 筆。", notifications.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "LINE 恢復通知合併推播失敗。");
            }
        }

        private List<PendingFaultNotification> DrainPendingFaultNotifications()
        {
            var notifications = new List<PendingFaultNotification>();

            while (_pendingFaultNotifications.TryDequeue(out var notification))
            {
                notifications.Add(notification);
                Interlocked.Decrement(ref _pendingFaultNotificationCount);
            }

            return notifications;
        }

        private List<PendingRecoveryNotification> DrainPendingRecoveryNotifications()
        {
            var notifications = new List<PendingRecoveryNotification>();

            while (_pendingRecoveryNotifications.TryDequeue(out var notification))
            {
                notifications.Add(notification);
                Interlocked.Decrement(ref _pendingRecoveryNotificationCount);
            }

            return notifications;
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

        private static string BuildFaultBatchMessage(IReadOnlyCollection<PendingFaultNotification> notifications)
        {
            if (notifications.Count == 1)
            {
                var notification = notifications.Single();
                return $"⚠️ 設備故障通知\n\n" +
                       BuildFaultNotificationLine(notification, includeIndex: false, index: 1);
            }

            var orderedNotifications = notifications
                .OrderBy(notification => notification.OccurredAt)
                .ThenBy(notification => notification.DeviceName)
                .ToArray();

            var firstOccurredAt = orderedNotifications.First().OccurredAt;
            var lastOccurredAt = orderedNotifications.Last().OccurredAt;
            var messageBuilder = new StringBuilder();
            messageBuilder.AppendLine($"⚠️ 設備故障彙整通知（共 {orderedNotifications.Length} 台）");
            messageBuilder.AppendLine($"時間範圍：{firstOccurredAt:yyyy-MM-dd HH:mm:ss} ~ {lastOccurredAt:yyyy-MM-dd HH:mm:ss}");
            messageBuilder.AppendLine();

            for (var index = 0; index < orderedNotifications.Length; index++)
            {
                messageBuilder.AppendLine(BuildFaultNotificationLine(orderedNotifications[index], includeIndex: true, index: index + 1));
                if (index < orderedNotifications.Length - 1)
                {
                    messageBuilder.AppendLine();
                }
            }

            return messageBuilder.ToString().TrimEnd();
        }

        private static string BuildFaultNotificationLine(PendingFaultNotification notification, bool includeIndex, int index)
        {
            var prefix = includeIndex ? $"{index}. " : string.Empty;
            return $"{prefix}設備：{notification.DeviceName}\n" +
                   $"   區域：{notification.Area}\n" +
                   $"   ESP32：{notification.ControllingEsp32MqttId}\n" +
                   $"   Slave ID：{notification.SlaveId}\n" +
                   $"   狀態：{notification.OldStatus} → {notification.NewStatus}\n" +
                   $"   時間：{notification.OccurredAt:yyyy-MM-dd HH:mm:ss}";
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

        private static string BuildRecoveryBatchMessage(IReadOnlyCollection<PendingRecoveryNotification> notifications)
        {
            if (notifications.Count == 1)
            {
                var notification = notifications.Single();
                return $"✅ 設備恢復通知\n\n" +
                       BuildRecoveryNotificationLine(notification, includeIndex: false, index: 1);
            }

            var orderedNotifications = notifications
                .OrderBy(notification => notification.OccurredAt)
                .ThenBy(notification => notification.DeviceName)
                .ToArray();

            var firstOccurredAt = orderedNotifications.First().OccurredAt;
            var lastOccurredAt = orderedNotifications.Last().OccurredAt;
            var messageBuilder = new StringBuilder();
            messageBuilder.AppendLine($"✅ 設備恢復彙整通知（共 {orderedNotifications.Length} 台）");
            messageBuilder.AppendLine($"時間範圍：{firstOccurredAt:yyyy-MM-dd HH:mm:ss} ~ {lastOccurredAt:yyyy-MM-dd HH:mm:ss}");
            messageBuilder.AppendLine();

            for (var index = 0; index < orderedNotifications.Length; index++)
            {
                messageBuilder.AppendLine(BuildRecoveryNotificationLine(orderedNotifications[index], includeIndex: true, index: index + 1));
                if (index < orderedNotifications.Length - 1)
                {
                    messageBuilder.AppendLine();
                }
            }

            return messageBuilder.ToString().TrimEnd();
        }

        private static string BuildRecoveryNotificationLine(PendingRecoveryNotification notification, bool includeIndex, int index)
        {
            var prefix = includeIndex ? $"{index}. " : string.Empty;
            return $"{prefix}設備：{notification.DeviceName}\n" +
                   $"   區域：{notification.Area}\n" +
                   $"   ESP32：{notification.ControllingEsp32MqttId}\n" +
                   $"   Slave ID：{notification.SlaveId}\n" +
                   $"   狀態：{notification.OldStatus} → {notification.NewStatus}\n" +
                   $"   時間：{notification.OccurredAt:yyyy-MM-dd HH:mm:ss}";
        }

        private sealed record PendingFaultNotification(
            string DeviceName,
            string Area,
            string? ControllingEsp32MqttId,
            int SlaveId,
            string OldStatus,
            string NewStatus,
            DateTime OccurredAt);

        private sealed record PendingRecoveryNotification(
            string DeviceName,
            string Area,
            string? ControllingEsp32MqttId,
            int SlaveId,
            string OldStatus,
            string NewStatus,
            DateTime OccurredAt);
    }
}
