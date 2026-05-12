# LINE Messaging API 故障推播設定指南

本專案已內建 LINE Messaging API 推播服務，當 Modbus 輪詢讀到設備狀態由正常轉為「故障」、或讀取回應變成「通訊失敗」時，會主動推播通知；當狀態從故障恢復為正常狀態時，也可推播恢復通知。

## 1. 建立 LINE 官方帳號與 Messaging API Channel

1. 到 LINE Official Account Manager 建立官方帳號。
2. 到 LINE Developers Console 建立或綁定 Messaging API Channel。
3. 在 Messaging API Channel 產生 `Channel access token`。
4. 將 Bot 加入要接收通知的使用者或群組。

> 注意：LINE Notify 已停止服務，請使用 Messaging API 的 Push Message。

## 2. 取得推播目標 ID

`TargetIds` 可以放一個或多個 LINE 目標：

- 使用者 ID：通常以 `U` 開頭。
- 群組 ID：通常以 `C` 開頭。
- 多人聊天室 ID：通常以 `R` 開頭。

實務上可先在 LINE Developers Console 啟用 Webhook，讓使用者傳訊息或讓群組產生事件，再從 Webhook 事件中取得 `source.userId` 或 `source.groupId`。

## 3. 設定 `appsettings.json`

開發測試可直接修改 `appsettings.json`：

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
$env:LineMessaging__CooldownMinutes="30"
$env:LineMessaging__NotifyRecovery="true"
```

## 4. 推播觸發規則

目前推播規則如下：

| 狀態變化 | 動作 |
| --- | --- |
| 正常狀態 → `故障` | 發送故障通知 |
| 正常狀態 → `通訊失敗` | 發送故障通知 |
| 故障/通訊失敗 → 正常狀態 | 若 `NotifyRecovery=true`，發送恢復通知 |
| 同設備重複故障 | 依 `CooldownMinutes` 冷卻時間抑制重複通知 |

## 5. 訊息額度注意事項

台灣 LINE 官方帳號免費方案每月內含 200 則訊息。若發送到 10 人群組，一次推播通常會依 10 位可接收成員計算訊息用量，因此建議：

- 一般告警只推播給值班人員。
- 嚴重告警才推播到全群組。
- 保留 `CooldownMinutes`，避免設備連續異常時洗版。
