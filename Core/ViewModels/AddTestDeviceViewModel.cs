using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Windows;

namespace SANJET.Core.ViewModels
{
    public partial class AddTestDeviceViewModel : ObservableObject
    {
        private readonly HomeViewModel _homeViewModel;
        private readonly ILogger<AddTestDeviceViewModel> _logger;

        [ObservableProperty]
        private string deviceName = string.Empty;

        [ObservableProperty]
        private string esp32MqttId = "ESP32_TEST_RS485";

        [ObservableProperty]
        private int slaveId = 1;

        [ObservableProperty]
        private int modbusDeviceIndex = 1;

        [ObservableProperty]
        private string statusMessage = string.Empty;

        public AddTestDeviceViewModel(HomeViewModel homeViewModel, ILogger<AddTestDeviceViewModel> logger)
        {
            _homeViewModel = homeViewModel;
            _logger = logger;
        }

        [RelayCommand]
        private async Task AddDeviceAsync()
        {
            // 驗證輸入
            if (string.IsNullOrWhiteSpace(DeviceName))
            {
                StatusMessage = "設備名稱不能為空";
                MessageBox.Show("設備名稱不能為空", "驗證錯誤", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(Esp32MqttId))
            {
                StatusMessage = "ESP32 MQTT ID 不能為空";
                MessageBox.Show("ESP32 MQTT ID 不能為空", "驗證錯誤", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (SlaveId < 1 || SlaveId > 247)
            {
                StatusMessage = "Slave ID 必須在 1-247 之間";
                MessageBox.Show("Slave ID 必須在 1-247 之間", "驗證錯誤", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (ModbusDeviceIndex < 1 || ModbusDeviceIndex > 4)
            {
                StatusMessage = "測試區設備編號必須在 1-4 之間";
                MessageBox.Show("測試區第一站支援設備編號 1-4，分別對應控制 0-3、狀態 10-13、次數 20 開始。", "驗證錯誤", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                StatusMessage = "正在添加設備...";
                bool success = await _homeViewModel.AddTestAreaDeviceAsync(DeviceName, Esp32MqttId, (byte)SlaveId, ModbusDeviceIndex);

                if (success)
                {
                    StatusMessage = $"成功添加設備：{DeviceName}";
                    _logger.LogInformation("成功通過對話框添加測試區設備：{DeviceName} (ESP32: {Esp32Id}, Slave: {SlaveId}, 設備編號: {ModbusDeviceIndex})",
                                         DeviceName, Esp32MqttId, SlaveId, ModbusDeviceIndex);

                    // 關閉對話框
                    Application.Current.Windows.OfType<Window>()
                        .FirstOrDefault(w => w.DataContext == this)?.Close();
                }
                else
                {
                    StatusMessage = "添加設備失敗，請檢查輸入或日誌";
                    MessageBox.Show("添加設備失敗，可能是重複的設備或其他錯誤。請檢查日誌獲取詳細信息。", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"錯誤：{ex.Message}";
                _logger.LogError(ex, "通過對話框添加測試區設備時發生錯誤");
                MessageBox.Show($"添加設備時發生錯誤：{ex.Message}", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private void Cancel()
        {
            // 關閉對話框
            Application.Current.Windows.OfType<Window>()
                .FirstOrDefault(w => w.DataContext == this)?.Close();
        }
    }
}
