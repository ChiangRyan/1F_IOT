#include <ArduinoOTA.h>
#include <WiFi.h>
#include <PubSubClient.h>
#include <ModbusMaster.h>
#include <Arduino_JSON.h>

// ================================================================
// 1. WiFi 設定
// ================================================================
//const char* ssid = "SJguest";
//const char* password = "jetguest";

const char* ssid = "SJ_3F";
const char* password = "sanjet-5836";

// const char* ssid = "1F_IOT";
// const char* password = "sanjet25653819";

const unsigned long WIFI_CONNECT_TIMEOUT = 15000;       // 初次連線等待時間
const unsigned long WIFI_RECONNECT_DELAY = 30000;       // WiFi 斷線後，每 30 秒嘗試重連
const unsigned long WIFI_STATUS_LOG_INTERVAL = 30000;   // WiFi 狀態 Log 間隔

// ================================================================
// 2. MQTT 設定
// ================================================================
#define DEVICE_ID "ESP32_TEST_RS485"   // 每一台 ESP32 必須有不同的值

const char* mqtt_server = "192.168.98.244";
const int mqtt_port = 1883;

char mqtt_client_id[64];

char topic_led_set[128];
char topic_led_status[128];
char topic_modbus_read_req[128];
char topic_modbus_read_resp[128];
char topic_modbus_write_req[128];
char topic_modbus_write_resp[128];
char topic_status[128];

// LWT 最後遺囑設定
char lwt_topic[128];
char lwt_payload[128];
const int lwt_qos = 1;
const boolean lwt_retain = true;

WiFiClient espClient;
PubSubClient client(espClient);

#define MSG_BUFFER_SIZE (256)
char msg[MSG_BUFFER_SIZE];
bool mqtt_connected = false;

// ================================================================
// 3. OTA 設定
// ================================================================
const char* ota_hostname = "esp32-wifi-rs485";
const char* ota_password = "0000";   // 建議正式使用時改成較強密碼
bool ota_started = false;

// ================================================================
// 4. I/O 與 Modbus RTU 設定
// ================================================================
#define LED_BUILTIN 2

// WiFi 版 ESP32-WROOM-32 可維持原本 GPIO16 / GPIO17
#define MODBUS_RX_PIN 16
#define MODBUS_TX_PIN 17
#define MODBUS_SERIAL_BAUD 9600
#define MAX_SLAVES 11

int Address_Offset = 7000;
ModbusMaster node;

// ================================================================
// 5. 函式原型
// ================================================================
void wifiTask(void *pvParameters);
void setLEDState(bool state);
void handleMqttSetLED(byte* payload, unsigned int length);
void handleMqttReadModbus(byte* payload, unsigned int length);
void handleMqttWriteModbus(byte* payload, unsigned int length);
bool initWiFi();
bool wifiReady();
void setupOTA();
void reconnectMqtt();
void publishOnlineStatus(bool retainFlag = true);

// ================================================================
// 6. MQTT Topic 建立
// ================================================================
void buildTopics() {
  snprintf(topic_led_set, sizeof(topic_led_set), "devices/%s/led/set", DEVICE_ID);
  snprintf(topic_led_status, sizeof(topic_led_status), "devices/%s/led/status", DEVICE_ID);
  snprintf(topic_modbus_read_req, sizeof(topic_modbus_read_req), "devices/%s/modbus/read/request", DEVICE_ID);
  snprintf(topic_modbus_read_resp, sizeof(topic_modbus_read_resp), "devices/%s/modbus/read/response", DEVICE_ID);
  snprintf(topic_modbus_write_req, sizeof(topic_modbus_write_req), "devices/%s/modbus/write/request", DEVICE_ID);
  snprintf(topic_modbus_write_resp, sizeof(topic_modbus_write_resp), "devices/%s/modbus/write/response", DEVICE_ID);
  snprintf(topic_status, sizeof(topic_status), "devices/%s/status", DEVICE_ID);
  snprintf(lwt_topic, sizeof(lwt_topic), "devices/%s/status", DEVICE_ID);
}

// ================================================================
// 7. WiFi 初始化與狀態檢查
// ================================================================
bool wifiReady() {
  return WiFi.status() == WL_CONNECTED;
}

bool initWiFi() {
  Serial.println("[WiFi] 正在啟動 WiFi...");

  WiFi.mode(WIFI_STA);
  WiFi.setSleep(false);       // 關閉省電模式，降低 MQTT / Modbus 輪巡時的斷線機率
  WiFi.disconnect(false);     // 不清除 WiFi 設定，只中斷目前連線
  delay(100);

  WiFi.begin(ssid, password);

  Serial.print("[WiFi] 連線中");
  unsigned long start = millis();
  while (!wifiReady() && millis() - start < WIFI_CONNECT_TIMEOUT) {
    Serial.print(".");
    delay(500);
  }
  Serial.println();

  if (!wifiReady()) {
    Serial.println("[WiFi] 連線失敗，稍後由連線任務重試");
    return false;
  }

  Serial.println("[WiFi] 連線成功");
  Serial.println("[WiFi] IP: " + WiFi.localIP().toString());
  Serial.println("[WiFi] MAC: " + WiFi.macAddress());
  Serial.println("[WiFi] RSSI: " + String(WiFi.RSSI()) + " dBm");
  return true;
}

// ================================================================
// 8. OTA 初始化
// ================================================================
void setupOTA() {
  if (ota_started) return;

  ArduinoOTA.setHostname(ota_hostname);
  ArduinoOTA.setPassword(ota_password);

  ArduinoOTA.onStart([]() {
    Serial.println("[OTA] 開始更新...");
  });

  ArduinoOTA.onEnd([]() {
    Serial.println("\n[OTA] 更新完成！");
  });

  ArduinoOTA.onProgress([](unsigned int progress, unsigned int total) {
    Serial.printf("[OTA] 進度: %u%%\r", (progress / (total / 100)));
  });

  ArduinoOTA.onError([](ota_error_t error) {
    Serial.printf("[OTA] 錯誤 [%u]: ", error);
    if (error == OTA_AUTH_ERROR) Serial.println("驗證失敗");
    else if (error == OTA_BEGIN_ERROR) Serial.println("開始失敗");
    else if (error == OTA_CONNECT_ERROR) Serial.println("連線失敗");
    else if (error == OTA_RECEIVE_ERROR) Serial.println("接收失敗");
    else if (error == OTA_END_ERROR) Serial.println("結束失敗");
  });

  ArduinoOTA.begin();
  ota_started = true;
  Serial.println("[OTA] 初始化完成");
}

// ================================================================
// 9. MQTT 狀態發佈
// ================================================================
void publishOnlineStatus(bool retainFlag) {
  JSONVar onlineMsg;
  onlineMsg["Status"] = "online";
  onlineMsg["IP"] = WiFi.localIP().toString();
  onlineMsg["MAC"] = WiFi.macAddress();
  onlineMsg["RSSI"] = WiFi.RSSI();
  onlineMsg["Network"] = "WiFi";
  onlineMsg["DeviceId"] = DEVICE_ID;

  String jsonString = JSON.stringify(onlineMsg);
  client.publish(topic_status, jsonString.c_str(), retainFlag);
  Serial.println("[MQTT] 已發佈上線狀態: " + jsonString + " 到主題: " + String(topic_status));
}

// ================================================================
// 10. MQTT Callback
// ================================================================
void callback(char* topic, byte* payload, unsigned int length) {
  char payload_copy[length + 1];
  memcpy(payload_copy, payload, length);
  payload_copy[length] = '\0';
  Serial.println(payload_copy);

  if (strcmp(topic, topic_led_set) == 0) {
    handleMqttSetLED(payload, length);
  } else if (strcmp(topic, topic_modbus_read_req) == 0) {
    handleMqttReadModbus(payload, length);
  } else if (strcmp(topic, topic_modbus_write_req) == 0) {
    handleMqttWriteModbus(payload, length);
  }
}

// ================================================================
// 11. MQTT 重新連線
// ================================================================
void reconnectMqtt() {
  if (!wifiReady()) {
    mqtt_connected = false;
    return;
  }

  Serial.print("[MQTT] 嘗試連線 Client ID: ");
  Serial.println(mqtt_client_id);
  Serial.print("[MQTT] LWT 主題: ");
  Serial.println(lwt_topic);
  Serial.print("[MQTT] LWT 內容: ");
  Serial.println(lwt_payload);

  if (client.connect(mqtt_client_id, NULL, NULL, lwt_topic, lwt_qos, lwt_retain, lwt_payload)) {
    Serial.println("[MQTT] 已連線");
    mqtt_connected = true;

    client.subscribe(topic_led_set);
    client.subscribe(topic_modbus_read_req);
    client.subscribe(topic_modbus_write_req);

    Serial.println("[MQTT] 已訂閱主題:");
    Serial.println(topic_led_set);
    Serial.println(topic_modbus_read_req);
    Serial.println(topic_modbus_write_req);

    publishOnlineStatus(true);
  } else {
    Serial.print("[MQTT] 連線失敗, rc=");
    Serial.println(client.state());
    mqtt_connected = false;
  }
}

// ================================================================
// 12. LED 狀態控制
// ================================================================
void setLEDState(bool state) {
  static bool lastState = false;

  // 避免每 5 秒重複印一樣的 LED 狀態，讓 Log 更乾淨
  if (lastState != state) {
    Serial.println("[LED] 狀態設為: " + String(state ? "開啟" : "關閉"));
    lastState = state;
  }

  digitalWrite(LED_BUILTIN, state ? HIGH : LOW);
}

// ================================================================
// 13. Setup
// ================================================================
void setup() {
  Serial.begin(115200);
  delay(300);

  pinMode(LED_BUILTIN, OUTPUT);
  setLEDState(false);

  Serial.println("[主程式] ESP32 WiFi MQTT RS485 初始化開始...");

  snprintf(mqtt_client_id, sizeof(mqtt_client_id), "esp32-%s", DEVICE_ID);
  buildTopics();

  // 設定 LWT payload
  JSONVar lwtMsgJson;
  lwtMsgJson["Status"] = "offline";
  lwtMsgJson["DeviceId"] = DEVICE_ID;
  String lwtJsonString = JSON.stringify(lwtMsgJson);
  strncpy(lwt_payload, lwtJsonString.c_str(), sizeof(lwt_payload) - 1);
  lwt_payload[sizeof(lwt_payload) - 1] = '\0';

  // Watchdog 先停用。
  // 原因：WiFi 連線、MQTT 重連、Modbus RTU 等待回應時，可能造成 loopTask 來不及 reset WDT 而重啟。
  // 若未來系統穩定後要啟用，建議放在 WiFi / MQTT / Modbus 都初始化完成後再啟用。

  // 初始化 WiFi，使用 DHCP 自動取得 IP
  if (initWiFi()) {
    setLEDState(true);
    setupOTA();
  } else {
    setLEDState(false);
    Serial.println("[主程式] WiFi 初始化未成功，稍後由連線任務監控");
  }

  // 初始化 Modbus RTU
  Serial2.begin(MODBUS_SERIAL_BAUD, SERIAL_8N1, MODBUS_RX_PIN, MODBUS_TX_PIN);
  Serial.println("[Modbus] Serial2 已初始化");
  Serial.println("[Modbus] RX=" + String(MODBUS_RX_PIN) + ", TX=" + String(MODBUS_TX_PIN));

  // 初始化 MQTT
  client.setServer(mqtt_server, mqtt_port);
  client.setCallback(callback);
  client.setBufferSize(MSG_BUFFER_SIZE);
  client.setKeepAlive(30);
  client.setSocketTimeout(5);

  // 建立 WiFi / MQTT 連線管理任務
  xTaskCreatePinnedToCore(
    wifiTask,
    "WiFiTask",
    8192,
    NULL,
    1,
    NULL,
    0
  );
  Serial.println("[主程式] WiFi 連線管理任務已創建");

  Serial.printf("[主程式] 可用堆記憶體: %d bytes\n", ESP.getFreeHeap());
}

// ================================================================
// 14. Loop
// ================================================================
void loop() {
  if (ota_started) {
    ArduinoOTA.handle();
  }

  if (wifiReady() && client.connected()) {
    client.loop();
  }

  delay(20);
}

// ================================================================
// 15. WiFi / MQTT 連線管理任務
// ================================================================
void wifiTask(void *pvParameters) {
  Serial.println("[連線任務] 啟動於核心 " + String(xPortGetCoreID()));

  unsigned long lastWiFiReconnect = 0;
  unsigned long lastStatusLog = 0;
  unsigned long lastMqttReconnect = 0;
  bool wasWiFiDown = false;

  const unsigned long mqttReconnectInterval = 5000;

  for (;;) {
    bool wifi = wifiReady();

    if (!wifi) {
      setLEDState(false);
      mqtt_connected = false;

      if (!wasWiFiDown) {
        wasWiFiDown = true;
        Serial.println("[連線任務] WiFi 已斷線");
      }

      if (millis() - lastWiFiReconnect >= WIFI_RECONNECT_DELAY) {
        Serial.println("[連線任務] 嘗試重新連線 WiFi...");
        WiFi.disconnect(false);
        delay(100);
        WiFi.begin(ssid, password);
        lastWiFiReconnect = millis();
      }
    } else {
      if (wasWiFiDown) {
        wasWiFiDown = false;
        Serial.println("[連線任務] WiFi 已恢復，IP: " + WiFi.localIP().toString());
      }

      setLEDState(true);

      if (!ota_started) {
        setupOTA();
      }

      if (!client.connected()) {
        mqtt_connected = false;
        if (millis() - lastMqttReconnect >= mqttReconnectInterval) {
          reconnectMqtt();
          lastMqttReconnect = millis();
        }
      } else {
        mqtt_connected = true;
        if (millis() - lastStatusLog >= WIFI_STATUS_LOG_INTERVAL) {
          Serial.println("[連線任務] WiFi / MQTT 正常，IP: " + WiFi.localIP().toString() +
                         ", RSSI: " + String(WiFi.RSSI()) + " dBm");
          lastStatusLog = millis();
        }
      }
    }

    vTaskDelay(pdMS_TO_TICKS(5000));
  }
}

// ================================================================
// 16. MQTT LED 控制
// ================================================================
void handleMqttSetLED(byte* payload, unsigned int length) {
  JSONVar response;
  char payloadStr[length + 1];
  memcpy(payloadStr, payload, length);
  payloadStr[length] = '\0';

  JSONVar request = JSON.parse(payloadStr);
  if (JSON.typeof(request) == "undefined") {
    Serial.println("[LED控制] JSON 解析失敗");
    response["Status"] = "error";
    response["Message"] = "無效的 JSON payload";
  } else if (!request.hasOwnProperty("state")) {
    Serial.println("[LED控制] JSON 缺少 state 欄位");
    response["Status"] = "error";
    response["Message"] = "缺少 state 參數";
  } else {
    String state = (const char*)request["state"];
    if (state == "ON") {
      setLEDState(true);
      response["Status"] = "success";
      response["Message"] = "LED 已開啟";
    } else if (state == "OFF") {
      setLEDState(false);
      response["Status"] = "success";
      response["Message"] = "LED 已關閉";
    } else {
      Serial.println("[LED控制] 無效的 state: " + state);
      response["Status"] = "error";
      response["Message"] = "無效的 state 參數: " + state;
    }
  }

  client.publish(topic_led_status, JSON.stringify(response).c_str());
}

// ================================================================
// 17. MQTT Modbus 讀取
// ================================================================
void handleMqttReadModbus(byte* payload, unsigned int length) {
  JSONVar response;
  response["DeviceId"] = DEVICE_ID;

  char payloadStr[length + 1];
  memcpy(payloadStr, payload, length);
  payloadStr[length] = '\0';
  Serial.println("[Modbus讀取請求] Payload: " + String(payloadStr));

  JSONVar request = JSON.parse(payloadStr);

  if (JSON.typeof(request) == "undefined") {
    Serial.println("[Modbus讀取] JSON 解析失敗");
    response["Status"] = "error";
    response["Message"] = "無效的 JSON payload (read request)";
    client.publish(topic_modbus_read_resp, JSON.stringify(response).c_str());
    return;
  }

  if (!request.hasOwnProperty("slaveId") || !request.hasOwnProperty("address") ||
      !request.hasOwnProperty("quantity") || !request.hasOwnProperty("functionCode")) {
    Serial.println("[Modbus讀取] JSON 缺少必要欄位");
    response["Status"] = "error";
    response["Message"] = "請求缺少必要參數 (slaveId, address, quantity, functionCode)";
    if (request.hasOwnProperty("slaveId")) response["SlaveId"] = (int)request["slaveId"];
    if (request.hasOwnProperty("address")) response["Address"] = (int)request["address"];
    if (request.hasOwnProperty("quantity")) response["Quantity"] = (int)request["quantity"];
    if (request.hasOwnProperty("functionCode")) response["FunctionCode"] = (int)request["functionCode"];
    client.publish(topic_modbus_read_resp, JSON.stringify(response).c_str());
    return;
  }

  uint8_t slaveId = (int)request["slaveId"];
  uint16_t relativeAddress = (int)request["address"];
  uint16_t modbusAddress = relativeAddress + Address_Offset;
  uint8_t quantity = (int)request["quantity"];
  uint8_t functionCode = (int)request["functionCode"];

  response["SlaveId"] = slaveId;
  response["Address"] = relativeAddress;
  response["Quantity"] = quantity;
  response["FunctionCode"] = functionCode;

  if (slaveId < 1 || slaveId > MAX_SLAVES) {
    Serial.println("[Modbus讀取] 無效的 slaveId: " + String(slaveId));
    response["Status"] = "error";
    response["Message"] = "slaveId 必須介於 1 和 " + String(MAX_SLAVES) + " 之間";
  } else if (quantity < 1 || quantity > 10) {
    Serial.println("[Modbus讀取] 無效的 quantity: " + String(quantity));
    response["Status"] = "error";
    response["Message"] = "quantity 必須介於 1 和 10 之間";
  } else {
    node.begin(slaveId, Serial2);
    uint8_t result = 0xFF;
    uint16_t dataBuffer[10];

    Serial.println("[Modbus讀取] 開始讀取從站 " + String(slaveId) +
                   ", 實際位址 " + String(modbusAddress) +
                   " (相對位址 " + String(relativeAddress) + ")" +
                   ", 數量 " + String(quantity) +
                   ", 功能碼 " + String(functionCode));

    uint8_t retries = 3;
    while (retries > 0) {
      if (functionCode == 3) {
        result = node.readHoldingRegisters(modbusAddress, quantity);
      } else if (functionCode == 4) {
        result = node.readInputRegisters(modbusAddress, quantity);
      } else {
        result = 0xE1;
        response["Message"] = "不支援的功能碼: " + String(functionCode);
        break;
      }

      if (result == node.ku8MBSuccess) break;
      retries--;
      delay(100);
    }

    if (result == node.ku8MBSuccess) {
      response["Status"] = "success";
      JSONVar dataArray;
      for (uint8_t i = 0; i < quantity; i++) {
        dataBuffer[i] = node.getResponseBuffer(i);
        dataArray[i] = dataBuffer[i];
      }
      response["Data"] = dataArray;
      Serial.println("[Modbus讀取] 成功，數據: " + String(JSON.stringify(dataArray)));
      Serial.println(" ");
    } else {
      response["Status"] = "error";
      if (!response.hasOwnProperty("Message")) {
        response["Message"] = "讀取 Modbus 失敗，錯誤碼: 0x" + String(result, HEX);
      }
      Serial.println("[Modbus讀取] 失敗，錯誤碼: 0x" + String(result, HEX));
      Serial.println(" ");
    }
  }

  String responseString = JSON.stringify(response);
  client.publish(topic_modbus_read_resp, responseString.c_str());
}

// ================================================================
// 18. MQTT Modbus 寫入
// ================================================================
void handleMqttWriteModbus(byte* payload, unsigned int length) {
  JSONVar response;
  char payloadStr[length + 1];
  memcpy(payloadStr, payload, length);
  payloadStr[length] = '\0';

  JSONVar request = JSON.parse(payloadStr);
  if (JSON.typeof(request) == "undefined") {
    Serial.println("[Modbus寫入] JSON 解析失敗");
    response["Status"] = "error";
    response["Message"] = "無效的 JSON payload";
    client.publish(topic_modbus_write_resp, JSON.stringify(response).c_str());
    return;
  }

  if (!request.hasOwnProperty("slaveId") ||
      !request.hasOwnProperty("address") ||
      (!request.hasOwnProperty("value") && !request.hasOwnProperty("values"))) {
    Serial.println("[Modbus寫入] JSON 缺少必要欄位");
    response["Status"] = "error";
    response["Message"] = "缺少必要參數 (slaveId, address, value 或 values)";
    client.publish(topic_modbus_write_resp, JSON.stringify(response).c_str());
    return;
  }

  uint8_t slaveId = (int)request["slaveId"];
  uint16_t address = (int)request["address"] + Address_Offset;

  if (slaveId < 1 || slaveId > MAX_SLAVES) {
    Serial.println("[Modbus寫入] 無效的 slaveId: " + String(slaveId));
    response["Status"] = "error";
    response["SlaveId"] = slaveId;
    response["Message"] = "slaveId 必須介於 1 和 " + String(MAX_SLAVES) + " 之間";
    client.publish(topic_modbus_write_resp, JSON.stringify(response).c_str());
    return;
  }

  if (request.hasOwnProperty("values")) {
    JSONVar values = request["values"];
    uint16_t quantity = values.length();

    node.begin(slaveId, Serial2);
    uint8_t result = node.ku8MBSuccess;

    Serial.println("[Modbus寫入] 改為逐筆單寫, 起始位址 " + String(address));

    for (int i = 0; i < quantity; i++) {
      uint16_t val = (int)values[i];

      Serial.println("[Modbus寫入] 準備寫入 -> Addr: " + String(address + i) +
                     " Val: " + String(val));

      uint8_t retries = 3;
      uint8_t writeResult = 0xFF;

      while (retries > 0) {
        Serial.println("[Modbus寫入] 嘗試寫入 (retry=" + String(3 - retries) + ")");

        writeResult = node.writeSingleRegister(address + i, val);

        if (writeResult == node.ku8MBSuccess) {
          Serial.println("[Modbus寫入] 成功 -> Addr: " + String(address + i) +
                         " Val: " + String(val));
          break;
        }

        Serial.println("[Modbus寫入] 失敗，錯誤碼: 0x" + String(writeResult, HEX));
        retries--;
        delay(50);
      }

      if (writeResult != node.ku8MBSuccess) {
        result = writeResult;
        Serial.println("[Modbus寫入] 最終失敗 at index " + String(i));
        break;
      }

      delay(10);
    }

    if (result == node.ku8MBSuccess) {
      response["Status"] = "success";
      response["SlaveId"] = slaveId;
      response["Message"] = "逐筆寫入成功";
    } else {
      response["Status"] = "error";
      response["SlaveId"] = slaveId;
      response["Message"] = "逐筆寫入失敗，錯誤碼: 0x" + String(result, HEX);
    }

    client.publish(topic_modbus_write_resp, JSON.stringify(response).c_str());
    return;
  }

  uint16_t value = (int)request["value"];

  node.begin(slaveId, Serial2);
  uint8_t result = 0xFF;

  Serial.println("[Modbus寫入] 開始寫入從站 " + String(slaveId) +
                 ", 位址 " + String(address) +
                 ", 值 " + String(value));

  uint8_t retries = 3;
  while (retries > 0) {
    result = node.writeSingleRegister(address, value);
    if (result == node.ku8MBSuccess) break;
    retries--;
    delay(100);
  }

  if (result == node.ku8MBSuccess) {
    response["Status"] = "success";
    response["SlaveId"] = slaveId;
    response["Message"] = "寫入成功";
    Serial.println("[Modbus寫入] 成功");
  } else {
    response["Status"] = "error";
    response["SlaveId"] = slaveId;
    response["Message"] = "寫入失敗，錯誤碼: 0x" + String(result, HEX);
    Serial.println("[Modbus寫入] 失敗，錯誤碼: 0x" + String(result, HEX));
  }

  client.publish(topic_modbus_write_resp, JSON.stringify(response).c_str());
}
