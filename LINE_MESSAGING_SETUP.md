# LINE 故障推播設定指南

本專案支援兩種 LINE 故障推播通道：

1. **LINE Messaging API**：使用 LINE 官方 API 發送訊息。
2. **LINE AutoHotkey**：不使用官方 API，改由 AutoHotkey 操作 Windows 桌面版 LINE App，搜尋聊天室並貼上訊息送出。

兩個通道可以同時存在，並由設定檔各自控制啟用狀態。若只想使用桌面版 LINE App，請保留 `LineMessaging` 設定但將 `Enabled` 設為 `false`，再啟用 `LineAutoHotkey`。

## 1. 共用故障通知設定

`FaultNotification` 控制故障通知的共用策略，不綁定特定 LINE 發送方式：

```json
{
  "FaultNotification": {
    "Enabled": true,
    "CooldownMinutes": 30,
    "NotifyRecovery": true
  }
}
```

| 設定 | 說明 |
| --- | --- |
| `Enabled` | 是否啟用故障通知流程。 |
| `CooldownMinutes` | 同一設備重複故障通知的冷卻時間。 |
| `NotifyRecovery` | 設備從故障恢復時是否發送恢復通知。 |

## 2. LINE Messaging API 設定

### 2.1 建立 LINE 官方帳號與 Messaging API Channel

1. 到 LINE Official Account Manager 建立官方帳號。
2. 到 LINE Developers Console 建立或綁定 Messaging API Channel。
3. 在 Messaging API Channel 產生 `Channel access token`。
4. 將 Bot 加入要接收通知的使用者或群組。

> 注意：LINE Notify 已停止服務，請使用 Messaging API 的 Push Message。

### 2.2 取得推播目標 ID

`TargetIds` 可以放一個或多個 LINE 目標：

- 使用者 ID：通常以 `U` 開頭。
- 群組 ID：通常以 `C` 開頭。
- 多人聊天室 ID：通常以 `R` 開頭。

實務上可先在 LINE Developers Console 啟用 Webhook，讓使用者傳訊息或讓群組產生事件，再從 Webhook 事件中取得 `source.userId` 或 `source.groupId`。

### 2.3 `appsettings.json` 範例

```json
{
  "LineMessaging": {
    "Enabled": true,
    "ChannelAccessToken": "請填入 LINE Channel access token",
    "TargetIds": [
      "請填入 userId 或 groupId"
    ],
    "CooldownMinutes": 30,
    "NotifyRecovery": true
  }
}
```

正式環境建議不要把 token 寫進檔案，可改用環境變數覆寫：

```powershell
$env:LineMessaging__Enabled="true"
$env:LineMessaging__ChannelAccessToken="你的 Channel access token"
$env:LineMessaging__TargetIds__0="你的 userId 或 groupId"
```

## 3. LINE AutoHotkey 設定

此模式不使用 LINE 官方 Messaging API，而是啟動 AutoHotkey 腳本操作目前登入 Windows 桌面工作階段中的 LINE 桌面版 App。它會：

1. 確認 LINE 程序是否存在，不存在時依 `LineExecutablePath` 啟動。
2. 將 LINE 主視窗切到前景。
3. 使用 `Ctrl+F` 搜尋 `TargetChatNames` 指定的聊天室。
4. 開啟搜尋結果後，透過剪貼簿貼上故障訊息。
5. 送出訊息。

### 3.1 `appsettings.json` 範例

```json
{
  "LineMessaging": {
    "Enabled": false,
    "ChannelAccessToken": "",
    "TargetIds": [],
    "CooldownMinutes": 30,
    "NotifyRecovery": true
  },
  "LineAutoHotkey": {
    "Enabled": true,
    "AutoHotkeyExecutablePath": "C:\\Program Files\\AutoHotkey\\v2\\AutoHotkey64.exe",
    "AutoHotkeyVersion": "v2",
    "LineExecutablePath": "C:\\Users\\你的帳號\\AppData\\Local\\LINE\\bin\\LineLauncher.exe",
    "LineProcessName": "LINE",
    "TargetChatNames": [
      "設備告警群組"
    ],
    "OperationTimeoutSeconds": 15,
    "SendDelayMilliseconds": 300,
    "RestoreClipboard": true
  }
}
```

| 設定 | 說明 |
| --- | --- |
| `Enabled` | 是否啟用桌面版 LINE 自動操作通道。 |
| `AutoHotkeyExecutablePath` | AutoHotkey 執行檔路徑；AutoHotkey v2 預設常見位置為 `C:\Program Files\AutoHotkey\v2\AutoHotkey64.exe`。 |
| `AutoHotkeyVersion` | 產生腳本的語法版本，支援 `v2` 或 `v1`；建議使用 `v2`。 |
| `LineExecutablePath` | LINE 未啟動時要執行的啟動程式路徑。 |
| `LineProcessName` | LINE 程序名稱，通常維持 `LINE`。 |
| `TargetChatNames` | 要接收訊息的聊天室或群組名稱，需與 LINE 搜尋時可找到的聊天室或群組名稱一致。 |
| `OperationTimeoutSeconds` | 等待 LINE 啟動或操作的逾時秒數。 |
| `SendDelayMilliseconds` | 每個 UI 操作之間的等待時間；現場電腦較慢時可調大。 |
| `RestoreClipboard` | 發送後是否嘗試還原原本的文字剪貼簿內容。 |

### 3.2 使用限制

- Windows 必須維持登入且桌面工作階段可操作；鎖定畫面、登出或無互動 Session 可能導致失敗。
- LINE 桌面版必須已登入，且不能停在 QR Code、更新、公告或錯誤彈窗。
- 發送期間請避免人工操作鍵盤滑鼠，避免焦點被搶走造成貼錯視窗。
- 請確認 AutoHotkey 已安裝，且 `AutoHotkeyExecutablePath` / `AutoHotkeyVersion` 與實際版本一致。
- AutoHotkey 會使用 LINE 搜尋功能開啟聊天室；若搜尋結果不唯一，請將 `TargetChatNames` 設成足夠明確的群組或聊天室名稱。
- 若聊天室或群組改名，請同步更新 `TargetChatNames`。
- 桌面版自動操作不像 Messaging API 有 HTTP 回應碼，因此成功判斷主要依流程是否發生例外與程式日誌。

## 4. 推播觸發規則

目前推播規則如下：

| 狀態變化 | 動作 |
| --- | --- |
| 正常狀態 → `故障` | 發送故障通知 |
| 正常狀態 → `通訊失敗` | 發送故障通知 |
| 故障/通訊失敗 → 正常狀態 | 若 `NotifyRecovery=true`，發送恢復通知 |
| 同設備重複故障 | 依 `CooldownMinutes` 冷卻時間抑制重複通知 |

## 5. 訊息額度注意事項

使用 LINE Messaging API 時，台灣 LINE 官方帳號免費方案每月內含訊息數可能依 LINE 官方政策調整。若發送到群組，一次推播可能依可接收成員數計算訊息用量，因此建議保留 `CooldownMinutes`，避免設備連續異常時洗版。

使用 LINE AutoHotkey 時不消耗 Messaging API 額度，但穩定性取決於 Windows 桌面環境、AutoHotkey 與 LINE App 狀態。
