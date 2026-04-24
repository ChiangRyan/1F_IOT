# 🚀 快速啟動指南 - RTSP 串流問題解決

## ⏱️ 5 分鐘快速解決

### 第 1 步：打開診斷工具（1 分鐘）

在您的應用程序中，找到並點擊「RTSP URL 快速測試」，或添加以下代碼：

```csharp
new RtspUrlTestWindow().ShowDialog();
```

### 第 2 步：自動測試（2-3 分鐘）

1. 在「快速 URL 測試」窗口中
2. 點擊「🔄 自動測試所有」按鈕
3. 等待測試完成

### 第 3 步：儲存結果（1 分鐘）

1. 查看「✅ 找到的有效 URL」部分
2. 點擊「💾 使用此 URL」按鈕
3. 確認儲存成功

### 第 4 步：重啟驗證（1 分鐘）

1. 關閉並重新打開串流窗口
2. 確認畫面顯示正常
3. 標題欄應顯示「RTSP 串流 - Playing」

---

## 📊 測試結果解讀

### ✅ 成功案例

```
✅ 找到的有效 URL：

✅ rtsp://SANJET:Sanjet25653819@192.168.70.90:554/stream2
✅ rtsp://192.168.70.90:554/stream2
```

→ 選擇任一有效 URL 並儲存

### ❌ 失敗案例

```
❌ 未找到任何有效的流路徑

建議排查：
1. 確認 IP 地址和埠是否正確
2. 確認相機是否在線
3. 確認防火牆設定
```

→ 按照建議進行排查

---

## 🔍 診斷指令

### 檢查相機是否在線

```powershell
ping 192.168.70.90
```

期望結果：
```
PING 192.168.70.90 (192.168.70.90): 56 data bytes
64 bytes from 192.168.70.90: icmp_seq=0 ttl=64 time=2.358 ms
```

### 檢查 RTSP 埠是否開放

```powershell
Test-NetConnection -ComputerName 192.168.70.90 -Port 554
```

期望結果：
```
ComputerName     : 192.168.70.90
RemoteAddress    : 192.168.70.90
RemotePort       : 554
TcpTestSucceeded : True  ← 應為 True
```

---

## 💾 手動編輯設定

如果工具無法工作，可以手動編輯配置文件：

**文件位置：** `%LOCALAPPDATA%\SANJET\rtsp_settings.json`

**範例內容：**
```json
{
  "IpAddress": "192.168.70.90",
  "Username": "SANJET",
  "Password": "Sanjet25653819",
  "Port": 554,
  "StreamPath": "stream2"
}
```

**常見流路徑：**
- `/stream1` - 某些 TP-Link 相機
- `/stream2` - 某些 TP-Link 相機
- `/` - 根路徑（某些相機預設值）
- `/media/video1` - ONVIF 標準
- `/Streaming/Channels/101` - Hikvision
- `/cam` - D-Link、某些通用相機

---

## 🆘 進階故障排查

### 使用 VLC 手動測試

1. 下載並安裝 [VLC 媒體播放器](https://www.videolan.org/)
2. 打開 VLC
3. 菜單：媒體 → 打開位置
4. 嘗試不同的 URL：

```
rtsp://SANJET:Sanjet25653819@192.168.70.90:554/stream1
rtsp://SANJET:Sanjet25653819@192.168.70.90:554/stream2
rtsp://SANJET:Sanjet25653819@192.168.70.90:554/
rtsp://192.168.70.90:554/stream1
```

5. 記下在 VLC 中成功的 URL
6. 將該 URL 複製到應用程序設定

### 查看詳細日誌

在 Visual Studio 中：
1. 打開「輸出」窗口（View → Output）
2. 選擇「Debug」輸出
3. 查看 VLC 的詳細日誌信息

---

## 📱 相機常見品牌流路徑

| 品牌 | 型號範例 | 流路徑 |
|------|---------|--------|
| **TP-Link** | C200, C100 | `/stream1`, `/stream2` |
| **Hikvision** | DS-2CD2xxx | `/Streaming/Channels/101` |
| **Dahua** | IPC-HDBW | `/cam/realmonitor?channel=1` |
| **D-Link** | DCS-F | `/video1.264` |
| **Axis** | M1004 | `/axis-media/media.3gp` |
| **ONVIF 標準** | 通用 | `/media/video1` |
| **Ubiquiti** | UVC | `/ufvs` |

---

## ✅ 完成檢查清單

在關閉此文檔前，請確認：

- [ ] 已打開「RTSP URL 快速測試」窗口
- [ ] 已點擊「🔄 自動測試所有」
- [ ] 找到了至少一個有效 URL（✅）
- [ ] 已點擊「💾 使用此 URL」儲存
- [ ] 已重新啟動串流窗口
- [ ] 串流窗口現在顯示畫面
- [ ] 標題欄顯示「RTSP 串流 - Playing」

---

## 🎉 成功

如果以上步驟完成且串流正常播放，恭喜！🎊

您現在可以：
- ✅ 實時監看相機畫面
- ✅ 保存配置設定
- ✅ 隨時重新連接

---

## 🆘 仍有問題？

請收集以下信息並聯繫支援：

1. **相機信息**
   - 品牌和型號
   - 固件版本

2. **測試結果**
   - 「自動測試」的完整輸出
   - VLC 是否能播放

3. **日誌信息**
   - Visual Studio 輸出窗口的 VLC 日誌
   - 錯誤信息截圖

4. **網絡信息**
   - 相機 IP：`192.168.70.90`
   - RTSP 埠：`554`
   - 相機是否在同一網段

---

**祝您使用愉快！** 🎥📹
