using LibVLCSharp.Shared;
using Microsoft.Extensions.DependencyInjection;
using SANJET.Core.Services;
using SANJET.Core.ViewModels;
using System;
using System.Runtime.Versioning;
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
        private bool _stream1Connected = false;
        private bool _stream2Connected = false;

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
            }

            _mediaPlayer1 = new LibVLCSharp.Shared.MediaPlayer(_libVLC);
            _mediaPlayer2 = new LibVLCSharp.Shared.MediaPlayer(_libVLC);
            RtspVideoView1.MediaPlayer = _mediaPlayer1;
            RtspVideoView2.MediaPlayer = _mediaPlayer2;

            Loaded += StreamWindow_Loaded;
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

        private void StreamWindow_Unloaded(object sender, RoutedEventArgs e)
        {
            StopStream1();
            StopStream2();

            RtspVideoView1.MediaPlayer = null;
            RtspVideoView2.MediaPlayer = null;

            _mediaPlayer1?.Dispose();
            _mediaPlayer1 = null;

            _mediaPlayer2?.Dispose();
            _mediaPlayer2 = null;

            // 不清理 _libVLC，因為它是通過 ILibVLCInitializationService 管理的全局單例
            // 清理將由應用程序關閉時的服務清理處理
            _libVLC = null;
        }

        private void UpdateStatusUI()
        {
            if (_currentScreen == 1)
            {
                StreamStatusText.Text = _stream1Connected
                    ? "攝像頭 1 - 已連接"
                    : "攝像頭 1 - 未連接";

                StatusDot.Fill = _stream1Connected
                    ? Brushes.LimeGreen
                    : Brushes.Gray;
            }
            else
            {
                StreamStatusText.Text = _stream2Connected
                    ? "攝像頭 2 - 已連接"
                    : "攝像頭 2 - 未連接";

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

            ConnectButton1.Visibility = connected ? Visibility.Collapsed : Visibility.Visible;
            DisconnectButton1.Visibility = connected ? Visibility.Visible : Visibility.Collapsed;
        }

        private void Connect1_Click(object sender, RoutedEventArgs e)
        {
            if (_currentScreen == 1)
                StartStream1();
            else
                StartStream2();
        }

        private void Disconnect1_Click(object sender, RoutedEventArgs e)
        {
            if (_currentScreen == 1)
                StopStream1();
            else
                StopStream2();
        }

        private void StartStream1()
        {
            try
            {
                if (_libVLC == null || _mediaPlayer1 == null || _viewModel == null)
                    return;

                if (_stream1Connected)
                    return;

                _currentScreen = 1;

                StreamStatusText.Text = "攝像頭 1 - 連接中...";
                StreamStatusText.Foreground = Brushes.White;
                StatusDot.Fill = Brushes.Orange;

                var rtspUrl = _viewModel.BuildRtspUrl1();

                if (_mediaPlayer1.IsPlaying)
                {
                    _mediaPlayer1.Stop();
                    System.Threading.Thread.Sleep(100);
                }

                _media1?.Dispose();
                _media1 = null;

                _media1 = new Media(_libVLC, new Uri(rtspUrl));
                _media1.AddOption(":rtsp-tcp");
                _media1.AddOption(":network-caching=300");

                _mediaPlayer1.Play(_media1);

                _stream1Connected = true;

                ConnectButton1.Visibility = Visibility.Collapsed;
                DisconnectButton1.Visibility = Visibility.Visible;

                UpdateStatusUI();
            }
            catch (Exception ex)
            {
                _stream1Connected = false;

                StreamStatusText.Text = $"攝像頭 1 - 連接失敗: {ex.Message}";
                StreamStatusText.Foreground = Brushes.Red;
                StatusDot.Fill = Brushes.Red;

                ConnectButton1.Visibility = Visibility.Visible;
                DisconnectButton1.Visibility = Visibility.Collapsed;
            }
        }

        private void StartStream2()
        {
            try
            {
                if (_libVLC == null || _mediaPlayer2 == null || _viewModel == null)
                    return;

                if (_stream2Connected)
                    return;

                _currentScreen = 2;

                StreamStatusText.Text = "攝像頭 2 - 連接中...";
                StreamStatusText.Foreground = Brushes.White;
                StatusDot.Fill = Brushes.Orange;

                var rtspUrl = _viewModel.BuildRtspUrl2();

                if (_mediaPlayer2.IsPlaying)
                {
                    _mediaPlayer2.Stop();
                    System.Threading.Thread.Sleep(100);
                }

                _media2?.Dispose();
                _media2 = null;

                _media2 = new Media(_libVLC, new Uri(rtspUrl));
                _media2.AddOption(":rtsp-tcp");
                _media2.AddOption(":network-caching=300");

                _mediaPlayer2.Play(_media2);

                _stream2Connected = true;

                UpdateControlButtons();
                UpdateStatusUI();
            }
            catch (Exception ex)
            {
                _stream2Connected = false;

                StreamStatusText.Text = $"攝像頭 2 - 連接失敗: {ex.Message}";
                StreamStatusText.Foreground = Brushes.Red;
                StatusDot.Fill = Brushes.Red;

                UpdateControlButtons();
            }
        }

        private void StopStream1()
        {
            try
            {
                if (_mediaPlayer1 != null)
                {
                    if (_mediaPlayer1.IsPlaying)
                    {
                        _mediaPlayer1.Stop();
                        System.Threading.Thread.Sleep(200);
                    }
                }

                _media1?.Dispose();
                _media1 = null;

                _stream1Connected = false;

                ConnectButton1.Visibility = Visibility.Visible;
                DisconnectButton1.Visibility = Visibility.Collapsed;

                // ⭐ 統一更新 UI（重點）
                UpdateStatusUI();
            }
            catch (Exception ex)
            {
                StreamStatusText.Text = $"攝像頭 1 - 斷開失敗: {ex.Message}";
                StreamStatusText.Foreground = Brushes.Red;
                StatusDot.Fill = Brushes.Red;
            }
        }

        private void StopStream2()
        {
            try
            {
                if (_mediaPlayer2 != null && _mediaPlayer2.IsPlaying)
                {
                    _mediaPlayer2.Stop();
                    System.Threading.Thread.Sleep(200);
                }

                _media2?.Dispose();
                _media2 = null;

                _stream2Connected = false;

                UpdateControlButtons();
                UpdateStatusUI();
            }
            catch (Exception ex)
            {
                StreamStatusText.Text = $"攝像頭 2 - 斷開失敗: {ex.Message}";
                StreamStatusText.Foreground = Brushes.Red;
                StatusDot.Fill = Brushes.Red;
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
            if (_stream2Connected)
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
            if (_stream1Connected)
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
