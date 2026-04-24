using LibVLCSharp.Shared;
using Microsoft.Extensions.DependencyInjection;
using SANJET.Core.ViewModels;
using System;
using System.Windows;
using System.Windows.Media;

namespace SANJET.UI.Views.Windows
{
    public partial class StreamWindow : Window
    {
        private LibVLC? _libVLC;
        private LibVLCSharp.Shared.MediaPlayer? _mediaPlayer;
        private Media? _media;
        private SettingsPageViewModel? _viewModel;

        public StreamWindow(SettingsPageViewModel viewModel)
        {
            InitializeComponent();

            _viewModel = viewModel;
            DataContext = _viewModel;

            LibVLCSharp.Shared.Core.Initialize();

            _libVLC = new LibVLC(
                "--rtsp-tcp",
                "--network-caching=300"
            );

            _mediaPlayer = new LibVLCSharp.Shared.MediaPlayer(_libVLC);
            RtspVideoView.MediaPlayer = _mediaPlayer;

            Loaded += StreamWindow_Loaded;
            Unloaded += StreamWindow_Unloaded;
        }

        private void StreamWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                _viewModel = DataContext as SettingsPageViewModel;
                if (_viewModel != null)
                {
                    _viewModel.SetAutoStartStreamAction(StartStream);
                    StartStream();
                }
            }
            catch (Exception ex)
            {
                StreamStatusText.Text = $"初始化失敗: {ex.Message}";
                StreamStatusText.Foreground = Brushes.Red;
                StreamStatusText.Visibility = Visibility.Visible;
            }
        }

        private void StreamWindow_Unloaded(object sender, RoutedEventArgs e)
        {
            StopStream();

            RtspVideoView.MediaPlayer = null;

            _mediaPlayer?.Dispose();
            _mediaPlayer = null;

            _libVLC?.Dispose();
            _libVLC = null;
        }

        private void StartStream()
        {
            try
            {
                if (_libVLC == null || _mediaPlayer == null || _viewModel == null)
                    return;

                StopStream();

                StreamStatusText.Text = "連接中...";
                StreamStatusText.Foreground = Brushes.White;
                StreamStatusText.Visibility = Visibility.Visible;

                var rtspUrl = _viewModel.BuildRtspUrl();
                _media = new Media(_libVLC, new Uri(rtspUrl));
                _media.AddOption(":rtsp-tcp");
                _media.AddOption(":network-caching=300");

                _mediaPlayer.Play(_media);

                StreamStatusText.Text = "已連接";
                StreamStatusText.Foreground = Brushes.LimeGreen;
                StreamStatusText.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                StreamStatusText.Text = $"連接失敗: {ex.Message}";
                StreamStatusText.Foreground = Brushes.Red;
                StreamStatusText.Visibility = Visibility.Visible;
            }
        }

        private void StopStream()
        {
            try
            {
                if (_mediaPlayer?.IsPlaying == true)
                {
                    _mediaPlayer.Stop();
                }

                _media?.Dispose();
                _media = null;

                StreamStatusText.Text = "已斷開連接";
                StreamStatusText.Foreground = Brushes.Gray;
                StreamStatusText.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                StreamStatusText.Text = $"斷開失敗: {ex.Message}";
                StreamStatusText.Foreground = Brushes.Red;
                StreamStatusText.Visibility = Visibility.Visible;
            }
        }

        private void DisconnectStream_Click(object sender, RoutedEventArgs e)
        {
            StopStream();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
