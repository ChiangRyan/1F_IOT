using LibVLCSharp.Shared;
using Microsoft.Extensions.DependencyInjection;
using SANJET.Core.Services;
using SANJET.Core.ViewModels;
using System;
using System.ComponentModel;
using System.Net.Sockets;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace SANJET.UI.Views.Windows
{
    [SupportedOSPlatform("windows7.0")]
    public partial class StreamWindow : Window
    {
        private LibVLC? _libVLC;
        private LibVLCSharp.Shared.MediaPlayer? _mediaPlayer1;
        private LibVLCSharp.Shared.MediaPlayer? _mediaPlayer2;
        private Media? _media1;
        private Media? _media2;
        private SettingsPageViewModel? _viewModel;
        private ILibVLCInitializationService? _libVLCService;
        private int _currentScreen = 1;
        private const int RtspConnectionTimeoutSeconds = 8;
        private const int RtspEndpointCheckTimeoutSeconds = 2;
        private bool _stream1Connected = false;
        private bool _stream2Connected = false;
        private bool _stream1Connecting = false;
        private bool _stream2Connecting = false;
        private CancellationTokenSource? _stream1ConnectionCts;
        private CancellationTokenSource? _stream2ConnectionCts;
        private bool _ownsLibVLC = false;
        private bool _isDisposed = false;

        public StreamWindow(SettingsPageViewModel viewModel)
        {
            InitializeComponent();

            _viewModel = viewModel;
            DataContext = _viewModel;

            // 嘗試從依賴注入中獲取預初始化的 LibVLC 服務
            try
            {
                _libVLCService = App.Host?.Services.GetService<ILibVLCInitializationService>();
            }
            catch
            {
                // 如果無法獲取服務，將在下面進行本地初始化
            }

            // 使用預初始化的服務或創建新的 LibVLC 實例
            if (_libVLCService != null)
            {
                _libVLC = _libVLCService.GetLibVLC();
            }
            else
            {
                // 後備方案：本地初始化
                LibVLCSharp.Shared.Core.Initialize();
                _libVLC = new LibVLC(
                    "--rtsp-tcp",
                    "--network-caching=300"
                );
                _ownsLibVLC = true;
            }

            _mediaPlayer1 = new LibVLCSharp.Shared.MediaPlayer(_libVLC);
            _mediaPlayer2 = new LibVLCSharp.Shared.MediaPlayer(_libVLC);
            RtspVideoView1.MediaPlayer = _mediaPlayer1;
            RtspVideoView2.MediaPlayer = _mediaPlayer2;

            Loaded += StreamWindow_Loaded;
            Closing += StreamWindow_Closing;
            Unloaded += StreamWindow_Unloaded;
        }

        private void StreamWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                _viewModel = DataContext as SettingsPageViewModel;
                ShowScreen1();
                UpdateStatusUI();
            }
            catch (Exception ex)
            {
                StreamStatusText.Text = $"初始化失敗: {ex.Message}";
                StreamStatusText.Foreground = Brushes.Red;
                StatusDot.Fill = Brushes.Red;
            }
        }

        private void StreamWindow_Closing(object? sender, CancelEventArgs e)
        {
            DisposeStreamResources();
        }

        private void StreamWindow_Unloaded(object sender, RoutedEventArgs e)
        {
            DisposeStreamResources();
        }

        private void DisposeStreamResources()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;

            StopStream1(updateUi: false);
            StopStream2(updateUi: false);

            RtspVideoView1.MediaPlayer = null;
            RtspVideoView2.MediaPlayer = null;

            _mediaPlayer1?.Dispose();
            _mediaPlayer1 = null;

            _mediaPlayer2?.Dispose();
            _mediaPlayer2 = null;

            // 如果 LibVLC 由全域 ILibVLCInitializationService 管理，不能在視窗關閉時釋放；
            // 只有本視窗自行建立的後備實例才需要在此 Dispose。
            if (_ownsLibVLC)
            {
                _libVLC?.Dispose();
                _ownsLibVLC = false;
            }

            _libVLC = null;
        }

        private void UpdateStatusUI()
        {
            if (_currentScreen == 1)
            {
                StreamStatusText.Text = _stream1Connected
                    ? "攝像頭 1 - 已連接"
                    : "攝像頭 1 - 未連接";
                StreamStatusText.Foreground = Brushes.White;

                StatusDot.Fill = _stream1Connected
                    ? Brushes.LimeGreen
                    : Brushes.Gray;
            }
            else
            {
                StreamStatusText.Text = _stream2Connected
                    ? "攝像頭 2 - 已連接"
                    : "攝像頭 2 - 未連接";
                StreamStatusText.Foreground = Brushes.White;

                StatusDot.Fill = _stream2Connected
                    ? Brushes.LimeGreen
                    : Brushes.Gray;
            }
        }

        private void UpdateControlButtons()
        {
            bool connected = _currentScreen == 1
                ? _stream1Connected
                : _stream2Connected;

            bool connecting = _currentScreen == 1
                ? _stream1Connecting
                : _stream2Connecting;

            ConnectButton1.Visibility = connected ? Visibility.Collapsed : Visibility.Visible;
            ConnectButton1.IsEnabled = !connecting;
            DisconnectButton1.Visibility = connected || connecting ? Visibility.Visible : Visibility.Collapsed;
        }

        private async void Connect1_Click(object sender, RoutedEventArgs e)
        {
            if (_currentScreen == 1)
                await StartStream1();
            else
                await StartStream2();
        }

        private void Disconnect1_Click(object sender, RoutedEventArgs e)
        {
            if (_currentScreen == 1)
                StopStream1();
            else
                StopStream2();
        }

        private async Task StartStream1()
        {
            try
            {
                if (_isDisposed || _libVLC == null || _mediaPlayer1 == null || _viewModel == null)
                    return;

                if (_stream1Connected || _stream1Connecting)
                    return;

                _currentScreen = 1;
                _stream1Connecting = true;
                var connectionCts = ResetConnectionToken(ref _stream1ConnectionCts);

                StreamStatusText.Text = "攝像頭 1 - 檢查連線...";
                StreamStatusText.Foreground = Brushes.White;
                StatusDot.Fill = Brushes.Orange;
                UpdateControlButtons();

                var rtspUrl = _viewModel.BuildRtspUrl1();
                await EnsureRtspEndpointReachableAsync(rtspUrl, 1, connectionCts.Token);
                StreamStatusText.Text = "攝像頭 1 - 連接中...";

                ResetPlayerBeforeStart(_mediaPlayer1, ref _media1);

                _media1 = new Media(_libVLC, new Uri(rtspUrl));
                _media1.AddOption(":rtsp-tcp");
                _media1.AddOption(":network-caching=300");

                RtspVideoView1.MediaPlayer = _mediaPlayer1;
                var playbackStartedTask = WaitForPlaybackStartedAsync(_mediaPlayer1, 1, connectionCts.Token);
                if (!_mediaPlayer1.Play(_media1))
                {
                    connectionCts.Cancel();
                    throw new InvalidOperationException("攝像頭 1 播放器啟動失敗。");
                }

                await playbackStartedTask;

                _stream1Connected = true;

                ConnectButton1.Visibility = Visibility.Collapsed;
                DisconnectButton1.Visibility = Visibility.Visible;

                UpdateStatusUI();
            }
            catch (Exception ex)
            {
                _stream1Connected = false;
                _stream1Connecting = false;

                if (ex is OperationCanceledException && !_isDisposed)
                {
                    UpdateStatusUI();
                    return;
                }

                StopStream1(updateUi: false);

                StreamStatusText.Text = $"攝像頭 1 - 連接失敗: {ex.Message}";
                StreamStatusText.Foreground = Brushes.Red;
                StatusDot.Fill = Brushes.Red;

                ConnectButton1.Visibility = Visibility.Visible;
                DisconnectButton1.Visibility = Visibility.Collapsed;
            }
            finally
            {
                _stream1Connecting = false;
                if (!_isDisposed)
                    UpdateControlButtons();
            }
        }

        private async Task StartStream2()
        {
            try
            {
                if (_isDisposed || _libVLC == null || _mediaPlayer2 == null || _viewModel == null)
                    return;

                if (_stream2Connected || _stream2Connecting)
                    return;

                _currentScreen = 2;
                _stream2Connecting = true;
                var connectionCts = ResetConnectionToken(ref _stream2ConnectionCts);

                StreamStatusText.Text = "攝像頭 2 - 檢查連線...";
                StreamStatusText.Foreground = Brushes.White;
                StatusDot.Fill = Brushes.Orange;
                UpdateControlButtons();

                var rtspUrl = _viewModel.BuildRtspUrl2();
                await EnsureRtspEndpointReachableAsync(rtspUrl, 2, connectionCts.Token);
                StreamStatusText.Text = "攝像頭 2 - 連接中...";

                ResetPlayerBeforeStart(_mediaPlayer2, ref _media2);

                _media2 = new Media(_libVLC, new Uri(rtspUrl));
                _media2.AddOption(":rtsp-tcp");
                _media2.AddOption(":network-caching=300");

                RtspVideoView2.MediaPlayer = _mediaPlayer2;
                var playbackStartedTask = WaitForPlaybackStartedAsync(_mediaPlayer2, 2, connectionCts.Token);
                if (!_mediaPlayer2.Play(_media2))
                {
                    connectionCts.Cancel();
                    throw new InvalidOperationException("攝像頭 2 播放器啟動失敗。");
                }

                await playbackStartedTask;

                _stream2Connected = true;

                UpdateControlButtons();
                UpdateStatusUI();
            }
            catch (Exception ex)
            {
                _stream2Connected = false;
                _stream2Connecting = false;

                if (ex is OperationCanceledException && !_isDisposed)
                {
                    UpdateStatusUI();
                    return;
                }

                StopStream2(updateUi: false);

                StreamStatusText.Text = $"攝像頭 2 - 連接失敗: {ex.Message}";
                StreamStatusText.Foreground = Brushes.Red;
                StatusDot.Fill = Brushes.Red;

                UpdateControlButtons();
            }
            finally
            {
                _stream2Connecting = false;
                if (!_isDisposed)
                    UpdateControlButtons();
            }
        }

        private static CancellationTokenSource ResetConnectionToken(ref CancellationTokenSource? connectionCts)
        {
            connectionCts?.Cancel();
            connectionCts?.Dispose();
            connectionCts = new CancellationTokenSource();
            return connectionCts;
        }


        private static async Task EnsureRtspEndpointReachableAsync(string rtspUrl, int cameraNumber, CancellationToken cancellationToken)
        {
            if (!Uri.TryCreate(rtspUrl, UriKind.Absolute, out var uri) || !string.Equals(uri.Scheme, Uri.UriSchemeRtsp, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"攝像頭 {cameraNumber} RTSP 位址格式錯誤，請重新檢查設定。");
            }

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(RtspEndpointCheckTimeoutSeconds));

            try
            {
                using var tcpClient = new TcpClient();
                await tcpClient.ConnectAsync(uri.Host, uri.Port, timeoutCts.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException($"攝像頭 {cameraNumber} 無法連線到 {uri.Host}:{uri.Port}，請確認攝影機已安裝、已上線，且 RTSP IP/連接埠正確。");
            }
            catch (SocketException ex)
            {
                throw new InvalidOperationException($"攝像頭 {cameraNumber} 無法連線到 {uri.Host}:{uri.Port}，請確認攝影機已安裝、已上線，且 RTSP IP/連接埠正確。", ex);
            }
        }

        private async Task WaitForPlaybackStartedAsync(LibVLCSharp.Shared.MediaPlayer mediaPlayer, int cameraNumber, CancellationToken cancellationToken)
        {
            var playbackStarted = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);

            void PlayingHandler(object? sender, EventArgs args) => playbackStarted.TrySetResult(null);
            void EncounteredErrorHandler(object? sender, EventArgs args) => playbackStarted.TrySetException(new InvalidOperationException($"攝像頭 {cameraNumber} 無法建立 RTSP 串流，請確認攝影機已安裝、IP/帳密/串流路徑正確。"));
            void StoppedHandler(object? sender, EventArgs args) => playbackStarted.TrySetException(new InvalidOperationException($"攝像頭 {cameraNumber} RTSP 串流已停止。"));

            mediaPlayer.Playing += PlayingHandler;
            mediaPlayer.EncounteredError += EncounteredErrorHandler;
            mediaPlayer.Stopped += StoppedHandler;

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(RtspConnectionTimeoutSeconds));
            using var cancellationRegistration = timeoutCts.Token.Register(() => playbackStarted.TrySetCanceled(timeoutCts.Token));

            try
            {
                await playbackStarted.Task;
                await WaitForVisibleVideoAsync(mediaPlayer, cameraNumber, timeoutCts.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException($"攝像頭 {cameraNumber} 連線逾時（{RtspConnectionTimeoutSeconds} 秒），請確認攝影機已上線並可存取 RTSP。若畫面仍為黑色，請檢查攝影機是否真的輸出影像。");
            }
            finally
            {
                mediaPlayer.Playing -= PlayingHandler;
                mediaPlayer.EncounteredError -= EncounteredErrorHandler;
                mediaPlayer.Stopped -= StoppedHandler;
            }
        }

        private static async Task WaitForVisibleVideoAsync(LibVLCSharp.Shared.MediaPlayer mediaPlayer, int cameraNumber, CancellationToken cancellationToken)
        {
            var voutCountProperty = mediaPlayer.GetType().GetProperty("VoutCount");

            if (voutCountProperty == null)
            {
                await Task.Delay(750, cancellationToken);
                if (!mediaPlayer.IsPlaying)
                    throw new InvalidOperationException($"攝像頭 {cameraNumber} RTSP 串流未保持播放狀態。");

                return;
            }

            while (!cancellationToken.IsCancellationRequested)
            {
                if (!mediaPlayer.IsPlaying)
                    throw new InvalidOperationException($"攝像頭 {cameraNumber} RTSP 串流未保持播放狀態。");

                var voutCountValue = voutCountProperty.GetValue(mediaPlayer);
                if (voutCountValue != null && Convert.ToUInt32(voutCountValue) > 0)
                    return;

                await Task.Delay(100, cancellationToken);
            }

            cancellationToken.ThrowIfCancellationRequested();
        }

        private static void ResetPlayerBeforeStart(LibVLCSharp.Shared.MediaPlayer mediaPlayer, ref Media? media)
        {
            // 不要對剛建立、尚未綁定媒體的播放器呼叫 Stop()；部分 LibVLC/WPF 組合
            // 會讓接下來的第一次 Play() 進入異常狀態，造成 UI 顯示已連接但畫面不出現。
            if (media != null || mediaPlayer.IsPlaying)
            {
                mediaPlayer.Stop();
                Thread.Sleep(100);
            }

            media?.Dispose();
            media = null;
        }

        private void StopStream1(bool updateUi = true)
        {
            try
            {
                _stream1ConnectionCts?.Cancel();

                _mediaPlayer1?.Stop();
                Thread.Sleep(200);

                _media1?.Dispose();
                _media1 = null;

                _stream1Connected = false;
                _stream1Connecting = false;

                if (updateUi && !_isDisposed)
                {
                    ConnectButton1.Visibility = Visibility.Visible;
                    DisconnectButton1.Visibility = Visibility.Collapsed;

                    // ⭐ 統一更新 UI（重點）
                    UpdateStatusUI();
                }
            }
            catch (Exception ex)
            {
                if (updateUi && !_isDisposed)
                {
                    StreamStatusText.Text = $"攝像頭 1 - 斷開失敗: {ex.Message}";
                    StreamStatusText.Foreground = Brushes.Red;
                    StatusDot.Fill = Brushes.Red;
                }
            }
        }

        private void StopStream2(bool updateUi = true)
        {
            try
            {
                _stream2ConnectionCts?.Cancel();

                _mediaPlayer2?.Stop();
                Thread.Sleep(200);

                _media2?.Dispose();
                _media2 = null;

                _stream2Connected = false;
                _stream2Connecting = false;

                if (updateUi && !_isDisposed)
                {
                    UpdateControlButtons();
                    UpdateStatusUI();
                }
            }
            catch (Exception ex)
            {
                if (updateUi && !_isDisposed)
                {
                    StreamStatusText.Text = $"攝像頭 2 - 斷開失敗: {ex.Message}";
                    StreamStatusText.Foreground = Brushes.Red;
                    StatusDot.Fill = Brushes.Red;
                }
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void ShowScreen1()
        {
            _currentScreen = 1;
            Screen1Container.Visibility = Visibility.Visible;
            Screen2Container.Visibility = Visibility.Collapsed;

            // Stop stream 2 when switching away from it
            if (_stream2Connected || _stream2Connecting)
            {
                StopStream2();
                UpdateStatusUI();
            }

            Button1.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF2196F3"));
            Button2.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF555555"));
        }

        private void ShowScreen2()
        {
            _currentScreen = 2;
            Screen1Container.Visibility = Visibility.Collapsed;
            Screen2Container.Visibility = Visibility.Visible;

            // Stop stream 1 when switching away from it
            if (_stream1Connected || _stream1Connecting)
            {
                StopStream1();
                UpdateStatusUI();
            }

            Button1.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF555555"));
            Button2.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF2196F3"));
        }

        private void Screen1_Click(object sender, RoutedEventArgs e)
        {
            ShowScreen1();
        }

        private void Screen2_Click(object sender, RoutedEventArgs e)
        {
            ShowScreen2();
        }

    }
}
