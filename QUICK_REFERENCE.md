# 🎯 RTSP 串流快速參考卡

## 問題症狀

```
VLC [Error] live555: connection error 404
→ 缺少流路徑或路徑無效
```

## 解決方案流程圖

```
START
  ↓
打開 RtspUrlTestWindow
  ↓
點擊「🔄 自動測試所有」
  ↓
找到 ✅ URL？
  ├─ 是 → 點擊「💾 使用此 URL」
  │      → 重啟應用
  │      → ✅ 完成！
  │
  └─ 否 → 檢查網絡連接
         → 查看相機手冊
         → 在 VLC 中測試
```

---

## 📱 常用命令

### 檢查相機連接
```powershell
ping 192.168.70.90
```

### 檢查埠開放
```powershell
Test-NetConnection -ComputerName 192.168.70.90 -Port 554
```

### 查看配置文件
```powershell
Get-Content "$env:LOCALAPPDATA\SANJET\rtsp_settings.json"
```

### 運行命令行診斷
```powershell
RtspQuickDiagnostic.exe 192.168.70.90 SANJET Sanjet25653819 554
```

---

## 🔧 代碼片段

### 打開診斷窗口
```csharp
new RtspUrlTestWindow().ShowDialog();
```

### 測試單個 URL
```csharp
using (var tester = new RtspUrlTester())
{
    var result = await tester.TestUrlAsync("rtsp://...");
    if (result.Success) Console.WriteLine("✅ 成功");
}
```

### 手動設定流路徑
```csharp
var settings = RtspStreamSettings.Load();
settings.StreamPath = "stream2";  // 根據測試結果
settings.Save();
```

---

## 📋 常見流路徑

| 品牌 | 路徑 |
|------|------|
| TP-Link | `/stream1`, `/stream2` |
| Hikvision | `/Streaming/Channels/101` |
| Dahua | `/cam/realmonitor?channel=1` |
| D-Link | `/video1.264` |
| ONVIF | `/media/video1` |
| 通用 | `/`, `/live`, `/h264` |

---

## 📚 文檔導航

| 文檔 | 用途 | 時間 |
|------|------|------|
| QUICK_START.md | 5分鐘快速解決 | ⚡ |
| PROBLEM_ANALYSIS.md | 理解根本原因 | 📖 |
| RTSP_Tools_Guide.md | 工具詳細說明 | 📚 |
| RTSP_Diagnosis_Guide.md | 進階排查 | 🔍 |

---

## ✅ 檢查清單

- [ ] 打開 RtspUrlTestWindow
- [ ] 執行自動測試
- [ ] 找到有效 URL（✅）
- [ ] 儲存配置
- [ ] 重啟應用
- [ ] 確認畫面顯示

---

## 🆘 快速求救

如果仍有問題，請提供：

1. 相機型號
2. 「自動測試」完整輸出
3. VLC 是否能播放（及哪個 URL）
4. Visual Studio 輸出窗口的 VLC 日誌

---

## 🎉 預期時間

| 步驟 | 時間 |
|------|------|
| 打開診斷工具 | 1分鐘 |
| 自動測試 | 1-2分鐘 |
| 儲存配置 | 1分鐘 |
| 重啟應用 | 1分鐘 |
| **總計** | **⏱️ 5 分鐘** |

---

**立即開始：→ QUICK_START.md** 🚀
