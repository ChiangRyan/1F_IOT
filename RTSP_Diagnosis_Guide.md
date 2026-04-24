# RTSP 串流診斷解決方案

## 🔴 當前問題分析

根據您提供的最新 VLC Debug 日誌：

```
VLC [Debug] live555: connection error 404
VLC [Error] live555: Failed to connect with rtsp://192.168.70.90:554
```

**問題確認：** 
- ✅ IP 連接成功（`192.168.70.90:554`）
- ✅ RTSP 協議支持
- ❌ **404 錯誤 = 找不到資源（流路徑無效）**

---

## 🚀 快速解決步驟

### 方案 1：使用圖形化 URL 測試工具（最快）

1. **在應用程序中打開「RTSP URL 快速測試」窗口**
   - 或直接在代碼中調用：`new RtspUrlTestWindow()`

2. **點擊「🔄 自動測試所有」按鈕**
   - 工具會自動測試 50+ 個常見的流路徑
   - 約 30-60 秒內完成測試

3. **查看測試結果**
   ```
   ✅ 找到的有效 URL：

   ✅ rtsp://SANJET:Sanjet25653819@192.168.70.90:554/stream2
   ✅ rtsp://192.168.70.90:554/stream2
   ```

4. **複製成功的 URL 並儲存**

### 方案 2：使用命令行診斷工具

```powershell
# 在項目根目錄執行
.\Tools\RtspQuickDiagnostic\bin\Debug\net8.0\RtspQuickDiagnostic.exe 192.168.70.90 SANJET Sanjet25653819 554
```

**輸出範例：**
```
✅ 找到的有效 URL：
  ✅ rtsp://SANJET:Sanjet25653819@192.168.70.90:554/stream2
  ✅ rtsp://192.168.70.90:554/stream2

建議：
請在應用程序設定中使用上述有效 URL
```

### 方案 3：使用 VLC 媒體播放器手動測試

1. 打開 VLC 媒體播放器
2. 菜單 → 媒體 → 打開位置
3. 輸入 URL 測試：
   ```
   rtsp://SANJET:Sanjet25653819@192.168.70.90:554/stream1
   rtsp://SANJET:Sanjet25653819@192.168.70.90:554/stream2
   rtsp://SANJET:Sanjet25653819@192.168.70.90:554/
   rtsp://192.168.70.90:554/stream1
   ```
4. 記下成功的 URL

---

## 📋 常見的相機流路徑

| 品牌/型號 | 常見流路徑 |
|----------|---------|
| Hikvision | `/Streaming/Channels/101` |
| Dahua | `/cam/realmonitor?channel=1` |
| TP-Link | `/stream1`, `/stream2` |
| Axis | `/axis-media/media.3gp` |
| D-Link | `/video1.264` |
| ONVIF 標準 | `/media/video1`, `/media/video2` |
| 通用 | `/`, `/live`, `/h264` |

---

## 🔧 設定儲存方式

### 方式 1：使用 UI 工具儲存

使用「RTSP URL 快速測試」窗口的「💾 使用此 URL」按鈕。

### 方式 2：手動編輯配置文件

編輯：`%LOCALAPPDATA%\SANJET\rtsp_settings.json`

```json
{
  "IpAddress": "192.168.70.90",
  "Username": "SANJET",
  "Password": "Sanjet25653819",
  "Port": 554,
  "StreamPath": "stream2"
}
```

### 方式 3：使用代碼設定

```csharp
var settings = RtspStreamSettings.Load();
settings.StreamPath = "stream2";  // 根據測試結果設定
settings.Save();
```

---

## 🎯 新增工具說明

### 1. RtspUrlTester（.NET 類庫）

```csharp
using (var tester = new RtspUrlTester())
{
    var result = await tester.TestUrlAsync("rtsp://...", timeoutMs: 5000);

    if (result.Success)
    {
        Console.WriteLine("✅ 連接成功");
    }
    else
    {
        Console.WriteLine($"❌ 錯誤：{result.Error}");
    }
}
```

### 2. RtspUrlTestWindow（WPF 窗口）

```csharp
var testWindow = new RtspUrlTestWindow();
testWindow.ShowDialog();
```

功能：
- 🔍 測試單個 URL
- 🔄 自動測試 50+ 個常見路徑
- 💾 一鍵儲存成功的 URL

### 3. RtspQuickDiagnostic（命令行工具）

```bash
RtspQuickDiagnostic.exe <ip> [username] [password] [port]
```

優勢：
- 無需 GUI 環境
- 快速批量測試
- 適合自動化診斷

---

## ✅ 完整排查清單

- [ ] 確認相機 IP 地址：`192.168.70.90`
- [ ] 確認 RTSP 埠：`554`
- [ ] 確認相機在線：`ping 192.168.70.90`
- [ ] 檢查防火牆：`Test-NetConnection -ComputerName 192.168.70.90 -Port 554`
- [ ] 查看相機使用手冊找到流路徑
- [ ] 使用 VLC 測試 RTSP URL
- [ ] 使用自動診斷工具測試常見路徑
- [ ] 確認用戶名和密碼正確
- [ ] 檢查相機網頁管理界面的 RTSP 設定

---

## 📞 如果仍然無法連接

請提供以下信息：

1. **相機型號和固件版本**
   ```powershell
   # 訪問相機網頁管理界面
   http://192.168.70.90
   # 查看「系統信息」或「關於」
   ```

2. **VLC 測試結果**
   - 哪些 URL 格式能在 VLC 中工作？

3. **所有已嘗試的 RTSP URL**

4. **完整的 VLC Debug 日誌**
   - 從 Visual Studio「輸出」窗口複製

---

## 🔑 常用命令參考

```powershell
# 測試網絡連接
ping 192.168.70.90

# 測試埠開放情況
Test-NetConnection -ComputerName 192.168.70.90 -Port 554

# 訪問相機管理界面
Start-Process "http://192.168.70.90"

# 查看本地 RTSP 配置
Get-Content "$env:LOCALAPPDATA\SANJET\rtsp_settings.json"
```

---

## 📝 下一步

1. ✅ 使用「RTSP URL 快速測試」工具找到有效 URL
2. ✅ 在工具中驗證 URL 可用
3. ✅ 點擊「💾 使用此 URL」儲存設定
4. ✅ 重新啟動串流窗口
5. ✅ 確認畫面顯示正常

**預期結果：** 串流窗口應該顯示相機畫面並在標題欄顯示「RTSP 串流 - Playing」
