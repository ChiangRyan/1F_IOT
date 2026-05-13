using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Extensions.Logging;
using SANJET.Core.Configuration;
using SANJET.Core.Interfaces;
using Clipboard = System.Windows.Clipboard;

namespace SANJET.Core.Services
{
    public class LineAutoHotkeyNotificationService : ILineNotificationChannel
    {
        private readonly LineAutoHotkeyOptions _options;
        private readonly ILogger<LineAutoHotkeyNotificationService> _logger;
        private readonly SemaphoreSlim _sendLock = new(1, 1);

        public LineAutoHotkeyNotificationService(
            LineAutoHotkeyOptions options,
            ILogger<LineAutoHotkeyNotificationService> logger)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public string ChannelName => "LINE AutoHotkey";

        public bool IsEnabled => _options.Enabled;

        public bool IsConfigured =>
            IsEnabled &&
            !string.IsNullOrWhiteSpace(_options.AutoHotkeyExecutablePath);

        public async Task SendTextMessageAsync(string message, CancellationToken cancellationToken = default)
        {
            if (!IsConfigured)
            {
                _logger.LogWarning("LINE AutoHotkey 未啟用或尚未設定 AutoHotkeyExecutablePath，略過推播。訊息: {Message}", message);
                return;
            }

            await _sendLock.WaitAsync(cancellationToken);
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                await SendTextMessageToLineWindowAsync(message, cancellationToken);
            }
            finally
            {
                _sendLock.Release();
            }
        }

        private async Task SendTextMessageToLineWindowAsync(string message, CancellationToken cancellationToken)
        {
            string? originalClipboardText = null;
            var hadClipboardText = false;

            if (_options.RestoreClipboard)
            {
                (hadClipboardText, originalClipboardText) = await GetClipboardTextAsync(cancellationToken);
            }

            var tempDirectory = Path.Combine(Path.GetTempPath(), "SANJET-LineAutoHotkey", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDirectory);

            try
            {
                var messageFilePath = Path.Combine(tempDirectory, "message.txt");
                var scriptFilePath = Path.Combine(tempDirectory, "send-line-message.ahk");

                await File.WriteAllTextAsync(messageFilePath, message, new UTF8Encoding(false), cancellationToken);
                await File.WriteAllTextAsync(scriptFilePath, BuildAutoHotkeyScript(messageFilePath), new UTF8Encoding(false), cancellationToken);

                await RunAutoHotkeyScriptAsync(scriptFilePath, cancellationToken);
                _logger.LogInformation("LINE AutoHotkey 故障通知已送出。以目前 LINE 視窗直接貼上並送出。");
            }
            finally
            {
                if (_options.RestoreClipboard && hadClipboardText && originalClipboardText is not null)
                {
                    await SetClipboardTextAsync(originalClipboardText, cancellationToken);
                }

                TryDeleteDirectory(tempDirectory);
            }
        }

        private async Task RunAutoHotkeyScriptAsync(string scriptFilePath, CancellationToken cancellationToken)
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = _options.AutoHotkeyExecutablePath,
                    Arguments = $"/ErrorStdOut {QuoteArgument(scriptFilePath)}",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true
                },
                EnableRaisingEvents = true
            };

            _logger.LogDebug("啟動 AutoHotkey 傳送 LINE 訊息。ExecutablePath: {ExecutablePath}, ScriptPath: {ScriptPath}",
                _options.AutoHotkeyExecutablePath,
                scriptFilePath);

            try
            {
                process.Start();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"無法啟動 AutoHotkey。請確認 AutoHotkeyExecutablePath 是否正確：{_options.AutoHotkeyExecutablePath}", ex);
            }

            var timeout = TimeSpan.FromSeconds(Math.Max(_options.OperationTimeoutSeconds, 5));
            var waitTask = process.WaitForExitAsync(cancellationToken);
            var completedTask = await Task.WhenAny(waitTask, Task.Delay(timeout, cancellationToken));

            if (completedTask != waitTask)
            {
                TryKillProcess(process);
                throw new TimeoutException($"AutoHotkey 操作 LINE 超過 {timeout.TotalSeconds:0} 秒仍未完成。");
            }

            await waitTask;
            var standardOutput = await process.StandardOutput.ReadToEndAsync(cancellationToken);
            var standardError = await process.StandardError.ReadToEndAsync(cancellationToken);

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    $"AutoHotkey 傳送 LINE 訊息失敗。ExitCode: {process.ExitCode}, Output: {standardOutput}, Error: {standardError}");
            }
        }

        private string BuildAutoHotkeyScript(string messageFilePath)
        {
            return IsAutoHotkeyV1()
                ? BuildAutoHotkeyV1Script(messageFilePath)
                : BuildAutoHotkeyV2Script(messageFilePath);
        }

        private string BuildAutoHotkeyV2Script(string messageFilePath)
        {
            var processName = GetLineProcessExecutableName();
            var delay = Math.Max(_options.SendDelayMilliseconds, 100);
            var timeout = Math.Max(_options.OperationTimeoutSeconds, 5);

            return $$"""
                #Requires AutoHotkey v2.0
                #SingleInstance Force
                SetTitleMatchMode 2
                SetKeyDelay {{delay}}, {{delay}}
                CoordMode "Mouse", "Screen"

                lineExe := "{{EscapeAutoHotkeyV2String(_options.LineExecutablePath)}}"
                processName := "{{EscapeAutoHotkeyV2String(processName)}}"
                messageFile := "{{EscapeAutoHotkeyV2String(messageFilePath)}}"
                delay := {{delay}}
                timeoutSeconds := {{timeout}}

                message := FileRead(messageFile, "UTF-8")

                if !ProcessExist(processName) {
                    if (lineExe = "")
                        ExitApp 20
                    Run lineExe
                }

                if !WinWait("ahk_exe " processName, , timeoutSeconds)
                    ExitApp 21

                lineWindow := WinExist("ahk_exe " processName)
                if !lineWindow
                    ExitApp 21

                WinActivate "ahk_id " lineWindow
                if !WinWaitActive("ahk_id " lineWindow, , timeoutSeconds)
                    ExitApp 22

                FocusLineMessageInput(lineWindow, delay)

                A_Clipboard := message
                if !ClipWait(2)
                    ExitApp 23

                Send "^v"
                Sleep delay
                Send "{Enter}"
                Sleep delay * 2
                WinMinimize "ahk_id " lineWindow
                ExitApp 0

                FocusLineMessageInput(lineWindow, delay) {
                    WinActivate "ahk_id " lineWindow
                    WinGetPos &windowX, &windowY, &windowWidth, &windowHeight, "ahk_id " lineWindow
                    clickX := windowX + Round(windowWidth * 0.55)
                    clickY := windowY + windowHeight - 85
                    Click clickX, clickY
                    Sleep delay
                }
                """;
        }

        private string BuildAutoHotkeyV1Script(string messageFilePath)
        {
            var processName = GetLineProcessExecutableName();
            var delay = Math.Max(_options.SendDelayMilliseconds, 100);
            var timeout = Math.Max(_options.OperationTimeoutSeconds, 5);

            return $$"""
                #NoEnv
                #SingleInstance Force
                SetTitleMatchMode, 2
                SetKeyDelay, {{delay}}, {{delay}}
                CoordMode, Mouse, Screen

                lineExe := "{{EscapeAutoHotkeyV1String(_options.LineExecutablePath)}}"
                processName := "{{EscapeAutoHotkeyV1String(processName)}}"
                messageFile := "{{EscapeAutoHotkeyV1String(messageFilePath)}}"
                delay := {{delay}}
                timeoutSeconds := {{timeout}}

                FileRead, message, *P65001 %messageFile%

                Process, Exist, %processName%
                if (ErrorLevel = 0) {
                    if (lineExe = "")
                        ExitApp, 20
                    Run, %lineExe%
                }

                WinWait, ahk_exe %processName%,, %timeoutSeconds%
                if ErrorLevel
                    ExitApp, 21

                WinGet, lineWindow, ID, ahk_exe %processName%
                if (!lineWindow)
                    ExitApp, 21

                WinActivate, ahk_id %lineWindow%
                WinWaitActive, ahk_id %lineWindow%,, %timeoutSeconds%
                if ErrorLevel
                    ExitApp, 22

                Gosub, FocusLineMessageInput

                Clipboard := message
                ClipWait, 2
                if ErrorLevel
                    ExitApp, 23

                Send, ^v
                Sleep, %delay%
                Send, {Enter}
                sleepAfterSend := delay * 2
                Sleep, %sleepAfterSend%
                WinMinimize, ahk_id %lineWindow%
                ExitApp, 0

                FocusLineMessageInput:
                    WinActivate, ahk_id %lineWindow%
                    WinGetPos, windowX, windowY, windowWidth, windowHeight, ahk_id %lineWindow%
                    clickX := windowX + Round(windowWidth * 0.55)
                    clickY := windowY + windowHeight - 85
                    Click, %clickX%, %clickY%
                    Sleep, %delay%
                Return
                """;
        }

        private bool IsAutoHotkeyV1() =>
            string.Equals(_options.AutoHotkeyVersion, "v1", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(_options.AutoHotkeyVersion, "1", StringComparison.OrdinalIgnoreCase);

        private string GetLineProcessExecutableName()
        {
            var processName = string.IsNullOrWhiteSpace(_options.LineProcessName)
                ? "LINE"
                : _options.LineProcessName.Trim();

            return processName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                ? processName
                : $"{processName}.exe";
        }

        private static async Task<(bool HadText, string? Text)> GetClipboardTextAsync(CancellationToken cancellationToken)
        {
            return await RunOnUiThreadAsync(() =>
            {
                if (!Clipboard.ContainsText())
                {
                    return (false, null);
                }

                return (true, Clipboard.GetText());
            }, cancellationToken);
        }

        private static async Task SetClipboardTextAsync(string text, CancellationToken cancellationToken)
        {
            await RunOnUiThreadAsync(() => Clipboard.SetText(text), cancellationToken);
        }

        private static Task<T> RunOnUiThreadAsync<T>(Func<T> action, CancellationToken cancellationToken)
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher is null || dispatcher.CheckAccess())
            {
                return Task.FromResult(action());
            }

            return dispatcher.InvokeAsync(action, System.Windows.Threading.DispatcherPriority.Send, cancellationToken).Task;
        }

        private static Task RunOnUiThreadAsync(Action action, CancellationToken cancellationToken)
        {
            return RunOnUiThreadAsync(() =>
            {
                action();
                return true;
            }, cancellationToken);
        }

        private static string QuoteArgument(string value) => $"\"{value.Replace("\"", "\\\"")}\"";

        private static string EscapeAutoHotkeyV2String(string value) =>
            value.Replace("`", "``").Replace("\"", "`\"").Replace("\r", "`r").Replace("\n", "`n");

        private static string EscapeAutoHotkeyV1String(string value) =>
            value.Replace("`", "``").Replace("\"", "\"\"").Replace("\r", "`r").Replace("\n", "`n");

        private static void TryKillProcess(Process process)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch
            {
                // Best effort only.
            }
        }

        private void TryDeleteDirectory(string tempDirectory)
        {
            try
            {
                if (Directory.Exists(tempDirectory))
                {
                    Directory.Delete(tempDirectory, recursive: true);
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "刪除 AutoHotkey 暫存目錄失敗。TempDirectory: {TempDirectory}", tempDirectory);
            }
        }
    }
}
