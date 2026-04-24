using LibVLCSharp.Shared;
using SANJET.Core.Services;
using System;
using System.Windows;
using LibVlcMedia = LibVLCSharp.Shared.Media;
using LibVlcMediaPlayer = LibVLCSharp.Shared.MediaPlayer;

namespace SANJET.UI.Views.Windows
{
    public partial class RtspStreamWindow : Window
    {
        private LibVLC? _libVlc;
        private LibVlcMediaPlayer? _mediaPlayer;
        private LibVlcMedia? _media;

        public RtspStreamWindow()
        {
            InitializeComponent();
            Loaded += RtspStreamWindow_Loaded;
            Closed += RtspStreamWindow_Closed;
        }

        private void RtspStreamWindow_Loaded(object sender, RoutedEventArgs e)
        {
            var settings = RtspStreamSettings.Load();
            string rtspUrl = settings.BuildRtspUrl();

            LibVLCSharp.Shared.Core.Initialize();
            _libVlc = new LibVLC("--network-caching=300", "--rtsp-tcp");
            _mediaPlayer = new LibVlcMediaPlayer(_libVlc);
            RtspVideoView.MediaPlayer = _mediaPlayer;

            try
            {
                _media = new LibVlcMedia(_libVlc, new Uri(rtspUrl));
                _mediaPlayer.Play(_media);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"RTSP 串流啟動失敗。\nURL: {rtspUrl}\n錯誤: {ex.Message}",
                    "RTSP 錯誤",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                Close();
            }
        }

        private void RtspStreamWindow_Closed(object? sender, EventArgs e)
        {
            if (_mediaPlayer?.IsPlaying == true)
            {
                _mediaPlayer.Stop();
            }

            RtspVideoView.MediaPlayer = null;
            _media?.Dispose();
            _mediaPlayer?.Dispose();
            _libVlc?.Dispose();
            _media = null;
            _mediaPlayer = null;
            _libVlc = null;
        }
    }
}
