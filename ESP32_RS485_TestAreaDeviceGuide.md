# ESP32 RS485 測試區設備添加完整指南

## 概述
本指南描述如何在測試區中添加 ESP32 設備，通過 RS485 通訊協議讀取 Modbus 從站設備的資訊。

## 系統架構

```
┌─────────────────────────────────────────────────────────────┐
│                        WPF 應用程式                          │
│                                                              │
│  ┌──────────────────────────────────────────────────────┐  │
│  │             HomeViewModel (首頁視圖模型)              │  │
│  │                                                      │  │
│  │  • DisplayAreaDevices (展示區設備)                   │  │
│  │  • TestAreaDevices    (測試區設備)                   │  │
│  │  • AddTestAreaDeviceAsync()  - 添加設備方法          │  │
│  │  • RemoveTestAreaDeviceAsync() - 移除設備方法        │  │
│  │  • OpenAddTestDeviceDialog() - 打開添加對話框        │  │
│  └──────────────────────────────────────────────────────┘  │
│           ↑                                    ↑             │
│           │                                    │             │
│  ┌────────┴──────────────────────────────────┴──────────┐  │
│  │        AddTestDeviceViewModel (對話框)               │  │
│  │  • DeviceName - 設備名稱                             │  │
│  │  • Esp32MqttId - ESP32 MQTT ID                      │  │
│  │  • SlaveId - Modbus 從站 ID                         │  │
│  │  • StatusMessage - 狀態消息                         │  │
│  └──────────────────────────────────────────────────────┘  │
│           ↓                                                  │
│  ┌──────────────────────────────────────────────────────┐  │
│  │            MQTT 服務 (MqttService)                  │  │
│  │  • 發送 Modbus 讀寫命令到 ESP32                      │  │
│  │  • 監聽 ESP32 的回應 (讀/寫狀態)                     │  │
│  └──────────────────────────────────────────────────────┘  │
│           ↓                                                  │
│  ┌──────────────────────────────────────────────────────┐  │
│  │           數據庫 (AppDbContext)                      │  │
│  │  • Device 表存儲設備信息                             │  │
│  │  • 字段: Id, Name, SlaveId, ControllingEsp32MqttId │  │
│  │        Status, IsOperational, RunCount             │  │
│  └──────────────────────────────────────────────────────┘  │
│           ↓                                                  │
│  ┌──────────────────────────────────────────────────────┐  │
│  │    MQTT Broker (MqttBrokerService)                  │  │
│  │  • 接收來自 ESP32 的 Modbus 回應                     │  │
│  │  • 主題格式: devices/{Esp32Id}/modbus/read|write/   │  │
│  └──────────────────────────────────────────────────────┘  │
│           ↓                                                  │
│  ┌──────────────────────────────────────────────────────┐  │
│  │            ESP32 設備 (MCU)                          │  │
│  │  • RS485 主機 - Modbus Master                        │  │
│  │  • 最多支持 5 個 RS485 從站                          │  │
│  │  • 通過 MQTT 與應用程式通訊                          │  │
│  └──────────────────────────────────────────────────────┘  │
│           ↓                                                  │
│  ┌──────────────────────────────────────────────────────┐  │
│  │        RS485 網絡 (Modbus 協議)                      │  │
│  │  ┌──────────┐ ┌──────────┐ ┌──────────┐            │  │
│  │  │ 站 ID:1  │ │ 站 ID:2  │ │ 站 ID:3  │ ...        │  │
│  │  │ (Slave1) │ │ (Slave2) │ │ (Slave3) │            │  │
│  │  └──────────┘ └──────────┘ └──────────┘            │  │
│  └──────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────┘
```

## 核心功能實現

### 1. 添加測試區設備

#### HomeViewModel 中的方法：

```csharp
/// <summary>
/// 在測試區添加新的 ESP32 RS485 設備
/// </summary>
/// <param name="deviceName">設備名稱</param>
/// <param name="esp32MqttId">ESP32 的 MQTT ID (例如: ESP32_TEST_RS485)</param>
/// <param name="slaveId">Modbus Slave ID (1-247)</param>
/// <returns>成功則回傳 true</returns>
public async Task<bool> AddTestAreaDeviceAsync(string deviceName, string esp32MqttId, byte slaveId)
{
    // 驗證輸入
    // 檢查重複的 ESP32 + Slave ID 組合
    // 創建新設備並保存到數據庫
    // 重新載入設備列表
}

/// <summary>
/// 從測試區移除設備
/// </summary>
public async Task<bool> RemoveTestAreaDeviceAsync(int deviceId)
{
    // 從數據庫刪除
    // 從 UI 集合中移除
}
```

### 2. UI 對話框

#### AddTestDeviceWindow.xaml
- 設備名稱輸入框
- ESP32 MQTT ID 輸入框
- Modbus Slave ID 輸入框（建議：100-110）
- RS485 配置信息提示
- 添加/取消按鈕

### 3. 數據流程

#### 添加設備流程：
```
用戶點擊"添加設備" 
    ↓
打開 AddTestDeviceWindow 對話框
    ↓
用戶輸入設備信息
    ↓
驗證輸入（名稱、ESP32 ID、Slave ID）
    ↓
調用 HomeViewModel.AddTestAreaDeviceAsync()
    ↓
檢查是否存在相同的 ESP32 + Slave ID 組合
    ↓
將新設備添加到數據庫
    ↓
重新載入設備列表
    ↓
關閉對話框
    ↓
UI 显示新設備
```

#### Modbus 數據讀取流程：
```
應用程式定期輪詢
    ↓
發送 Modbus Read Request via MQTT
  主題: devices/{Esp32Id}/modbus/read/request
  負載: {slaveId, address, quantity, functionCode}
    ↓
ESP32 接收請求
    ↓
ESP32 通過 RS485 對從站設備進行 Modbus RTU 讀取
    ↓
ESP32 接收從站回應
    ↓
ESP32 發送讀取結果回應 via MQTT
  主題: devices/{Esp32Id}/modbus/read/response
  負載: {deviceId, slaveId, status, data}
    ↓
應用程式接收回應
    ↓
解析數據並更新數據庫和 UI
    ↓
顯示設備狀態（運行中/閒置/故障等）
```

## 配置指南

### 1. Slave ID 建議分配

```
展示區設備 (DisplayAreaDevices):
  • Slave ID 1-50: 常用範圍
  • 例如：1, 2, 3 (當前展示區設備)

測試區設備 (TestAreaDevices):
  • Slave ID 100-247: 測試設備推薦
  • 支持無限制數量（Modbus 最多 247 個從站）
  • 避免與展示區衝突
```

### 2. ESP32 MQTT ID 命名規則

```
展示區設備:
  ControllingEsp32MqttId: "ESP32_RS485"

測試區設備:
  ControllingEsp32MqttId: "ESP32_TEST_RS485" (推薦)
  或自定義: "ESP32_Custom", "ESP32_Lab", 等

備注: 單個 ESP32 可控制 1-247 個 Modbus 從站，無數量限制
      （受硬件資源和網絡帶寬限制）
```

### 3. 數據庫 Device 表結構

```csharp
public class Device
{
    public int Id { get; set; }                          // 主鍵
    public string Name { get; set; }                     // 設備名稱
    public int SlaveId { get; set; }                     // Modbus Slave ID
    public string Status { get; set; }                   // 狀態: 閒置/運行中/故障/通訊失敗
    public bool IsOperational { get; set; }              // 是否啟用
    public int RunCount { get; set; }                    // 運轉次數
    public string? ControllingEsp32MqttId { get; set; }  // 控制此設備的 ESP32 ID
    public DateTime Timestamp { get; set; }              // 最後更新時間
}
```

## RS485 / Modbus 通訊細節

### 1. Modbus 基本信息

```
協議: Modbus RTU (Binary) 或 Modbus TCP
波特率: 9600 bps (典型)
校驗: CRC-16
超時: 2-5 秒
最大從站數: 247 (Slave ID: 1-247)
```

### 2. 支持的 Modbus 功能碼

```
功能碼 03: 讀保持寄存器 (Read Holding Registers)
功能碼 06: 寫單個寄存器 (Write Single Register)
功能碼 16: 寫多個寄存器 (Write Multiple Registers)
```

### 3. MQTT 消息格式

#### 讀取請求：
```json
{
  "slaveId": 100,
  "address": 0,
  "quantity": 1,
  "functionCode": 3
}
主題: devices/ESP32_TEST_RS485/modbus/read/request
```

注: Slave ID 支援 1-247 任意從站，無限制。

#### 讀取回應：
```json
{
  "deviceId": "ESP32_TEST_RS485",
  "slaveId": 100,
  "status": "success",
  "data": [0, 0, 1, 2, 3],
  "address": 0,
  "quantity": 1,
  "functionCode": 3
}
主題: devices/ESP32_TEST_RS485/modbus/read/response
```

#### 寫入請求：
```json
{
  "slaveId": 100,
  "address": 0,
  "value": 1
}
主題: devices/ESP32_TEST_RS485/modbus/write/request
```

#### 寫入回應：
```json
{
  "deviceId": "ESP32_TEST_RS485",
  "slaveId": 100,
  "status": "success",
  "message": "Write operation completed"
}
主題: devices/ESP32_TEST_RS485/modbus/write/response
```

## 實現的新文件

### 1. Core\ViewModels\AddTestDeviceViewModel.cs
- 對話框的業務邏輯
- 輸入驗證
- 與 HomeViewModel 的交互

### 2. UI\Views\Windows\AddTestDeviceWindow.xaml
- 對話框的 UI 佈局
- 表單字段和按鈕

### 3. UI\Views\Windows\AddTestDeviceWindow.xaml.cs
- 對話框的代碼後置

## 修改的現有文件

### 1. Core\ViewModels\HomeViewModel.cs
- 添加 `AddTestAreaDeviceAsync()` 方法
- 添加 `RemoveTestAreaDeviceAsync()` 方法
- 添加 `OpenAddTestDeviceDialogCommand` 命令

### 2. UI\Views\Pages\HomePage.xaml
- 在測試區添加"添加設備"按鈕
- 綁定到 `OpenAddTestDeviceDialogCommand`

### 3. UI\Converters\Converters.cs
- 添加 `StringToVisibilityConverter` 用於條件顯示

### 4. App.xaml.cs
- 註冊 `AddTestDeviceViewModel` 和 `AddTestDeviceWindow`

### 5. App.xaml
- 註冊 `StringToVisibilityConverter`

## 使用指南

### 添加測試區設備步驟：

1. **進入測試區**
   - 在首頁點擊"測試區"按鈕
   - 看到"此區域尚未配置設備"消息

2. **打開添加設備對話框**
   - 點擊"添加設備"按鈕（藍色，在頂部工具欄）

3. **填寫設備信息**
   - 設備名稱: 例如 "測試馬達1"
   - ESP32 MQTT ID: "ESP32_TEST_RS485"
   - Slave ID: 100-110 範圍內選擇

4. **驗證和添加**
   - 系統會驗證所有輸入
   - 點擊"添加"按鈕
   - 設備會立即出現在測試區

5. **使用設備**
   - 啟動/停止設備
   - 編輯設備名稱
   - 查看和編輯運轉次數
   - 查看設備狀態

### 移除設備（代碼示例）

```csharp
// 在 HomeViewModel 中
var success = await RemoveTestAreaDeviceAsync(deviceId);
```

## 錯誤處理

### 常見錯誤場景：

1. **重複的 ESP32 + Slave ID 組合**
   - 錯誤消息: "已存在相同的設備組合"
   - 解決: 使用不同的 Slave ID

2. **無效的 Slave ID**
   - 錯誤消息: "Slave ID 必須在 1-247 之間"
   - 解決: 使用 1-247 範圍內的 ID

3. **ESP32 未連接**
   - 設備狀態: "通訊失敗"
   - 解決: 檢查 ESP32 是否在線、MQTT 連接狀態

4. **RS485 從站無回應**
   - 設備狀態: "通訊失敗"
   - 解決: 檢查從站設備、RS485 線路、波特率設置

## 日誌和調試

### 重要日誌消息：

```
成功添加測試區設備：{Name} (ESP32: {Esp32Id}, Slave: {SlaveId})
ESP32 {Esp32Id}, Slave {SlaveId} - 狀態從 '{OldStatus}' 變為 '{NewStatus}'
Modbus 讀取失敗 (ESP32: {Esp32Id}, Slave: {SlaveId}): {Message}
已發送 Modbus Write 命令到 {Topic}
```

### 監控建議：

1. 定期檢查日誌文件中的錯誤
2. 監控 MQTT 消息流量
3. 檢查數據庫中設備的時間戳
4. 驗證 ESP32 的連接狀態

## 最佳實踐

### 1. 設備命名
```
✓ 好的命名: "測試馬達1", "實驗傳感器", "新型控制器"
✗ 不好的命名: "設備1", "測試", "xxx"
```

### 2. Slave ID 分配
```
✓ 組織分明: 展示區 1-50, 測試區 100-110
✗ 混亂分配: 隨意分配，容易產生衝突
```

### 3. ESP32 管理
```
✓ 使用有意義的 ID: "ESP32_RS485", "ESP32_TEST_RS485"
✗ 通用 ID: "ESP32", "DEVICE"
```

### 4. 錯誤處理
```
✓ 始終檢查返回值
✓ 提供用戶友好的錯誤消息
✗ 忽略錯誤條件
```

## 擴展功能建議

### 1. 批量導入設備
```csharp
public async Task<bool> ImportTestDevicesFromCsvAsync(string filePath)
{
    // 從 CSV 文件讀取設備列表並批量添加
}
```

### 2. 設備模板
```csharp
public static class DeviceTemplates
{
    public static readonly (string name, byte slaveId)[] TestDevices = new[]
    {
        ("測試設備1", 100),
        ("測試設備2", 101),
        ("測試設備3", 102),
    };
}
```

### 3. 設備診斷工具
```csharp
public async Task<DiagnosticReport> DiagnoseDeviceAsync(int deviceId)
{
    // 測試 MQTT 連接、RS485 通訊、設備響應時間
}
```

### 4. 批量控制
```csharp
public async Task<bool> ControlAllTestDevicesAsync(string command)
{
    // 對所有測試區設備執行相同命令
}
```

## 總結

這個實現提供了完整的測試區設備管理功能，包括：

✓ 動態添加 ESP32 RS485 設備（無數量限制）
✓ Modbus 從站支持（最多 247 個，Modbus 標準上限）
✓ 數據庫持久化
✓ 實時 MQTT 通訊
✓ 用戶友好的 UI 對話框
✓ 完整的錯誤處理
✓ 詳細的日誌記錄

系統可以輕鬆擴展以支持更多功能和設備類型。
