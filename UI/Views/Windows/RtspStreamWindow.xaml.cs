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
                using var media = new LibVlcMedia(_libVlc, new Uri(rtspUrl));
                _mediaPlayer.Play(media);
            }
            catch
            {
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
            _mediaPlayer?.Dispose();
            _libVlc?.Dispose();
            _mediaPlayer = null;
            _libVlc = null;
        }
    }
}
