using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using Microsoft.Extensions.Logging;
using SANJET.Core.Configuration;
using SANJET.Core.Interfaces;
using Clipboard = System.Windows.Clipboard;
using SendKeys = System.Windows.Forms.SendKeys;

namespace SANJET.Core.Services
{
    public class LineUiAutomationNotificationService : ILineNotificationChannel
    {
        private const int SW_RESTORE = 9;
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
                    var lineWindow = AutomationElement.FromHandle(lineProcess.MainWindowHandle)
                        ?? throw new InvalidOperationException("無法取得 LINE 主視窗的 UI Automation 元素。");

                    OpenChatWithoutSearch(lineWindow, chatName);
                    WaitForUiDelay(2);

                    lineWindow = AutomationElement.FromHandle(lineProcess.MainWindowHandle)
                        ?? throw new InvalidOperationException("無法重新取得 LINE 主視窗的 UI Automation 元素。");

                    var inputBox = FocusMessageInput(lineWindow);
                    WaitForUiDelay();

                    PasteMessage(inputBox, message);
                    WaitForUiDelay();
                    SendEnterKey();
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

        private void OpenChatWithoutSearch(AutomationElement lineWindow, string chatName)
        {
            var chatElement = FindChatElement(lineWindow, chatName)
                ?? throw new InvalidOperationException($"LINE 視窗中找不到可直接點選的聊天室「{chatName}」。請先將該聊天室固定或保留在左側聊天清單中，避免使用 Ctrl+F 搜尋時貼錯位置。");

            if (!TrySelectElement(chatElement))
            {
                ClickElement(chatElement, $"聊天室「{chatName}」");
            }
        }

        private AutomationElement? FindChatElement(AutomationElement lineWindow, string chatName)
        {
            var chatNameCondition = new PropertyCondition(AutomationElement.NameProperty, chatName, PropertyConditionFlags.IgnoreCase);
            var controlTypeCondition = new OrCondition(
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.ListItem),
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button),
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Text),
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Custom),
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.DataItem));

            var exactMatches = lineWindow.FindAll(
                TreeScope.Descendants,
                new AndCondition(chatNameCondition, controlTypeCondition));

            var exactMatch = exactMatches
                .Cast<AutomationElement>()
                .Where(element => IsInChatListArea(lineWindow, element))
                .OrderBy(GetElementTop)
                .FirstOrDefault();

            if (exactMatch is not null)
            {
                return GetClickableAncestor(exactMatch) ?? exactMatch;
            }

            var candidates = lineWindow.FindAll(TreeScope.Descendants, controlTypeCondition)
                .Cast<AutomationElement>()
                .Where(element => ElementNameMatches(element, chatName))
                .Where(element => IsInChatListArea(lineWindow, element))
                .OrderBy(GetElementTop)
                .ToArray();

            return candidates
                .Select(element => GetClickableAncestor(element) ?? element)
                .FirstOrDefault();
        }

        private AutomationElement FocusMessageInput(AutomationElement lineWindow)
        {
            var editCondition = new AndCondition(
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit),
                new PropertyCondition(AutomationElement.IsEnabledProperty, true));

            var inputBox = lineWindow.FindAll(TreeScope.Descendants, editCondition)
                .Cast<AutomationElement>()
                .Where(element => IsLikelyMessageInput(lineWindow, element))
                .OrderByDescending(GetElementTop)
                .FirstOrDefault();

            if (inputBox is null)
            {
                throw new InvalidOperationException("找不到 LINE 訊息輸入框，已停止貼上訊息以避免送到錯誤位置。");
            }

            if (!TryFocusElement(inputBox))
            {
                ClickElement(inputBox, "LINE 訊息輸入框");
            }
            else if (!TryClickElement(inputBox, "LINE 訊息輸入框"))
            {
                _logger.LogDebug("LINE 訊息輸入框可取得焦點但沒有可點擊座標，將只使用鍵盤貼上。ElementName: {ElementName}", inputBox.Current.Name);
            }

            return inputBox;
        }

        private void PasteMessage(AutomationElement inputBox, string message)
        {
            SetClipboardTextWithRetry(message);

            if (TrySetInputValue(inputBox, message))
            {
                _logger.LogDebug("LINE UI Automation 已透過 ValuePattern 寫入訊息輸入框。");
                return;
            }

            SendPasteShortcut();
            WaitForUiDelay(2);

            var inputHasText = InputContainsMessage(inputBox, message);
            if (inputHasText == true)
            {
                return;
            }

            _logger.LogDebug(
                inputHasText is null
                    ? "LINE 訊息輸入框不支援讀取內容，改用 SendKeys 再貼上一次以提高成功率。"
                    : "LINE UI Automation 第一次 Ctrl+V 後未偵測到訊息，改用 SendKeys 再貼上一次。");

            SendKeys.SendWait("^v");
            WaitForUiDelay(2);

            inputHasText = InputContainsMessage(inputBox, message);
            if (inputHasText == false)
            {
                throw new InvalidOperationException("已點選 LINE 訊息輸入框，但無法確認訊息已貼上，已停止送出以避免傳送空白訊息。");
            }

            if (inputHasText is null)
            {
                _logger.LogWarning("LINE 訊息輸入框不支援讀取內容，已執行兩種貼上方式但無法自動驗證文字是否出現。若畫面沒有文字，請檢查 LINE 是否在前景、聊天室是否已開啟，以及 Windows 是否允許程式操作剪貼簿/鍵盤。");
            }
        }

        private void SetClipboardTextWithRetry(string message)
        {
            Exception? lastException = null;

            for (var attempt = 1; attempt <= 3; attempt++)
            {
                try
                {
                    Clipboard.SetDataObject(message, true);

                    if (Clipboard.ContainsText() && Clipboard.GetText() == message)
                    {
                        return;
                    }
                }
                catch (ExternalException ex)
                {
                    lastException = ex;
                    _logger.LogDebug(ex, "設定剪貼簿失敗，準備重試。Attempt: {Attempt}", attempt);
                }

                WaitForUiDelay();
            }

            throw new InvalidOperationException("無法將 LINE 訊息寫入剪貼簿，請確認剪貼簿未被其他程式鎖定。", lastException);
        }

        private static bool TrySetInputValue(AutomationElement inputBox, string message)
        {
            if (!inputBox.TryGetCurrentPattern(ValuePattern.Pattern, out var valuePatternObject) ||
                valuePatternObject is not ValuePattern valuePattern ||
                valuePattern.Current.IsReadOnly)
            {
                return false;
            }

            valuePattern.SetValue(message);
            return true;
        }

        private static bool? InputContainsMessage(AutomationElement inputBox, string message)
        {
            if (inputBox.TryGetCurrentPattern(ValuePattern.Pattern, out var valuePatternObject) &&
                valuePatternObject is ValuePattern valuePattern)
            {
                return valuePattern.Current.Value?.Contains(message, StringComparison.Ordinal) == true;
            }

            if (inputBox.TryGetCurrentPattern(TextPattern.Pattern, out var textPatternObject) &&
                textPatternObject is TextPattern textPattern)
            {
                return textPattern.DocumentRange.GetText(-1).Contains(message, StringComparison.Ordinal);
            }

            return null;
        }

        private static void SendPasteShortcut()
        {
            keybd_event(VK_CONTROL, 0, 0, UIntPtr.Zero);
            keybd_event(VK_V, 0, 0, UIntPtr.Zero);
            keybd_event(VK_V, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        }

        private static void SendEnterKey()
        {
            keybd_event(VK_RETURN, 0, 0, UIntPtr.Zero);
            keybd_event(VK_RETURN, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        }

        private static bool ElementNameMatches(AutomationElement element, string expectedName)
        {
            var name = element.Current.Name?.Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            return string.Equals(name, expectedName, StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith(expectedName + " ", StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith(expectedName + Environment.NewLine, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsInChatListArea(AutomationElement lineWindow, AutomationElement element)
        {
            var windowRectangle = lineWindow.Current.BoundingRectangle;
            var elementRectangle = element.Current.BoundingRectangle;

            if (windowRectangle.IsEmpty || elementRectangle.IsEmpty || element.Current.IsOffscreen)
            {
                return false;
            }

            var elementCenterX = elementRectangle.Left + (elementRectangle.Width / 2);
            var chatListRightBoundary = windowRectangle.Left + (windowRectangle.Width * 0.45);
            return elementCenterX <= chatListRightBoundary;
        }

        private static AutomationElement? GetClickableAncestor(AutomationElement element)
        {
            var walker = TreeWalker.ControlViewWalker;
            var current = element;

            for (var depth = 0; depth < 4 && current is not null; depth++)
            {
                if (SupportsPattern(current, SelectionItemPattern.Pattern) ||
                    SupportsPattern(current, InvokePattern.Pattern) ||
                    HasUsableClickablePoint(current))
                {
                    return current;
                }

                current = walker.GetParent(current);
            }

            return null;
        }

        private bool TrySelectElement(AutomationElement element)
        {
            if (element.TryGetCurrentPattern(SelectionItemPattern.Pattern, out var selectionPatternObject) &&
                selectionPatternObject is SelectionItemPattern selectionPattern)
            {
                selectionPattern.Select();
                return true;
            }

            if (element.TryGetCurrentPattern(InvokePattern.Pattern, out var invokePatternObject) &&
                invokePatternObject is InvokePattern invokePattern)
            {
                invokePattern.Invoke();
                return true;
            }

            return false;
        }

        private bool TryFocusElement(AutomationElement element)
        {
            try
            {
                element.SetFocus();
                return true;
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogDebug(ex, "LINE UI Automation 無法直接 Focus 元素，改用滑鼠點擊。ElementName: {ElementName}", element.Current.Name);
                return false;
            }
        }

        private void ClickElement(AutomationElement element, string description)
        {
            if (!TryClickElement(element, description))
            {
                throw new InvalidOperationException($"找不到可點擊的 {description}，已停止操作以避免貼錯位置。");
            }
        }

        private bool TryClickElement(AutomationElement element, string description)
        {
            if (!element.TryGetClickablePoint(out var point))
            {
                _logger.LogDebug("找不到可點擊的 {Description}。ElementName: {ElementName}", description, element.Current.Name);
                return false;
            }

            SetCursorPos((int)Math.Round(point.X), (int)Math.Round(point.Y));
            WaitForUiDelay();
            mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, UIntPtr.Zero);
            mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, UIntPtr.Zero);
            return true;
        }

        private static bool IsLikelyMessageInput(AutomationElement lineWindow, AutomationElement element)
        {
            if (element.Current.IsOffscreen)
            {
                return false;
            }

            var windowRectangle = lineWindow.Current.BoundingRectangle;
            var rectangle = element.Current.BoundingRectangle;
            if (windowRectangle.IsEmpty || rectangle.IsEmpty || rectangle.Width < 100 || rectangle.Height < 20)
            {
                return false;
            }

            var elementCenterX = rectangle.Left + (rectangle.Width / 2);
            var elementCenterY = rectangle.Top + (rectangle.Height / 2);
            var composeAreaLeftBoundary = windowRectangle.Left + (windowRectangle.Width * 0.35);
            var composeAreaTopBoundary = windowRectangle.Top + (windowRectangle.Height * 0.55);

            return elementCenterX >= composeAreaLeftBoundary && elementCenterY >= composeAreaTopBoundary;
        }

        private static bool SupportsPattern(AutomationElement element, AutomationPattern pattern)
        {
            return element.TryGetCurrentPattern(pattern, out _);
        }

        private static bool HasUsableClickablePoint(AutomationElement element)
        {
            return !element.Current.IsOffscreen && element.TryGetClickablePoint(out _);
        }

        private static double GetElementTop(AutomationElement element)
        {
            var rectangle = element.Current.BoundingRectangle;
            return rectangle.IsEmpty ? double.MaxValue : rectangle.Top;
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

        private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        private const uint MOUSEEVENTF_LEFTUP = 0x0004;
        private const uint KEYEVENTF_KEYUP = 0x0002;
        private const byte VK_CONTROL = 0x11;
        private const byte VK_RETURN = 0x0D;
        private const byte VK_V = 0x56;

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool SetCursorPos(int x, int y);

        [DllImport("user32.dll")]
        private static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);

        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);
    }
}
