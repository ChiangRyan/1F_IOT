using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Extensions.Logging;
using SANJET.Core.Configuration;
using SANJET.Core.Interfaces;
using Clipboard = System.Windows.Clipboard;

namespace SANJET.Core.Services
{
    public class LineUiAutomationNotificationService : ILineNotificationChannel
    {
        private const int SW_RESTORE = 9;
        private const uint KEYEVENTF_KEYUP = 0x0002;
        private const byte VK_CONTROL = 0x11;
        private const byte VK_F = 0x46;
        private const byte VK_V = 0x56;
        private const byte VK_RETURN = 0x0D;
        private readonly LineUiAutomationOptions _options;
        private readonly ILogger<LineUiAutomationNotificationService> _logger;
        private readonly SemaphoreSlim _sendLock = new(1, 1);

        public LineUiAutomationNotificationService(
            LineUiAutomationOptions options,
            ILogger<LineUiAutomationNotificationService> logger)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public string ChannelName => "LINE UI Automation";

        public bool IsEnabled => _options.Enabled;

        public bool IsConfigured =>
            IsEnabled &&
            _options.TargetChatNames.Any(chatName => !string.IsNullOrWhiteSpace(chatName));

        public async Task SendTextMessageAsync(string message, CancellationToken cancellationToken = default)
        {
            if (!IsConfigured)
            {
                _logger.LogWarning("LINE UI Automation 未啟用或尚未設定 TargetChatNames，略過推播。訊息: {Message}", message);
                return;
            }

            await _sendLock.WaitAsync(cancellationToken);
            try
            {
                foreach (var chatName in _options.TargetChatNames.Where(chatName => !string.IsNullOrWhiteSpace(chatName)))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await SendTextMessageToChatAsync(chatName.Trim(), message, cancellationToken);
                }
            }
            finally
            {
                _sendLock.Release();
            }
        }

        private async Task SendTextMessageToChatAsync(string chatName, string message, CancellationToken cancellationToken)
        {
            var lineProcess = await EnsureLineProcessAsync(cancellationToken);
            await RunOnUiThreadAsync(() =>
            {
                ActivateLineWindow(lineProcess);
                WaitForUiDelay();

                string? originalClipboardText = null;
                var hadClipboardText = false;

                if (_options.RestoreClipboard && Clipboard.ContainsText())
                {
                    originalClipboardText = Clipboard.GetText();
                    hadClipboardText = true;
                }

                try
                {
                    // LINE 桌面版的 Ctrl+F 可開啟搜尋；貼上聊天室名稱後按 Enter 進入聊天室。
                    SendKeyboardShortcut(VK_CONTROL, VK_F);
                    WaitForUiDelay();
                    Clipboard.SetText(chatName);
                    SendKeyboardShortcut(VK_CONTROL, VK_V);
                    WaitForUiDelay();
                    SendVirtualKey(VK_RETURN);
                    WaitForUiDelay(2);

                    Clipboard.SetText(message);
                    SendKeyboardShortcut(VK_CONTROL, VK_V);
                    WaitForUiDelay();
                    SendVirtualKey(VK_RETURN);
                }
                finally
                {
                    if (_options.RestoreClipboard && hadClipboardText && originalClipboardText is not null)
                    {
                        Clipboard.SetText(originalClipboardText);
                    }
                }
            }, cancellationToken);

            _logger.LogInformation("LINE UI Automation 故障通知已送出。ChatName: {ChatName}", chatName);
        }

        private async Task<Process> EnsureLineProcessAsync(CancellationToken cancellationToken)
        {
            var timeout = TimeSpan.FromSeconds(Math.Max(1, _options.OperationTimeoutSeconds));
            var deadline = DateTime.UtcNow.Add(timeout);
            var process = FindLineProcess();

            if (process is not null)
            {
                return process;
            }

            if (string.IsNullOrWhiteSpace(_options.LineExecutablePath))
            {
                throw new InvalidOperationException("LINE UI Automation 已啟用，但 LINE 未啟動且未設定 LineExecutablePath。");
            }

            if (!File.Exists(_options.LineExecutablePath))
            {
                throw new FileNotFoundException("找不到 LINE 執行檔，請確認 LineExecutablePath 設定。", _options.LineExecutablePath);
            }

            _logger.LogInformation("LINE 未啟動，嘗試啟動 LINE：{LineExecutablePath}", _options.LineExecutablePath);
            Process.Start(new ProcessStartInfo
            {
                FileName = _options.LineExecutablePath,
                UseShellExecute = true
            });

            while (DateTime.UtcNow < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Delay(500, cancellationToken);
                process = FindLineProcess();
                if (process is not null)
                {
                    return process;
                }
            }

            throw new TimeoutException($"等待 LINE 啟動逾時，Timeout={timeout.TotalSeconds} 秒。");
        }

        private Process? FindLineProcess()
        {
            var processName = string.IsNullOrWhiteSpace(_options.LineProcessName)
                ? "LINE"
                : _options.LineProcessName.Trim();

            return Process.GetProcessesByName(processName)
                .OrderByDescending(process => process.MainWindowHandle != IntPtr.Zero)
                .FirstOrDefault();
        }

        private void ActivateLineWindow(Process lineProcess)
        {
            lineProcess.Refresh();
            if (lineProcess.MainWindowHandle == IntPtr.Zero)
            {
                throw new InvalidOperationException("找不到 LINE 主視窗，請確認 LINE 桌面版已登入並顯示主視窗。");
            }

            ShowWindow(lineProcess.MainWindowHandle, SW_RESTORE);
            if (!SetForegroundWindow(lineProcess.MainWindowHandle))
            {
                _logger.LogWarning("嘗試將 LINE 視窗切到前景時未取得成功回傳值，仍會繼續嘗試送出訊息。");
            }
        }

        private Task RunOnUiThreadAsync(Action action, CancellationToken cancellationToken)
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher is null)
            {
                throw new InvalidOperationException("找不到 WPF Dispatcher，無法執行 LINE UI Automation。");
            }

            if (dispatcher.CheckAccess())
            {
                action();
                return Task.CompletedTask;
            }

            return dispatcher.InvokeAsync(action).Task.WaitAsync(cancellationToken);
        }

        private void WaitForUiDelay(int multiplier = 1)
        {
            var delay = Math.Max(50, _options.SendDelayMilliseconds) * Math.Max(1, multiplier);
            Thread.Sleep(delay);
        }

        private static void SendKeyboardShortcut(byte modifierKey, byte key)
        {
            KeyDown(modifierKey);
            KeyDown(key);
            KeyUp(key);
            KeyUp(modifierKey);
        }

        private static void SendVirtualKey(byte key)
        {
            KeyDown(key);
            KeyUp(key);
        }

        private static void KeyDown(byte key)
        {
            keybd_event(key, 0, 0, UIntPtr.Zero);
        }

        private static void KeyUp(byte key)
        {
            keybd_event(key, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        }

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);
    }
}
