using LibVLCSharp.Shared;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SANJET.UI.Views.Pages
{
    public partial class SettingsPage : Page
    {
        private const string RtspUrl = "rtsp://SANJET:Sanjet25653819@192.168.70.90:554/stream1";

        private LibVLC? _libVLC;
        private LibVLCSharp.Shared.MediaPlayer? _mediaPlayer;
        private Media? _media;

        public SettingsPage()
        {
            InitializeComponent();

            LibVLCSharp.Shared.Core.Initialize();

            _libVLC = new LibVLC(
                "--rtsp-tcp",
                "--network-caching=300"
            );

            _mediaPlayer = new LibVLCSharp.Shared.MediaPlayer(_libVLC);
            RtspVideoView.MediaPlayer = _mediaPlayer;

            Unloaded += SettingsPage_Unloaded;
        }

        private void SettingsPage_Unloaded(object sender, RoutedEventArgs e)
        {
            StopStream();

            RtspVideoView.MediaPlayer = null;

            _mediaPlayer?.Dispose();
            _mediaPlayer = null;

            _libVLC?.Dispose();
            _libVLC = null;
        }

        private void ConnectRtspStream_Click(object sender, RoutedEventArgs e)
        {
            StartStream();
        }

        private void DisconnectRtspStream_Click(object sender, RoutedEventArgs e)
        {
            StopStream();
        }

        private void StartStream()
        {
            try
            {
                if (_libVLC == null || _mediaPlayer == null)
                    return;

                StopStream();

                StreamStatusText.Text = "連接中...";
                StreamStatusText.Foreground = Brushes.White;
                StreamStatusText.Visibility = Visibility.Visible;

                _media = new Media(_libVLC, new Uri(RtspUrl));
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
    }
}