
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using SANJET.Core.Interfaces;
using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;

namespace SANJET.Core.ViewModels
{
    public partial class SettingsPageViewModel : ObservableObject
    {
        private readonly ILogger<SettingsPageViewModel> _logger;
        private readonly IDatabaseManagementService _dbManagementService;
        private readonly string _rtspSettingsPath;

        [ObservableProperty]
        private string _pageTitle = "應用程式設定";

        [ObservableProperty]
        private string _rtspIpAddress = "192.168.70.90";

        [ObservableProperty]
        private string _rtspUsername = "SANJET";

        [ObservableProperty]
        private string _rtspPassword = "Sanjet25653819";

        [ObservableProperty]
        private int _rtspPort = 554;

        [ObservableProperty]
        private string _rtspStreamPath = "stream1";

        public SettingsPageViewModel(ILogger<SettingsPageViewModel> logger, IDatabaseManagementService dbManagementService)
        {
            _logger = logger;
            _dbManagementService = dbManagementService;
            _rtspSettingsPath = Path.Combine(AppContext.BaseDirectory, "rtsp.settings.json");
            _logger.LogInformation("SettingsViewModel 已初始化。");
        }

        public void LoadSettings()
        {
            _logger.LogInformation("正在加載設定值...");

            try
            {
                if (!File.Exists(_rtspSettingsPath))
                {
                    _logger.LogInformation("RTSP 設定檔不存在，使用預設值。路徑: {Path}", _rtspSettingsPath);
                    return;
                }

                var json = File.ReadAllText(_rtspSettingsPath);
                var settings = JsonSerializer.Deserialize<RtspSettingsModel>(json);

                if (settings == null)
                {
                    _logger.LogWarning("RTSP 設定檔格式錯誤，使用預設值。路徑: {Path}", _rtspSettingsPath);
                    return;
                }

                RtspIpAddress = settings.IpAddress;
                RtspUsername = settings.Username;
                RtspPassword = settings.Password;
                RtspPort = settings.Port;
                RtspStreamPath = settings.StreamPath;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "加載 RTSP 設定失敗。路徑: {Path}", _rtspSettingsPath);
                MessageBox.Show($"加載 RTSP 設定失敗：{ex.Message}", "設定錯誤", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        public string BuildRtspUrl()
        {
            var ip = RtspIpAddress?.Trim();
            var user = RtspUsername?.Trim();
            var pass = RtspPassword ?? string.Empty;
            var streamPath = (RtspStreamPath ?? string.Empty).Trim().TrimStart('/');

            if (string.IsNullOrWhiteSpace(ip))
            {
                throw new InvalidOperationException("請先設定 RTSP IP 位址。");
            }

            var authPart = string.IsNullOrWhiteSpace(user)
                ? string.Empty
                : $"{Uri.EscapeDataString(user)}:{Uri.EscapeDataString(pass)}@";

            return string.IsNullOrWhiteSpace(streamPath)
                ? $"rtsp://{authPart}{ip}:{RtspPort}"
                : $"rtsp://{authPart}{ip}:{RtspPort}/{streamPath}";
        }

        [RelayCommand]
        private void SaveRtspSettings()
        {
            try
            {
                var settings = new RtspSettingsModel
                {
                    IpAddress = RtspIpAddress.Trim(),
                    Username = RtspUsername.Trim(),
                    Password = RtspPassword,
                    Port = RtspPort,
                    StreamPath = RtspStreamPath.Trim().TrimStart('/')
                };

                var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_rtspSettingsPath, json);

                _logger.LogInformation("RTSP 設定已儲存。路徑: {Path}", _rtspSettingsPath);
                MessageBox.Show("RTSP 設定已儲存。", "儲存成功", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "儲存 RTSP 設定失敗。路徑: {Path}", _rtspSettingsPath);
                MessageBox.Show($"儲存 RTSP 設定失敗：{ex.Message}", "儲存失敗", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private async Task BackupDatabaseAsync()
        {
            var saveFileDialog = new SaveFileDialog
            {
                Filter = "資料庫備份檔案 (*.db)|*.db|所有檔案 (*.*)|*.*",
                Title = "選擇備份路徑",
                FileName = $"SNAJET_backup_{DateTime.Now:yyyyMMdd_HHmmss}.db"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                string destinationPath = saveFileDialog.FileName;
                _logger.LogInformation("使用者選擇備份路徑: {Path}", destinationPath);
                bool success = await _dbManagementService.BackupDatabaseAsync(destinationPath);
                if (success)
                {
                    MessageBox.Show($"資料庫已成功備份至:\n{destinationPath}", "備份成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                // 失敗的訊息由服務層顯示
            }
        }

        [RelayCommand]
        private async Task RestoreDatabaseAsync()
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "資料庫備份檔案 (*.db)|*.db|所有檔案 (*.*)|*.*",
                Title = "選擇要還原的備份檔案"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                string sourcePath = openFileDialog.FileName;
                var result = MessageBox.Show(
                    "警告：此操作將會用選擇的備份檔案覆蓋目前的資料庫。\n\n所有未備份的變更都將遺失，且應用程式將會重新啟動。\n\n確定要繼續嗎？",
                    "確認還原",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    _logger.LogInformation("使用者確認從 '{Path}' 還原資料庫。", sourcePath);
                    await _dbManagementService.RestoreDatabaseAsync(sourcePath);
                    // 成功還原後，服務會處理重啟邏輯，此處不需再做操作
                }
            }
        }

        private class RtspSettingsModel
        {
            public string IpAddress { get; set; } = "192.168.70.90";
            public string Username { get; set; } = "SANJET";
            public string Password { get; set; } = "Sanjet25653819";
            public int Port { get; set; } = 554;
            public string StreamPath { get; set; } = "stream1";
        }
    }
}
