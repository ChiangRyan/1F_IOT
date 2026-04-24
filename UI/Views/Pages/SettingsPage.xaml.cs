using SANJET.Core.Services;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SANJET.UI.Views.Pages
{
    public partial class SettingsPage : Page
    {
        public SettingsPage()
        {
            InitializeComponent();
            Loaded += SettingsPage_Loaded;
        }

        private void SettingsPage_Loaded(object sender, RoutedEventArgs e)
        {
            var settings = RtspStreamSettings.Load();
            IpAddressTextBox.Text = settings.IpAddress;
            PortTextBox.Text = settings.Port.ToString();
            UsernameTextBox.Text = settings.Username;
            PasswordBox.Password = settings.Password;
            PathTextBox.Text = settings.StreamPath;

            RtspStatusTextBlock.Text = "狀態：已載入 RTSP 設定";
            RtspStatusTextBlock.Foreground = Brushes.DarkGreen;
        }

        private void SaveRtspSettingsButton_Click(object sender, RoutedEventArgs e)
        {
            if (!int.TryParse(PortTextBox.Text?.Trim(), out int port) || port <= 0 || port > 65535)
            {
                RtspStatusTextBlock.Text = "狀態：連接埠格式錯誤，請輸入 1-65535";
                RtspStatusTextBlock.Foreground = Brushes.OrangeRed;
                return;
            }

            if (string.IsNullOrWhiteSpace(IpAddressTextBox.Text))
            {
                RtspStatusTextBlock.Text = "狀態：請輸入 IP 位址";
                RtspStatusTextBlock.Foreground = Brushes.OrangeRed;
                return;
            }

            try
            {
                var settings = new RtspStreamSettings
                {
                    IpAddress = IpAddressTextBox.Text.Trim(),
                    Port = port,
                    Username = UsernameTextBox.Text?.Trim() ?? string.Empty,
                    Password = PasswordBox.Password ?? string.Empty,
                    StreamPath = PathTextBox.Text?.Trim() ?? string.Empty
                };

                settings.Save();
                RtspStatusTextBlock.Text = $"狀態：已儲存 ({DateTime.Now:HH:mm:ss})";
                RtspStatusTextBlock.Foreground = Brushes.DodgerBlue;
            }
            catch (Exception ex)
            {
                RtspStatusTextBlock.Text = $"狀態：儲存失敗 - {ex.Message}";
                RtspStatusTextBlock.Foreground = Brushes.Red;
            }
        }
    }
}
