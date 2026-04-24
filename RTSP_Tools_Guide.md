# RTSP 串流故障排查工具集

## 📊 您的問題

根據最新 VLC 日誌分析：

```
VLC [Debug] live555: connection error 404 ← 找不到流資源
VLC [Error] live555: Failed to connect with rtsp://192.168.70.90:554
```

**原因：** 當前 RTSP URL 中的流路徑無效。需要找到相機支援的正確流路徑。

---

## 🛠️ 推薦的快速解決方案

### ✅ 步驟 1：快速測試（5 分鐘）

**使用「RTSP URL 快速測試」窗口：**

```csharp
// 在您的應用程序中添加這一行
new RtspUrlTestWindow().ShowDialog();
```

**使用步驟：**
1. 打開窗口
2. 點擊「🔄 自動測試所有」
3. 等待 30-60 秒
4. 查看「✅ 找到的有效 URL」
5. 點擊「💾 使用此 URL」保存

---

### 🎯 步驟 2：重啟應用

重新啟動串流窗口，應該看到：
- ✅ 串流窗口顯示畫面
- ✅ 標題欄顯示「RTSP 串流 - Playing」

---

## 📚 所有可用工具

### 1. RtspUrlTestWindow（推薦 👍）

**類型：** WPF 圖形界面窗口

**功能：**
- 🔍 測試單個 URL
- 🔄 自動測試 50+ 常見路徑
- 💾 一鍵儲存成功的 URL
- 📊 詳細結果展示

**使用代碼：**
```csharp
var window = new RtspUrlTestWindow();
window.ShowDialog();
```

**特點：**
- 最快找到正確 URL
- 支援複製和編輯 URL
- 自動解析和儲存設定

---

### 2. RtspUrlTester（.NET 類庫）

**類型：** C# 類庫，用於測試 RTSP URL

**功能：**
- 測試單個 URL
- 批量測試多個 URL
- 返回詳細的連接結果

**使用代碼：**
```csharp
using (var tester = new RtspUrlTester())
{
    // 測試單個 URL
    var result = await tester.TestUrlAsync("rtsp://...", timeoutMs: 5000);

    if (result.Success)
    {
        Console.WriteLine($"✅ 連接成功: {result.Url}");
    }
    else
    {
        Console.WriteLine($"❌ 失敗: {result.Error}");
    }

    // 或批量測試
    var urls = new[] { "rtsp://...", "rtsp://..." };
    var results = await tester.TestMultipleUrlsAsync(urls);
}
```

**返回結果：**
```csharp
public class RtspTestResult
{
    public string Url { get; set; }              // 測試的 URL
    public bool Success { get; set; }            // 是否成功
    public string Error { get; set; }            // 錯誤信息
    public bool PlayStarted { get; set; }        // 播放是否啟動
    public bool PlayingEventTriggered { get; set; } // Playing 事件是否觸發
    public bool ErrorEventTriggered { get; set; }   // Error 事件是否觸發
}
```

---

### 3. RtspDiagnostics（診斷工具）

**類型：** 增強版診斷類

**改進：**
- 測試 26 種常見流路徑
- 支援帶認證和不帶認證的 URL
- 詳細的事件日誌

**使用代碼：**
```csharp
using (var diagnostics = new RtspDiagnostics())
{
    var results = await diagnostics.TestCommonStreamPaths(
        ipAddress: "192.168.70.90",
        username: "SANJET",
        password: "Sanjet25653819",
        port: 554,
        timeoutMs: 5000);

    foreach (var (url, success, error) in results)
    {
        Console.WriteLine(success ? $"✅ {url}" : $"❌ {url} - {error}");
    }
}
```

---

### 4. RtspQuickDiagnostic（命令行工具）

**類型：** 獨立可執行程序

**用途：** 無需 GUI 環境的快速診斷

**使用方式：**
```powershell
# 基本用法
RtspQuickDiagnostic.exe 192.168.70.90

# 指定認證信息
RtspQuickDiagnostic.exe 192.168.70.90 SANJET Sanjet25653819 554

# 指定不同埠
RtspQuickDiagnostic.exe 192.168.1.100 admin password 8554
```

**輸出範例：**
```
╔════════════════════════════════════════════════════════════════╗
║           RTSP 快速診斷工具                                    ║
╚════════════════════════════════════════════════════════════════╝

📡 目標相機：192.168.70.90:554
👤 使用者名稱：SANJET
🔐 密碼：*****

🔍 準備測試 52 個 URL...
⏳ 測試中，請稍候...

✅ 找到的有效 URL：

  ✅ rtsp://SANJET:Sanjet25653819@192.168.70.90:554/stream2
  ✅ rtsp://192.168.70.90:554/stream2

✅ 請在應用程序設定中使用上述有效 URL
```

---

## 🔄 RtspStreamSettings 改進

新增方法支援多種 URL 格式：

```csharp
// 標準格式（帶認證）
string url = settings.BuildRtspUrl();
// rtsp://user:pass@host:port/path

// 查詢字符串格式（某些相機需要）
string url = settings.BuildRtspUrl(useQueryAuth: true);
// rtsp://host:port/path?user=xxx&password=xxx
```

---

## 🎯 推薦工作流程

```mermaid
graph TD
    A[相機無法串流] --> B{打開 RtspUrlTestWindow}
    B --> C[點擊自動測試]
    C --> D{找到有效 URL?}
    D -->|是| E[點擊使用此 URL]
    D -->|否| F[檢查相機連接]
    E --> G[重啟應用]
    G --> H[✅ 串流成功]
    F --> I[使用 VLC 手動測試]
    I --> J[查看相機手冊]
```

---

## 📋 快速參考

| 工具 | 推薦用途 | 難度 | 速度 |
|------|---------|------|------|
| **RtspUrlTestWindow** | 首選方案 | ⭐ | ⚡ 最快 |
| **RtspQuickDiagnostic** | 命令行 | ⭐⭐ | ⚡ 快 |
| **RtspUrlTester** | 程序集成 | ⭐⭐ | 中等 |
| **RtspDiagnostics** | 詳細診斷 | ⭐⭐⭐ | 中等 |

---

## 💡 常見問題

### Q: 為什麼沒有找到任何有效的 URL？

A: 可能原因：
1. 相機 IP 地址錯誤
2. 相機未開機或不在線
3. 防火牆阻止了連接
4. 相機使用非標準的流路徑

**解決方案：**
1. 執行：`ping 192.168.70.90`
2. 執行：`Test-NetConnection -ComputerName 192.168.70.90 -Port 554`
3. 查看相機網頁管理界面
4. 查看相機使用手冊

### Q: VLC 可以播放，但應用不行？

A: 可能原因：
1. LibVLC 版本問題
2. 硬體加速衝突
3. 媒體選項設定差異

**解決方案：**
- 確保禁用硬體加速：`--avcodec-hw=none`
- 嘗試增加超時時間：`:rtsp-timeout=5000`
- 嘗試不同的傳輸方式：`:rtsp-tcp`

### Q: 如何手動測試 RTSP URL？

A: 使用 VLC 媒體播放器：
1. 打開 VLC
2. Ctrl+N 或菜單 → 媒體 → 打開位置
3. 輸入 RTSP URL
4. 點擊播放

---

## 🚀 下一步

1. ✅ 運行 `RtspUrlTestWindow`
2. ✅ 自動測試找到有效 URL
3. ✅ 儲存設定
4. ✅ 重啟應用
5. ✅ 享受無限期的串流 🎥

---

## 📞 需要幫助？

如果工具仍無法找到有效 URL，請提供：
1. 相機型號和固件版本
2. 完整的 VLC 日誌
3. `%LOCALAPPDATA%\SANJET\rtsp_settings.json` 內容
4. 相機網頁管理界面的截圖
