/*
 * ESP32 Modbus Master RS485 控制程式範例
 * 
 * 功能:
 * - 通過 RS485 主機與最多 5 個 Modbus 從站通訊
 * - 通過 MQTT 接收讀寫命令
 * - 通過 MQTT 回傳讀寫結果
 * 
 * 硬件連接:
 * - RS485 模塊的 TX/RX 連接到 ESP32 的 GPIO
 * - GPIO16 - RS485 RXD (U2RXD)
 * - GPIO17 - RS485 TXD (U2TXD)
 * - GPIO5  - RS485 DE 和 RE (方向控制)
 * 
 * MQTT 主題格式:
 * - 讀取請求: devices/{DEVICE_ID}/modbus/read/request
 * - 讀取回應: devices/{DEVICE_ID}/modbus/read/response
 * - 寫入請求: devices/{DEVICE_ID}/modbus/write/request
 * - 寫入回應: devices/{DEVICE_ID}/modbus/write/response
 */

#include <Arduino.h>
#include <WiFi.h>
#include <PubSubClient.h>
#include <ArduinoJson.h>
#include <ModbusMaster.h>  // 使用 Arduino-Modbus 或 ArduinoModbus 庫

// Wi-Fi 配置
const char* WIFI_SSID = "YOUR_SSID";
const char* WIFI_PASSWORD = "YOUR_PASSWORD";

// MQTT 配置
const char* MQTT_BROKER = "192.168.1.100";  // MQTT Broker IP
const int MQTT_PORT = 1883;
const char* MQTT_DEVICE_ID = "ESP32_TEST_RS485";

// RS485 配置
#define RS485_RXD 16   // U2RXD
#define RS485_TXD 17   // U2TXD
#define RS485_DE 5     // Direction Enable
#define RS485_RE 5     // Receiver Enable (可與 DE 相同)

// 常量
#define MODBUS_BAUD 9600
#define MODBUS_TIMEOUT 2000  // 毫秒
#define MAX_SLAVES 247  // Modbus 最大從站數

// 全局變量
WiFiClient wifiClient;
PubSubClient mqttClient(wifiClient);
HardwareSerial rs485Serial(2);  // UART2
ModbusMaster slave;
String deviceId = MQTT_DEVICE_ID;

// 設備結構體
struct ModbusDevice {
    uint8_t slaveId;
    bool active;
    uint32_t lastPoll;
    uint8_t failureCount;
};

// 使用動態或靜態列表存儲從站（支持任意數量）
ModbusDevice slaves[247];  // 支持全部 Modbus 從站 ID (1-247)

// 前向聲明
void connectToWiFi();
void connectToMqtt();
void handleMqttMessage(char* topic, byte* payload, unsigned int length);
void setupModbus();
void preTransmission();
void postTransmission();
uint16_t readFromSlave(uint8_t slaveId, uint16_t address, uint16_t quantity, uint8_t functionCode);
bool writeToSlave(uint8_t slaveId, uint16_t address, uint16_t value);
void publishModbusReadResponse(uint8_t slaveId, uint16_t address, uint16_t quantity, uint16_t* data, uint8_t dataLength, bool success, String errorMsg = "");
void publishModbusWriteResponse(uint8_t slaveId, bool success, String message = "");

void setup() {
    Serial.begin(115200);
    delay(1000);

    Serial.println("\n\nESP32 Modbus Master RS485 啟動");
    Serial.println("================================");

    // 初始化 LED 和控制引腳
    pinMode(LED_BUILTIN, OUTPUT);
    digitalWrite(LED_BUILTIN, LOW);

    // 初始化 RS485
    setupModbus();

    // 連接 Wi-Fi
    connectToWiFi();

    // 配置 MQTT
    mqttClient.setServer(MQTT_BROKER, MQTT_PORT);
    mqttClient.setCallback(handleMqttMessage);
    connectToMqtt();

    Serial.println("ESP32 初始化完成");
}

void loop() {
    // 保持 Wi-Fi 連接
    if (WiFi.status() != WL_CONNECTED) {
        Serial.println("Wi-Fi 連接中斷，重新連接...");
        connectToWiFi();
    }

    // 保持 MQTT 連接
    if (!mqttClient.connected()) {
        if (!connectToMqtt()) {
            delay(5000);
        }
    }

    // 處理 MQTT 消息
    mqttClient.loop();

    // 定期輪詢從站（可選）
    pollAllSlaves();

    delay(100);
}

void connectToWiFi() {
    Serial.print("連接到 Wi-Fi: ");
    Serial.println(WIFI_SSID);

    WiFi.mode(WIFI_STA);
    WiFi.begin(WIFI_SSID, WIFI_PASSWORD);

    int attempts = 0;
    while (WiFi.status() != WL_CONNECTED && attempts < 20) {
        delay(500);
        Serial.print(".");
        attempts++;
    }

    if (WiFi.status() == WL_CONNECTED) {
        Serial.println("\n✓ Wi-Fi 已連接");
        Serial.print("IP 地址: ");
        Serial.println(WiFi.localIP());
        digitalWrite(LED_BUILTIN, HIGH);
    } else {
        Serial.println("\n✗ Wi-Fi 連接失敗");
        digitalWrite(LED_BUILTIN, LOW);
    }
}

bool connectToMqtt() {
    Serial.print("連接到 MQTT 代理: ");
    Serial.print(MQTT_BROKER);
    Serial.print(":");
    Serial.println(MQTT_PORT);

    String clientId = "ESP32-" + String(random(0xffff), HEX);

    if (mqttClient.connect(clientId.c_str())) {
        Serial.println("✓ MQTT 已連接");

        // 訂閱命令主題
        String readTopic = "devices/" + deviceId + "/modbus/read/request";
        String writeTopic = "devices/" + deviceId + "/modbus/write/request";

        mqttClient.subscribe(readTopic.c_str());
        mqttClient.subscribe(writeTopic.c_str());

        Serial.print("已訂閱: ");
        Serial.println(readTopic);
        Serial.print("已訂閱: ");
        Serial.println(writeTopic);

        // 發布上線消息
        String onlineTopic = "devices/" + deviceId + "/status";
        String onlinePayload = "{\"status\":\"online\",\"deviceId\":\"" + deviceId + "\"}";
        mqttClient.publish(onlineTopic.c_str(), onlinePayload.c_str());

        return true;
    } else {
        Serial.print("✗ MQTT 連接失敗，代碼: ");
        Serial.println(mqttClient.state());
        return false;
    }
}

void handleMqttMessage(char* topic, byte* payload, unsigned int length) {
    // 將 payload 轉換為字符串
    String message = "";
    for (unsigned int i = 0; i < length; i++) {
        message += (char)payload[i];
    }

    Serial.print("收到 MQTT 消息 - 主題: ");
    Serial.println(topic);
    Serial.print("負載: ");
    Serial.println(message);

    // 解析 JSON
    JsonDocument doc;
    DeserializationError error = deserializeJson(doc, message);

    if (error) {
        Serial.print("JSON 解析失敗: ");
        Serial.println(error.c_str());
        return;
    }

    // 判斷是讀取還是寫入
    String topicStr = String(topic);
    if (topicStr.endsWith("modbus/read/request")) {
        handleReadRequest(doc);
    } else if (topicStr.endsWith("modbus/write/request")) {
        handleWriteRequest(doc);
    }
}

void handleReadRequest(JsonDocument& doc) {
    uint8_t slaveId = doc["slaveId"];
    uint16_t address = doc["address"];
    uint16_t quantity = doc["quantity"];
    uint8_t functionCode = doc["functionCode"] | 3;  // 默認為功能碼 3

    Serial.print("讀取請求 - Slave: ");
    Serial.print(slaveId);
    Serial.print(", Address: ");
    Serial.print(address);
    Serial.print(", Quantity: ");
    Serial.println(quantity);

    // 執行 Modbus 讀取
    bool success = false;
    uint16_t data[125];
    uint8_t dataLength = 0;
    String errorMsg = "";

    if (functionCode == 3) {
        // 讀保持寄存器
        slave.begin(slaveId, rs485Serial);
        uint8_t result = slave.readHoldingRegisters(address, quantity);

        if (result == slave.ku8MBSuccess) {
            success = true;
            dataLength = slave.getResponseBuffer(0, data, quantity);
        } else {
            errorMsg = "Modbus 讀取失敗: " + String(result);
        }
    }

    publishModbusReadResponse(slaveId, address, quantity, data, dataLength, success, errorMsg);
}

void handleWriteRequest(JsonDocument& doc) {
    uint8_t slaveId = doc["slaveId"];
    uint16_t address = doc["address"];

    Serial.print("寫入請求 - Slave: ");
    Serial.print(slaveId);
    Serial.print(", Address: ");
    Serial.println(address);

    bool success = false;
    String message = "";

    // 檢查是單個值還是多個值
    if (doc.containsKey("value")) {
        // 單個值寫入
        uint16_t value = doc["value"];
        slave.begin(slaveId, rs485Serial);
        uint8_t result = slave.writeSingleRegister(address, value);

        if (result == slave.ku8MBSuccess) {
            success = true;
            message = "寫入成功";
        } else {
            message = "寫入失敗: " + String(result);
        }
    } else if (doc.containsKey("values")) {
        // 多個值寫入
        JsonArray values = doc["values"];
        uint16_t data[125];
        uint8_t dataLength = values.size();

        for (uint8_t i = 0; i < dataLength && i < 125; i++) {
            data[i] = values[i];
        }

        slave.begin(slaveId, rs485Serial);
        slave.setTransmitBuffer(0, data, dataLength);
        uint8_t result = slave.writeMultipleRegisters(address, dataLength);

        if (result == slave.ku8MBSuccess) {
            success = true;
            message = "寫入成功";
        } else {
            message = "寫入失敗: " + String(result);
        }
    }

    publishModbusWriteResponse(slaveId, success, message);
}

void setupModbus() {
    Serial.println("初始化 RS485 Modbus Master...");

    // 配置 RS485 UART
    rs485Serial.begin(MODBUS_BAUD, SERIAL_8N1, RS485_RXD, RS485_TXD);

    // 配置方向控制引腳
    pinMode(RS485_DE, OUTPUT);
    pinMode(RS485_RE, OUTPUT);
    digitalWrite(RS485_DE, LOW);
    digitalWrite(RS485_RE, LOW);

    // 配置 ModbusMaster
    slave.preTransmission(preTransmission);
    slave.postTransmission(postTransmission);
    slave.begin(1, rs485Serial);  // 初始從站 ID 為 1
}

void preTransmission() {
    // 發送前：設置為發送模式
    digitalWrite(RS485_DE, HIGH);
    digitalWrite(RS485_RE, HIGH);
    delayMicroseconds(100);
}

void postTransmission() {
    // 發送後：設置為接收模式
    delayMicroseconds(100);
    digitalWrite(RS485_DE, LOW);
    digitalWrite(RS485_RE, LOW);
}

void pollAllSlaves() {
    // 可選：定期輪詢所有活躍的從站
    // 這個例子中，輪詢由 MQTT 命令觸發

    static unsigned long lastPoll = 0;
    if (millis() - lastPoll < 5000) return;  // 每 5 秒輪詢一次
    lastPoll = millis();

    for (int i = 1; i <= 247; i++) {
        if (slaves[i-1].active) {
            Serial.print("輪詢從站 ");
            Serial.println(slaves[i-1].slaveId);

            // 輪詢狀態寄存器（地址 0，1 個寄存器）
            // 實際應用中應根據設備特定調整
        }
    }
}

void publishModbusReadResponse(uint8_t slaveId, uint16_t address, uint16_t quantity, uint16_t* data, uint8_t dataLength, bool success, String errorMsg) {
    String responseTopic = "devices/" + deviceId + "/modbus/read/response";

    JsonDocument doc;
    doc["deviceId"] = deviceId;
    doc["slaveId"] = slaveId;
    doc["status"] = success ? "success" : "error";
    doc["message"] = errorMsg;
    doc["address"] = address;
    doc["quantity"] = quantity;
    doc["functionCode"] = 3;

    if (success) {
        JsonArray dataArray = doc.createNestedArray("data");
        for (uint8_t i = 0; i < dataLength; i++) {
            dataArray.add(data[i]);
        }
    }

    String payload;
    serializeJson(doc, payload);

    Serial.print("發布讀取回應: ");
    Serial.println(payload);

    mqttClient.publish(responseTopic.c_str(), payload.c_str());
}

void publishModbusWriteResponse(uint8_t slaveId, bool success, String message) {
    String responseTopic = "devices/" + deviceId + "/modbus/write/response";

    JsonDocument doc;
    doc["deviceId"] = deviceId;
    doc["slaveId"] = slaveId;
    doc["status"] = success ? "success" : "error";
    doc["message"] = message;

    String payload;
    serializeJson(doc, payload);

    Serial.print("發布寫入回應: ");
    Serial.println(payload);

    mqttClient.publish(responseTopic.c_str(), payload.c_str());
}

/*
 * 使用所需的 Arduino 庫:
 * 
 * 1. PubSubClient (by Nick O'Leary)
 *    - 用於 MQTT 連接
 * 
 * 2. ArduinoJson (by Benoit Blanchon)
 *    - 用於 JSON 序列化/反序列化
 * 
 * 3. ModbusMaster (by Doc Walker) 或 Arduino-Modbus (by Rob Tillaart)
 *    - 用於 Modbus RTU 主機通訊
 * 
 * 在 Arduino IDE 中通過 Library Manager 安裝
 */
