using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SANJET.Core.Configuration;
using SANJET.Core.Interfaces;

namespace SANJET.Core.Services
{
    public class LineNotificationService : ILineNotificationService, IDisposable
    {
        private const string PushMessageEndpoint = "https://api.line.me/v2/bot/message/push";
        private readonly LineMessagingOptions _options;
        private readonly ILogger<LineNotificationService> _logger;
        private readonly HttpClient _httpClient;
        private bool _disposed;

        public LineNotificationService(LineMessagingOptions options, ILogger<LineNotificationService> logger)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        }

        public bool IsConfigured =>
            _options.Enabled &&
            !string.IsNullOrWhiteSpace(_options.ChannelAccessToken) &&
            _options.TargetIds.Any(targetId => !string.IsNullOrWhiteSpace(targetId));

        public async Task SendTextMessageAsync(string message, CancellationToken cancellationToken = default)
        {
            if (!IsConfigured)
            {
                _logger.LogWarning("LINE Messaging API 未啟用或尚未設定 ChannelAccessToken / TargetIds，略過推播。訊息: {Message}", message);
                return;
            }

            foreach (var targetId in _options.TargetIds.Where(targetId => !string.IsNullOrWhiteSpace(targetId)))
            {
                await SendTextMessageToTargetAsync(targetId.Trim(), message, cancellationToken);
            }
        }

        private async Task SendTextMessageToTargetAsync(string targetId, string message, CancellationToken cancellationToken)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, PushMessageEndpoint);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ChannelAccessToken);

            var payload = new
            {
                to = targetId,
                messages = new[]
                {
                    new
                    {
                        type = "text",
                        text = message
                    }
                }
            };

            request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("LINE 故障通知已送出。TargetId: {TargetId}", MaskTargetId(targetId));
                return;
            }

            _logger.LogError("LINE 故障通知送出失敗。StatusCode: {StatusCode}, TargetId: {TargetId}, Response: {Response}",
                (int)response.StatusCode,
                MaskTargetId(targetId),
                responseBody);
        }

        private static string MaskTargetId(string targetId)
        {
            if (targetId.Length <= 8)
            {
                return "****";
            }

            return $"{targetId[..4]}****{targetId[^4..]}";
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _httpClient.Dispose();
            _disposed = true;
        }
    }
}
