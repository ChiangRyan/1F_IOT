using LibVLCSharp.Shared;
using SANJET.Core.Services;
using System;
using System.Windows;
using System.Windows.Media;
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
            // ✅ 如果已經初始化過，就不重複執行
            if (_libVlc != null) return;

            try
            {
                var settings = RtspStreamSettings.Load();
                string rtspUrl = settings.BuildRtspUrl();

                // 初始化 LibVLC 核心
                LibVLCSharp.Shared.Core.Initialize();

                // ✅ 修正：移除 --rtsp-tcp，只用基本參數
                _libVlc = new LibVLC("--network-caching=200");
                _mediaPlayer = new LibVlcMediaPlayer(_libVlc);
                
                // ✅ 先綁定 UI 控制項
                RtspVideoView.MediaPlayer = _mediaPlayer;

                // ✅ 【關鍵修正】：不使用 using，保持 media 物件生存期
                _media = new LibVlcMedia(_libVlc, new Uri(rtspUrl));
                
                // 啟動播放
                _mediaPlayer.Play(_media);
                
                Title = "RTSP 串流 - 已連接";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"無法開啟串流：{ex.Message}", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
                Close();
            }
        }

        private void RtspStreamWindow_Closed(object? sender, EventArgs e)
        {
            DisposePlayer();
        }

        private void StopPlayback()
        {
            if (_mediaPlayer?.IsPlaying == true)
            {
                _mediaPlayer.Stop();
            }
        }

        private void DisposePlayer()
        {
            StopPlayback();

            // ✅ 解除 UI 綁定
            if (RtspVideoView != null)
            {
                RtspVideoView.MediaPlayer = null;
            }

            // ✅ 釋放媒體資源
            _media?.Dispose();
            _mediaPlayer?.Dispose();
            _libVlc?.Dispose();

            _media = null;
            _mediaPlayer = null;
            _libVlc = null;
        }
    }
}
