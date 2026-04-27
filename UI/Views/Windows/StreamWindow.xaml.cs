using LibVLCSharp.Shared;
using Microsoft.Extensions.DependencyInjection;
using SANJET.Core.Services;
using SANJET.Core.ViewModels;
using System;
using System.Windows;
using System.Windows.Media;

namespace SANJET.UI.Views.Windows
{
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
                // LibVLC 已在應用啟動時預先初始化，無需再次預熱
            }
            catch (Exception ex)
            {
                StreamStatusText1.Text = $"初始化失敗: {ex.Message}";
                StreamStatusText1.Foreground = Brushes.Red;
                StreamStatusText1.Visibility = Visibility.Visible;
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

        private void StartStream1()
        {
            try
            {
                if (_libVLC == null || _mediaPlayer1 == null || _viewModel == null)
                    return;

                if (_stream1Connected)
                    return;

                StreamStatusText1.Text = "攝像頭 1 - 連接中...";
                StreamStatusText1.Foreground = Brushes.White;
                StreamStatusText1.Visibility = Visibility.Visible;

                var rtspUrl = _viewModel.BuildRtspUrl1();

                // Stop any existing stream first
                if (_mediaPlayer1.IsPlaying)
                {
                    _mediaPlayer1.Stop();
                    System.Threading.Thread.Sleep(100);
                }

                // Clean up old media
                _media1?.Dispose();
                _media1 = null;

                _media1 = new Media(_libVLC, new Uri(rtspUrl));
                _media1.AddOption(":rtsp-tcp");
                _media1.AddOption(":network-caching=300");

                _mediaPlayer1.Play(_media1);

                StreamStatusText1.Text = "攝像頭 1 - 已連接";
                StreamStatusText1.Foreground = Brushes.LimeGreen;
                StreamStatusText1.Visibility = Visibility.Collapsed;

                _stream1Connected = true;
                ConnectButton1.Visibility = Visibility.Collapsed;
                DisconnectButton1.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                StreamStatusText1.Text = $"攝像頭 1 - 連接失敗: {ex.Message}";
                StreamStatusText1.Foreground = Brushes.Red;
                StreamStatusText1.Visibility = Visibility.Visible;
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

                StreamStatusText2.Text = "攝像頭 2 - 連接中...";
                StreamStatusText2.Foreground = Brushes.White;
                StreamStatusText2.Visibility = Visibility.Visible;

                var rtspUrl = _viewModel.BuildRtspUrl2();

                // Stop any existing stream first
                if (_mediaPlayer2.IsPlaying)
                {
                    _mediaPlayer2.Stop();
                    System.Threading.Thread.Sleep(100);
                }

                // Clean up old media
                _media2?.Dispose();
                _media2 = null;

                _media2 = new Media(_libVLC, new Uri(rtspUrl));
                _media2.AddOption(":rtsp-tcp");
                _media2.AddOption(":network-caching=300");

                _mediaPlayer2.Play(_media2);

                StreamStatusText2.Text = "攝像頭 2 - 已連接";
                StreamStatusText2.Foreground = Brushes.LimeGreen;
                StreamStatusText2.Visibility = Visibility.Collapsed;

                _stream2Connected = true;
                ConnectButton2.Visibility = Visibility.Collapsed;
                DisconnectButton2.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                StreamStatusText2.Text = $"攝像頭 2 - 連接失敗: {ex.Message}";
                StreamStatusText2.Foreground = Brushes.Red;
                StreamStatusText2.Visibility = Visibility.Visible;
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

                if (_media1 != null)
                {
                    _media1.Dispose();
                    _media1 = null;
                }

                StreamStatusText1.Text = "攝像頭 1 - 未連接";
                StreamStatusText1.Foreground = Brushes.Gray;
                StreamStatusText1.Visibility = Visibility.Visible;

                _stream1Connected = false;
                ConnectButton1.Visibility = Visibility.Visible;
                DisconnectButton1.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                StreamStatusText1.Text = $"攝像頭 1 - 斷開失敗: {ex.Message}";
                StreamStatusText1.Foreground = Brushes.Red;
                StreamStatusText1.Visibility = Visibility.Visible;
            }
        }

        private void StopStream2()
        {
            try
            {
                if (_mediaPlayer2 != null)
                {
                    if (_mediaPlayer2.IsPlaying)
                    {
                        _mediaPlayer2.Stop();
                        System.Threading.Thread.Sleep(200);
                    }
                }

                if (_media2 != null)
                {
                    _media2.Dispose();
                    _media2 = null;
                }

                StreamStatusText2.Text = "攝像頭 2 - 未連接";
                StreamStatusText2.Foreground = Brushes.Gray;
                StreamStatusText2.Visibility = Visibility.Visible;

                _stream2Connected = false;
                ConnectButton2.Visibility = Visibility.Visible;
                DisconnectButton2.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                StreamStatusText2.Text = $"攝像頭 2 - 斷開失敗: {ex.Message}";
                StreamStatusText2.Foreground = Brushes.Red;
                StreamStatusText2.Visibility = Visibility.Visible;
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

        private void Connect1_Click(object sender, RoutedEventArgs e)
        {
            StartStream1();
        }

        private void Connect2_Click(object sender, RoutedEventArgs e)
        {
            StartStream2();
        }

        private void Disconnect1_Click(object sender, RoutedEventArgs e)
        {
            StopStream1();
        }

        private void Disconnect2_Click(object sender, RoutedEventArgs e)
        {
            StopStream2();
        }
    }
}
