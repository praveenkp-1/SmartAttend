/*
 * SmartAttend ESP32-S3 firmware
 *
 * TFT/SD/Touch SPI : MOSI=11, MISO=13, SCK=12
 * RFID SPI         : MOSI=5,  MISO=15, SCK=4
 * TFT  CS=10  DC=9   RST=8
 * TOUCH CS=14
 * RFID CS=17  RST=18
 * SD   CS=6

 * FP   RX=44  TX=43   (AS608 / UART fingerprint sensor)
 * BUZ  GPIO=16
 */

#include <Arduino.h>
#include <SPI.h>
#include <TFT_eSPI.h>
#include <MFRC522.h>
#include <SD.h>
#include <WiFi.h>
#include <WebServer.h>
#include <ArduinoJson.h>
#include <Wire.h>
#include <RTClib.h>
#include <time.h>
#include <Adafruit_Fingerprint.h>
#include "ui.h"
#include "storage.h"
#include "network.h"
#include <ESPmDNS.h>

// ── Pin definitions ────────────────────────────────────
#define RFID_SS 17
#define RFID_RST 18
#define SD_CS 6
#define TFT_CS_PIN 10
#define TOUCH_CS_PIN 14
#define BUZZER_PIN 16
#define FACTORY_RESET_BUTTON_PIN 42
#define FP_RX_PIN 44
#define FP_TX_PIN 43
#define FP_BAUD 57600

#define RFID_SPI_SCK 4
#define RFID_SPI_MISO 15
#define RFID_SPI_MOSI 5
#define RFID_SPI_FREQUENCY 400000

// ── Master admin PIN — stored in config, default 1234 ─
#define DEFAULT_MASTER_PIN "1234"

// ── Admin session timeout (30 seconds of no touch) ────
#define ADMIN_TIMEOUT_MS 30000
#define WIFI_RETRY_INTERVAL_MS 30000
#define WIFI_CONNECT_GRACE_MS 3000
#define SYNC_INTERVAL_MS 30000
#define MAX_SYNC_PER_PASS 2
#define LOCAL_GMT_OFFSET_SEC 19800
#define LOCAL_DAYLIGHT_OFFSET_SEC 0
#define FACTORY_RESET_HOLD_MS 5000

// ── Global objects ─────────────────────────────────────
TFT_eSPI tft = TFT_eSPI();
MFRC522 rfid(RFID_SS, RFID_RST);
WebServer server(5001);
HardwareSerial fpSerial(2);
Adafruit_Fingerprint finger(&fpSerial);

// ── State ──────────────────────────────────────────────
bool wifiConnected = false;
bool sdReady = false;
bool rfidReady = false;
bool fpReady = false;

AttendanceSettings settings;
RTC_DS3231 rtc;
bool rtcReady = false;

String deviceConfig = "";
AppState currentState = STATE_HOME;

// ── Timing ────────────────────────────────────────────
unsigned long lastWifiCheck = 0;
unsigned long lastWifiAttempt = 0;
unsigned long lastClockUpdate = 0;
unsigned long lastSyncCheck = 0;
unsigned long feedbackUntil = 0;
unsigned long adminLastAction = 0;
bool wifiReconnectInProgress = false;
String lastAbsentDate = "";
bool factoryResetPressed = false;
bool factoryResetHoldHandled = false;
unsigned long factoryResetPressStart = 0;

// ── Forward declarations ───────────────────────────────
void initSPI();
void initSD();
void initRFID();
void initFingerprint();
void connectWiFi();
void connectWiFiWithCreds(String ssid, String pass);
void checkWiFi();
void setupHttpServer();
void handleFactoryResetButton();
void runFactoryResetFlow();
void handleRFIDScan();
void handlePINAttendance();
void handleFingerprintScan();
bool authStartsWithRFID(AuthMode mode);
bool authStartsWithPIN(AuthMode mode);
bool authStartsWithFP(AuthMode mode);
bool waitForRFIDCard(String& cardId, unsigned long timeoutMs);
int findNextFingerprintId();
int readFingerprintId();
bool enrollFingerprintId(int id);
void markAbsencesIfDue();
String attendanceStatusForCheckIn(String checkIn);
String calculateWorkedHours(String checkIn, String checkOut);
int minutesFromHHMM(String value);
void syncOfflineRecords();
void syncDeviceUpdates();
bool applyDeviceUpdate(DeviceUpdate &update);
void handleTouch();
void runAdminFlow();
void runEnrollFlow(bool pendingOnly = true);
void buzzSuccess();
void buzzFail();
void buzzWelcome();
void buzzBtnPress();
void syncRtcFromNtp();
String getDateStr();
String getTimeStr();
String getDisplayDateStr();
String getMasterPIN();
void saveMasterPIN(String pin);
void startAPMode(); // new
void startMdns();

// ── WiFi credentials fallback ──────────────────────────
// Replace with:
#define AP_SSID "SmartAttend-Setup"
#define AP_IP "192.168.4.1"
#define MDNS_HOSTNAME "smartattend"
#define DEVICE_NAME "SmartAttend-001"
bool apModeActive = false;

// ══════════════════════════════════════════════════════
void setup()
{
    Serial.begin(115200);
    unsigned long t0 = millis();
    while (!Serial && millis() - t0 < 3000)
        delay(10);

    Serial.println("SmartAttend booting...");

    Serial.println("Before Wire.begin");
    Wire.begin(3, 7);
    Serial.println("After Wire.begin");

    Serial.println("Before rtc.begin");
    bool rtcOk = rtc.begin();
    rtcReady = rtcOk;
    Serial.println("After rtc.begin");

    if (!rtcOk)
    {
        Serial.println("[RTC] DS3231 not found");
    }
    else
    {
        Serial.println("[RTC] DS3231 ready");

        DateTime now = rtc.now();

        Serial.printf(
            "RTC=%04d-%02d-%02d %02d:%02d:%02d\n",
            now.year(),
            now.month(),
            now.day(),
            now.hour(),
            now.minute(),
            now.second());
    }

    // Buzzer init
    pinMode(BUZZER_PIN, OUTPUT);
    digitalWrite(BUZZER_PIN, LOW);

    buzzWelcome();

    pinMode(FACTORY_RESET_BUTTON_PIN, INPUT_PULLUP);

    initSPI();
    initRFID();
    initFingerprint();

    UI_Init(tft);
    UI_InitTouch(tft);
    UI_ShowBoot(tft);

    initSD();

    Serial.println("Settings file exists?");
    Serial.println(
        SD.exists("/settings.json")
            ? "YES"
            : "NO");

    DeviceConfig cfg;
    cfg.wifiSSID = "";
    cfg.wifiPassword = "";
    // cfg.boundAppIP = "";
    cfg.deviceName = DEVICE_NAME;

    if (sdReady)
    {
        // cfg = Storage_LoadConfig();
        Serial.println("Before LoadConfig");
        cfg = Storage_LoadConfig();
        Serial.println("After LoadConfig");

        settings = Storage_LoadSettings();
        deviceConfig = settings.serverIP;

        if (!SD.exists("/sync.json"))
        {
            Serial.println("[SD] sync.json not found — creating default");
            Storage_SaveLastDeviceSyncId(0);
        }
    }

    if (!cfg.wifiSSID.isEmpty())
    {
        // Has saved credentials → normal STA mode
        connectWiFiWithCreds(cfg.wifiSSID, cfg.wifiPassword);
    }
    else
    {
        // No credentials → AP mode for first time setup
        startAPMode();
    }

    setupHttpServer();
    server.begin();

    // Show correct screen based on mode
    if (apModeActive)
    {
        currentState = STATE_AP_MODE;
        UI_ShowAPMode(tft); // ← setup screen
    }
    else
    {
        currentState = STATE_HOME;
        UI_ShowHome(tft, wifiConnected, settings.authMode,
                    getTimeStr(), getDisplayDateStr()); // ← normal home screen
    }
}

// ══════════════════════════════════════════════════════
void loop()
{
    server.handleClient();
    handleFactoryResetButton();

    // ── WiFi watchdog ──────────────────────────────────
    if (millis() - lastWifiCheck > 10000)
    {
        lastWifiCheck = millis();
        checkWiFi();
    }

    // ── Clock update on home screen ────────────────────
    if (!apModeActive &&
        currentState == STATE_HOME &&
        millis() - lastClockUpdate > 1000)
    {
        lastClockUpdate = millis();
        UI_UpdateClock(tft, getTimeStr(), getDisplayDateStr());
        UI_UpdateWifiDot(tft, wifiConnected);
    }

    // ── Return to home after feedback ─────────────────
    if (currentState != STATE_HOME &&
        currentState != STATE_AP_MODE &&
        currentState != STATE_ADMIN_PIN &&
        currentState != STATE_ADMIN_MENU &&
        currentState != STATE_ENROLL_LIST &&
        currentState != STATE_ENROLL_MENU &&
        currentState != STATE_ENROLL_RFID &&
        currentState != STATE_ENROLL_PIN &&
        currentState != STATE_CHANGE_PIN &&
        feedbackUntil > 0 &&
        millis() > feedbackUntil)
    {
        feedbackUntil = 0;
        currentState = STATE_HOME;
        UI_ShowHome(tft, wifiConnected, settings.authMode,
                    getTimeStr(), getDisplayDateStr());
    }

    // ── Offline sync every 30s ─────────────────────────
    if (!apModeActive &&
        wifiConnected && millis() - lastSyncCheck > SYNC_INTERVAL_MS)
    {
        lastSyncCheck = millis();
        syncDeviceUpdates();
        syncOfflineRecords();
    }

    if (!apModeActive && currentState == STATE_HOME)
        markAbsencesIfDue();

    // ── RFID scan on home screen ───────────────────────
    if (!apModeActive &&
        currentState == STATE_HOME &&
        authStartsWithRFID(settings.authMode))
        handleRFIDScan();

    // â”€â”€ Fingerprint scan on home screen â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    if (!apModeActive &&
        currentState == STATE_HOME &&
        authStartsWithFP(settings.authMode))
        handleFingerprintScan();

    // ── Touch handler on home screen ───────────────────
    if (!apModeActive && currentState == STATE_HOME)
        handleTouch();
}

// ══════════════════════════════════════════════════════
// TOUCH — detects ADMIN button tap on home screen
// ══════════════════════════════════════════════════════
void handleTouch()
{
    TouchResult t = UI_GetTouch(tft);
    if (!t.pressed)
        return;

    // ADMIN button zone — bottom strip of home screen
    // y > 285 covers the "SMARTATTEND v2.0" footer area
    if (UI_IsTapped(t, 198, 286, 32, 28))
    {
        runAdminFlow();
    }
    else if (authStartsWithPIN(settings.authMode) &&
             UI_IsTapped(t, 22, 198, 196, 66))
    {
        handlePINAttendance();
    }
}

void handleFactoryResetButton()
{
    if (currentState == STATE_FACTORY_RESET)
        return;

    bool pressed = digitalRead(FACTORY_RESET_BUTTON_PIN) == LOW;
    unsigned long now = millis();

    if (pressed && !factoryResetPressed)
    {
        Serial.println("Reset button pressed");
        factoryResetPressed = true;
        factoryResetHoldHandled = false;
        factoryResetPressStart = now;
        
    }

    if (!pressed)
    {
        if (factoryResetPressed)
            factoryResetPressed = false;
        factoryResetHoldHandled = false;
        factoryResetPressStart = 0;
        return;
    }

    if (!factoryResetHoldHandled &&
        now - factoryResetPressStart >= FACTORY_RESET_HOLD_MS)
    {
        Serial.println("Factory reset triggered");
        factoryResetHoldHandled = true;
        runFactoryResetFlow();
    }
}

void runFactoryResetFlow()
{
    currentState = STATE_FACTORY_RESET;

    int unsynced = sdReady ? Storage_GetUnsyncedCount() : -1;
    bool confirmed = UI_ShowFactoryResetConfirm(tft, unsynced);

    if (!confirmed)
    {
        currentState = apModeActive ? STATE_AP_MODE : STATE_HOME;
        if (apModeActive)
            UI_ShowAPMode(tft);
        else
            UI_ShowHome(tft, wifiConnected, settings.authMode,
                        getTimeStr(), getDisplayDateStr());
        return;
    }
    UI_ShowProcessing(tft);
    Storage_FactoryReset();
    delay(500);
    ESP.restart();
}

bool authStartsWithRFID(AuthMode mode)
{
    return mode == AUTH_ANY ||
           mode == AUTH_RFID ||
           mode == AUTH_PIN_RFID;
}

bool authStartsWithPIN(AuthMode mode)
{
    return mode == AUTH_ANY ||
           mode == AUTH_PIN;
}

bool authStartsWithFP(AuthMode mode)
{
    return mode == AUTH_ANY ||
           mode == AUTH_FP ||
           mode == AUTH_FP_RFID ||
           mode == AUTH_FP_PIN;
}

int minutesFromHHMM(String value)
{
    value.trim();
    if (value.length() < 5)
        return -1;

    int colon = value.indexOf(':');
    if (colon < 0)
        return -1;

    int hours = value.substring(0, colon).toInt();
    int minutes = value.substring(colon + 1, colon + 3).toInt();
    if (hours < 0 || hours > 23 || minutes < 0 || minutes > 59)
        return -1;

    return hours * 60 + minutes;
}

String attendanceStatusForCheckIn(String checkIn)
{
    int checkInMin = minutesFromHHMM(checkIn);
    int startMin = minutesFromHHMM(settings.workStart);
    if (checkInMin < 0 || startMin < 0)
        return "Present";

    int lateAfter = startMin + settings.gracePeriod;
    return checkInMin > lateAfter ? "Late" : "Present";
}

String calculateWorkedHours(String checkIn, String checkOut)
{
    int inMin = minutesFromHHMM(checkIn);
    int outMin = minutesFromHHMM(checkOut);
    if (inMin < 0 || outMin < 0)
        return "";

        
    int diff = outMin - inMin;
    if (diff < 0)
        diff += 24 * 60;
    if (diff <= 0)
        return "";

    return String(diff / 60) + "h " + String(diff % 60) + "m";
}

void markAbsencesIfDue()
{
    if (!sdReady)
        return;

    String today = getDateStr();

    if (today == lastAbsentDate)
        return;

    int nowMin = minutesFromHHMM(getTimeStr());
    int workEndMin = minutesFromHHMM(settings.workEnd);
    if (nowMin < 0 || workEndMin < 0 || nowMin < workEndMin)
        return;

    auto employees = Storage_GetAllEmployees();
    for (auto &emp : employees)
    {
        if (Storage_HasCheckedInToday(emp.userId, today))
            continue;

        AttendanceRecord rec;
        rec.userId = emp.userId;
        rec.name = emp.name;
        rec.cardId = emp.userId;
        rec.method = "-";
        rec.checkIn = "-";
        rec.checkOut = "-";
        rec.hours = "-";
        rec.date = today;
        rec.status = "Absent";
        rec.lateMinutes = 0;
        rec.synced = false;
        Storage_SaveAttendance(rec);
    }

    lastAbsentDate = today;
    syncOfflineRecords();
}

// ══════════════════════════════════════════════════════
// ADMIN FLOW — full state machine
// ══════════════════════════════════════════════════════
void runAdminFlow()
{
    currentState = STATE_ADMIN_PIN;

    // Step 1 — verify master PIN
    String enteredPIN = "";
    String masterPIN = getMasterPIN();

    bool ok = UI_ShowAdminPIN(tft, "Admin Access", enteredPIN);

    if (!ok)
    {
        currentState = STATE_HOME;
        UI_ShowHome(tft, wifiConnected, settings.authMode,
                    getTimeStr(), getDisplayDateStr());
        return;
    }

    if (enteredPIN != masterPIN)
    {
        buzzFail();
        currentState = STATE_FAIL;
        UI_ShowWrongPin(tft);
        feedbackUntil = millis() + 1000;
        return;
    }

    buzzSuccess();
    adminLastAction = millis();

    // Step 2 — admin menu loop
    while (true)
    {
        // Admin session timeout
        if (millis() - adminLastAction > ADMIN_TIMEOUT_MS)
        {
            Serial.println("[ADMIN] Session timeout");
            break;
        }

        currentState = STATE_ADMIN_MENU;
        int choice = UI_ShowAdminMenu(tft);
        adminLastAction = millis();

        if (choice == 0)
        {
            // Enroll new user
            runEnrollFlow(true);
            adminLastAction = millis();
        }
        else if (choice == 1)
        {
            // Manage existing employees
            runEnrollFlow(false);
            adminLastAction = millis();
        }
        else if (choice == 2)
        {
            // Change master PIN
            currentState = STATE_CHANGE_PIN;
            String newPIN = "";
            bool changed = UI_ShowChangeMasterPIN(
                tft, masterPIN, newPIN);

            if (changed && !newPIN.isEmpty())
            {
                saveMasterPIN(newPIN);
                masterPIN = newPIN;
                buzzSuccess();
                UI_ShowEnrollSuccess(tft, "Master PIN", "Updated");
                delay(2500);
            }
            adminLastAction = millis();
        }
        else
        {
            // Back to home
            break;
        }
    }

    currentState = STATE_HOME;
    UI_ShowHome(tft, wifiConnected, settings.authMode,
                getTimeStr(), getDisplayDateStr());
}

// ══════════════════════════════════════════════════════
// ENROLL FLOW — fetch pending list, enroll methods
// ══════════════════════════════════════════════════════
void runEnrollFlow(bool pendingOnly)
{

    // Load employees from local SD. Online sync refreshes this first.
    currentState = STATE_ENROLL_LIST;

    PendingEmployee empList[20];
    int empCount = 0;

    settings = Storage_LoadSettings();
    deviceConfig = settings.serverIP;

    if (wifiConnected &&
        !settings.serverIP.isEmpty())
    {
        Serial.println("Starting device update sync");
        syncDeviceUpdates();
    }

    if (pendingOnly)
    {
        empCount = Storage_GetEmployees(empList, 20, true);
    }
    else
    {
        String query = "";
        if (!UI_ShowEmployeeSearch(tft, query))
            return;

        empCount = Storage_SearchEmployees(empList, 20, query);
    }

    Serial.print(pendingOnly ? "Local Pending Count = "
                             : "Local Employee Count = ");
    Serial.println(empCount);

    int scrollOffset = 0;

    // Employee selection loop
    while (true)
    {
        int selected = UI_ShowEnrollList(
            tft, empList, empCount, scrollOffset, pendingOnly);

        if (selected == -1)
            return; // back

        PendingEmployee &emp = empList[selected];

        // Enroll method loop for selected employee
        while (true)
        {
            currentState = STATE_ENROLL_MENU;
            int method = UI_ShowEnrollMenu(
                tft, emp.name, emp.userId,
                emp.rfidDone, emp.pinDone, emp.fpDone);

            if (method == 3)
                break; // done / back to list

            if (method == 0)
            {
                // ── Enroll RFID ─────────────────────────
                currentState = STATE_ENROLL_RFID;
                UI_ShowEnrollRFID(tft, emp.name);

                // Wait for RFID scan (max 15 seconds)
                String scannedCard = "";
                unsigned long wait = millis();

                while (millis() - wait < 15000)
                {
                    // Check cancel button touch
                    TouchResult t = UI_GetTouch(tft);
                    if (UI_IsTapped(t, 70, 282, 100, 26))
                        break; // cancelled

                    // Try RFID read
                    if (rfidReady)
                    {
                        if (rfid.PICC_IsNewCardPresent() &&
                            rfid.PICC_ReadCardSerial())
                        {
                            for (byte i = 0; i < rfid.uid.size; i++)
                            {
                                if (rfid.uid.uidByte[i] < 0x10)
                                    scannedCard += "0";
                                scannedCard += String(
                                    rfid.uid.uidByte[i], HEX);
                            }
                            scannedCard.toUpperCase();
                            rfid.PICC_HaltA();
                            rfid.PCD_StopCrypto1();
                            break;
                        }
                    }
                    delay(100);
                }

                if (scannedCard.isEmpty())
                {
                    UI_ShowEnrollFail(tft, "No card scanned");
                    delay(2000);
                    continue;
                }

                // Save to SD
                EmployeeRecord rec;
                rec.userId = emp.userId;
                rec.name = emp.name;
                rec.department = emp.department;
                rec.rfidCard = scannedCard;
                rec.pin = "";
                rec.fingerprintId = -1;
                Storage_SaveEmployee(rec);

                // Notify app
                bool sent = false;
                if (wifiConnected && !deviceConfig.isEmpty())
                    sent = Network_RegisterRFID(
                        deviceConfig, scannedCard, emp.userId);

                emp.rfidDone = true;
                buzzSuccess();
                currentState = STATE_ENROLL_SUCCESS;
                UI_ShowEnrollSuccess(tft, emp.name, "RFID Card");
                delay(2500);
            }
            else if (method == 1)
            {
                // ── Enroll PIN ──────────────────────────
                currentState = STATE_ENROLL_PIN;
                String newPIN = "";
                bool ok = UI_ShowEnrollPINEntry(
                    tft, emp.name, newPIN);

                if (!ok || newPIN.isEmpty())
                    continue;

                EmployeeRecord pinOwner =
                    Storage_FindEmployeeByPIN(newPIN);
                if (!pinOwner.userId.isEmpty() &&
                    pinOwner.userId != emp.userId)
                {
                    buzzFail();
                    UI_ShowEnrollFail(tft, "PIN already used");
                    delay(2000);
                    continue;
                }

                // Save PIN to SD alongside employee record
                EmployeeRecord existing =
                    Storage_FindEmployeeByUserId(emp.userId);
                existing.userId = emp.userId;
                existing.name = emp.name;
                existing.department = emp.department;
                existing.pin = newPIN;
                Storage_SaveEmployee(existing);

                // Notify app
                if (wifiConnected && !deviceConfig.isEmpty())
                    Network_RegisterPIN(
                        deviceConfig, emp.userId, newPIN);

                emp.pinDone = true;
                buzzSuccess();
                currentState = STATE_ENROLL_SUCCESS;
                UI_ShowEnrollSuccess(tft, emp.name, "PIN");
                delay(2500);
            }
            else if (method == 2)
            {
                currentState = STATE_ENROLL_RFID;

                if (!fpReady)
                {
                    buzzFail();
                    UI_ShowEnrollFail(tft, "FP sensor not ready");
                    delay(2000);
                    continue;
                }

                EmployeeRecord existing =
                    Storage_FindEmployeeByUserId(emp.userId);
                int fpId = existing.fingerprintId >= 0
                               ? existing.fingerprintId
                               : findNextFingerprintId();

                if (fpId <= 0)
                {
                    buzzFail();
                    UI_ShowEnrollFail(tft, "No FP slot free");
                    delay(2000);
                    continue;
                }

                if (!enrollFingerprintId(fpId))
                {
                    buzzFail();
                    UI_ShowEnrollFail(tft, "FP enroll failed");
                    delay(2000);
                    continue;
                }

                existing.userId = emp.userId;
                existing.name = emp.name;
                existing.department = emp.department;
                existing.fingerprintId = fpId;
                Storage_SaveEmployee(existing);

                if (wifiConnected && !deviceConfig.isEmpty())
                    Network_RegisterFingerprint(deviceConfig, emp.userId);

                emp.fpDone = true;
                buzzSuccess();
                currentState = STATE_ENROLL_SUCCESS;
                UI_ShowEnrollSuccess(tft, emp.name, "Fingerprint");
                delay(2500);
            }
        }
    }
}

int findNextFingerprintId()
{
    int maxId = 0;
    auto employees = Storage_GetAllEmployees();

    for (auto &emp : employees)
    {
        if (emp.fingerprintId > maxId)
            maxId = emp.fingerprintId;
    }

    int nextId = maxId + 1;
    return nextId > 0 && nextId < 200 ? nextId : -1;
}

bool enrollFingerprintId(int id)
{
    UI_ShowFingerprint(tft, "ENROLL FINGER", "Place finger", "First scan");

    unsigned long started = millis();
    while (finger.getImage() != FINGERPRINT_OK)
    {
        if (millis() - started > 15000)
            return false;
        delay(100);
    }

    if (finger.image2Tz(1) != FINGERPRINT_OK)
        return false;

    UI_ShowFingerprint(tft, "ENROLL FINGER", "Remove finger", "Then scan again");
    delay(1500);

    started = millis();
    while (finger.getImage() != FINGERPRINT_NOFINGER)
    {
        if (millis() - started > 8000)
            return false;
        delay(100);
    }

    UI_ShowFingerprint(tft, "ENROLL FINGER", "Place same finger", "Second scan");

    started = millis();
    while (finger.getImage() != FINGERPRINT_OK)
    {
        if (millis() - started > 15000)
            return false;
        delay(100);
    }

    if (finger.image2Tz(2) != FINGERPRINT_OK)
        return false;
    if (finger.createModel() != FINGERPRINT_OK)
        return false;
    if (finger.storeModel(id) != FINGERPRINT_OK)
        return false;

    return true;
}

int readFingerprintId()
{
    uint8_t result = finger.getImage();
    if (result == FINGERPRINT_NOFINGER)
        return -1;
    if (result != FINGERPRINT_OK)
        return -2;

    UI_ShowFingerprintScanning(tft, "Reading fingerprint", "Keep finger still");

    if (finger.image2Tz() != FINGERPRINT_OK)
        return -2;
    if (finger.fingerFastSearch() != FINGERPRINT_OK)
        return -2;

    return finger.fingerID;
}

bool waitForRFIDCard(String& cardId, unsigned long timeoutMs)
{
    unsigned long started = millis();

    while (millis() - started < timeoutMs)
    {
        if (rfidReady &&
            rfid.PICC_IsNewCardPresent() &&
            rfid.PICC_ReadCardSerial())
        {
            cardId = "";
            for (byte i = 0; i < rfid.uid.size; i++)
            {
                if (rfid.uid.uidByte[i] < 0x10)
                    cardId += "0";
                cardId += String(rfid.uid.uidByte[i], HEX);
            }
            cardId.toUpperCase();
            rfid.PICC_HaltA();
            rfid.PCD_StopCrypto1();
            return true;
        }
        delay(100);
    }

    return false;
}

void handleFingerprintScan()
{
    if (!authStartsWithFP(settings.authMode) || !fpReady)
        return;

    int fpId = readFingerprintId();
    if (fpId == -1)
        return;

    currentState = STATE_SCANNING;
    UI_ShowFingerprintScanning(tft, "Checking fingerprint", "ID " + String(fpId));

    if (!sdReady)
    {
        buzzFail();
        currentState = STATE_FAIL;
        UI_ShowFail(tft, "SD Not Ready", "FP");
        feedbackUntil = millis() + 2000;
        return;
    }

    if (fpId < 0)
    {
        buzzFail();
        currentState = STATE_FAIL;
        UI_ShowFail(tft, "Finger Not Found", "FP");
        feedbackUntil = millis() + 2000;
        return;
    }

    EmployeeRecord emp;
    emp.userId = "";
    auto employees = Storage_GetAllEmployees();

    for (auto &candidate : employees)
    {
        if (candidate.fingerprintId == fpId)
        {
            emp = candidate;
            break;
        }
    }

    if (emp.userId.isEmpty())
    {
        buzzFail();
        currentState = STATE_FAIL;
        UI_ShowFail(tft, "Unknown Finger", "FP");
        feedbackUntil = millis() + 2000;
        return;
    }

    String authMethod = "FP";

    if (settings.authMode == AUTH_FP_PIN)
    {
        if (emp.pin.isEmpty())
        {
            buzzFail();
            currentState = STATE_FAIL;
            UI_ShowFail(tft, "PIN Not Enrolled", "FP");
            feedbackUntil = millis() + 2000;
            return;
        }

        String enteredPIN;
        if (!UI_ShowAttendancePIN(tft, enteredPIN))
        {
            currentState = STATE_HOME;
            UI_ShowHome(tft, wifiConnected, settings.authMode,
                        getTimeStr(), getDisplayDateStr());
            return;
        }

        if (enteredPIN != emp.pin)
        {
            buzzFail();
            currentState = STATE_FAIL;
            UI_ShowFail(tft, "PIN Mismatch", "FP");
            feedbackUntil = millis() + 2000;
            return;
        }

        authMethod = "FP+PIN";
    }
    else if (settings.authMode == AUTH_FP_RFID)
    {
        if (emp.rfidCard.isEmpty())
        {
            buzzFail();
            currentState = STATE_FAIL;
            UI_ShowFail(tft, "RFID Not Enrolled", "FP");
            feedbackUntil = millis() + 2000;
            return;
        }

        UI_ShowFingerprint(tft, "SCAN RFID", "Scan linked card", "After fingerprint");
        String scannedCard;
        if (!waitForRFIDCard(scannedCard, 15000))
        {
            buzzFail();
            currentState = STATE_FAIL;
            UI_ShowFail(tft, "RFID Timeout", "FP");
            feedbackUntil = millis() + 2000;
            return;
        }

        if (scannedCard != emp.rfidCard)
        {
            buzzFail();
            currentState = STATE_FAIL;
            UI_ShowFail(tft, "RFID Mismatch", scannedCard);
            feedbackUntil = millis() + 2000;
            return;
        }

        authMethod = "FP+RFID";
    }

    String today = getDateStr();
    String timeNow = getTimeStr();
    String activityNow = today + " " + timeNow;
    bool isCheckOut = Storage_HasCheckedInToday(emp.userId, today);

    AttendanceRecord rec;
    rec.userId = emp.userId;
    rec.name = emp.name;
    rec.cardId = String(fpId);
    rec.method = authMethod;
    rec.date = today;
    rec.synced = false;
    rec.lastActivity = activityNow;

    String statusLabel;

    if (!isCheckOut)
    {
        rec.checkIn = timeNow;
        rec.checkOut = "";
        rec.hours = "";
        rec.status = attendanceStatusForCheckIn(timeNow);
        rec.lateMinutes = rec.status == "Late"
                              ? max(0, minutesFromHHMM(timeNow) -
                                           minutesFromHHMM(settings.workStart) -
                                           settings.gracePeriod)
                              : 0;
        statusLabel = "Checked In";
        Storage_SaveAttendance(rec);
    }
    else
    {
        rec.checkIn = Storage_GetCheckInTime(emp.userId, today);
        rec.checkOut = timeNow;
        rec.hours = calculateWorkedHours(rec.checkIn, rec.checkOut);
        rec.status = "CheckOut";
        rec.lateMinutes = 0;
        statusLabel = "Checked Out";
        Storage_UpdateCheckOut(emp.userId, today, timeNow, rec.hours, rec.lastActivity);
    }

    buzzSuccess();
    currentState = STATE_SUCCESS;
    UI_ShowSuccess(tft, emp.name, statusLabel, timeNow, authMethod);
    feedbackUntil = millis() + 3000;

    if (wifiConnected && !deviceConfig.isEmpty())
    {
        bool sent = Network_SendAttendance(deviceConfig, rec);
        if (sent)
            Storage_MarkSynced(emp.userId, today);
    }
}

// ══════════════════════════════════════════════════════
// RFID SCAN (normal attendance)
// ══════════════════════════════════════════════════════
void handleRFIDScan()
{
    if (!authStartsWithRFID(settings.authMode))
        return;

    if (!rfidReady)
        return;

    digitalWrite(SD_CS, HIGH);
    digitalWrite(TFT_CS_PIN, HIGH);
    digitalWrite(TOUCH_CS_PIN, HIGH);
    SPI.setFrequency(RFID_SPI_FREQUENCY);

    if (!rfid.PICC_IsNewCardPresent() ||
        !rfid.PICC_ReadCardSerial())
        return;

    String cardId = "";
    for (byte i = 0; i < rfid.uid.size; i++)
    {
        if (rfid.uid.uidByte[i] < 0x10)
            cardId += "0";
        cardId += String(rfid.uid.uidByte[i], HEX);
    }
    cardId.toUpperCase();

    rfid.PICC_HaltA();
    rfid.PCD_StopCrypto1();

    currentState = STATE_SCANNING;
    UI_ShowScanning(tft, cardId);

    if (!sdReady)
    {
        buzzFail();
        currentState = STATE_FAIL;
        UI_ShowFail(tft, "SD Not Ready", cardId);
        feedbackUntil = millis() + 3000;
        return;
    }

    EmployeeRecord emp = Storage_FindEmployeeByRFID(cardId);

    if (emp.userId.isEmpty())
    {
        buzzFail();
        currentState = STATE_FAIL;
        UI_ShowFail(tft, "Unknown Card", cardId);
        feedbackUntil = millis() + 3000;

        return;
    }

    String authMethod = "RFID";

    if (settings.authMode == AUTH_PIN_RFID)
    {
        String enteredPIN;

        if (emp.pin.isEmpty())
        {
            buzzFail();
            currentState = STATE_FAIL;
            UI_ShowFail(tft, "PIN Not Enrolled", cardId);
            feedbackUntil = millis() + 2000;
            return;
        }

        if (!UI_ShowAttendancePIN(tft, enteredPIN))
        {
            currentState = STATE_HOME;
            UI_ShowHome(tft, wifiConnected, settings.authMode,
                        getTimeStr(), getDisplayDateStr());
            return;
        }

        if (enteredPIN != emp.pin)
        {
            buzzFail();
            currentState = STATE_FAIL;
            UI_ShowFail(tft, "PIN Mismatch", cardId);
            feedbackUntil = millis() + 2000;
            return;
        }

        authMethod = "RFID+PIN";
    }

    String today = getDateStr();
    String timeNow = getTimeStr();
    String activityNow = today + " " + timeNow;
    bool isCheckOut = Storage_HasCheckedInToday(emp.userId, today);

    AttendanceRecord rec;
    rec.userId = emp.userId;
    rec.name = emp.name;
    rec.cardId = cardId;
    rec.method = authMethod;
    rec.date = today;
    rec.synced = false;
    rec.lastActivity = activityNow;

    String statusLabel;

    if (!isCheckOut)
    {
        rec.checkIn = timeNow;
        rec.checkOut = "";
        rec.hours = "";
        rec.status = attendanceStatusForCheckIn(timeNow);
        rec.lateMinutes = rec.status == "Late"
                              ? max(0, minutesFromHHMM(timeNow) -
                                           minutesFromHHMM(settings.workStart) -
                                           settings.gracePeriod)
                              : 0;
        statusLabel = "Checked In";
        Storage_SaveAttendance(rec);
    }
    else
    {
        rec.checkIn = Storage_GetCheckInTime(emp.userId, today);
        rec.checkOut = timeNow;
        rec.hours = calculateWorkedHours(rec.checkIn, rec.checkOut);
        rec.status = "CheckOut";
        rec.lateMinutes = 0;
        statusLabel = "Checked Out";
        Storage_UpdateCheckOut(emp.userId, today, timeNow, rec.hours, rec.lastActivity);
    }

    buzzSuccess();
    currentState = STATE_SUCCESS;
    UI_ShowSuccess(tft, emp.name, statusLabel, timeNow, authMethod);
    feedbackUntil = millis() + 3000;

    if (wifiConnected && !deviceConfig.isEmpty())
    {
        bool sent = Network_SendAttendance(deviceConfig, rec);
        if (sent)
            Storage_MarkSynced(emp.userId, today);
    }
}

// ══════════════════════════════════════════════════════
// PIN ATTENDANCE
// ══════════════════════════════════════════════════════
void handlePINAttendance()
{
    if (!authStartsWithPIN(settings.authMode))
        return;

    currentState = STATE_PIN_ATTENDANCE;

    String enteredPIN = "";
    bool entered = UI_ShowAttendancePIN(tft, enteredPIN);

    if (!entered)
    {
        currentState = STATE_HOME;
        UI_ShowHome(tft, wifiConnected, settings.authMode,
                    getTimeStr(), getDisplayDateStr());
        return;
    }

    if (!sdReady)
    {
        buzzFail();
        currentState = STATE_FAIL;
        UI_ShowFail(tft, "SD Not Ready", "PIN");
        feedbackUntil = millis() + 2000;
        return;
    }

    EmployeeRecord emp = Storage_FindEmployeeByPIN(enteredPIN);

    if (emp.userId.isEmpty())
    {
        buzzFail();
        currentState = STATE_FAIL;
        UI_ShowFail(tft, "Unknown PIN", "PIN");
        feedbackUntil = millis() + 1800;
        return;
    }

    String today = getDateStr();
    String timeNow = getTimeStr();
    String activityNow = today + " " + timeNow;
    bool isCheckOut = Storage_HasCheckedInToday(emp.userId, today);

    AttendanceRecord rec;
    rec.userId = emp.userId;
    rec.name = emp.name;
    rec.cardId = emp.userId;
    rec.method = "PIN";
    rec.date = today;
    rec.synced = false;
    rec.lastActivity = activityNow;

    String statusLabel;

    if (!isCheckOut)
    {
        rec.checkIn = timeNow;
        rec.checkOut = "";
        rec.hours = "";
        rec.status = attendanceStatusForCheckIn(timeNow);
        rec.lateMinutes = rec.status == "Late"
                              ? max(0, minutesFromHHMM(timeNow) -
                                           minutesFromHHMM(settings.workStart) -
                                           settings.gracePeriod)
                              : 0;
        statusLabel = "Checked In";
        Storage_SaveAttendance(rec);
    }
    else
    {
        rec.checkIn = Storage_GetCheckInTime(emp.userId, today);
        rec.checkOut = timeNow;
        rec.hours = calculateWorkedHours(rec.checkIn, rec.checkOut);
        rec.status = "CheckOut";
        rec.lateMinutes = 0;
        statusLabel = "Checked Out";
        Storage_UpdateCheckOut(emp.userId, today, timeNow, rec.hours, rec.lastActivity);
    }

    buzzSuccess();
    currentState = STATE_SUCCESS;
    UI_ShowSuccess(tft, emp.name, statusLabel, timeNow, "PIN");
    feedbackUntil = millis() + 3000;

    if (wifiConnected && !deviceConfig.isEmpty())
    {
        bool sent = Network_SendAttendance(deviceConfig, rec);
        if (sent)
            Storage_MarkSynced(emp.userId, today);
    }
}

// OFFLINE SYNC
// â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
void syncOfflineRecords()
{
    if (!wifiConnected || deviceConfig.isEmpty())
        return;

    auto unsynced = Storage_GetUnsyncedRecords();
    int count = 0;

    for (auto &rec : unsynced)
    {
        bool ok = Network_SendAttendance(deviceConfig, rec);
        if (!ok)
            break;

        Storage_MarkSynced(rec.userId, rec.date);
        count++;

        if (count >= MAX_SYNC_PER_PASS)
            break;
    }

    if (count > 0)
        Serial.println("[SYNC] Sent " + String(count) + " records");
}

void syncDeviceUpdates()
{
    if (!wifiConnected || deviceConfig.isEmpty() || !sdReady)
        return;

    long lastSyncId = Storage_LoadLastDeviceSyncId();
    DeviceUpdate updates[8];
    int updateCount = 0;

    bool ok = Network_GetDeviceUpdates(
        deviceConfig,
        lastSyncId,
        updates,
        8,
        updateCount);

    if (!ok || updateCount == 0)
        return;

    bool showSyncUi = currentState == STATE_HOME;
    long ackIds[8];
    int ackCount = 0;
    long maxAckedId = lastSyncId;

    for (int i = 0; i < updateCount; i++)
    {
        if (showSyncUi)
            UI_ShowDeviceSync(tft, i + 1, updateCount);

        if (!applyDeviceUpdate(updates[i]))
            break;

        ackIds[ackCount++] = updates[i].id;
        if (updates[i].id > maxAckedId)
            maxAckedId = updates[i].id;
    }

    if (ackCount == 0)
        return;

    if (Network_AckDeviceUpdates(deviceConfig, ackIds, ackCount))
    {
        Storage_SaveLastDeviceSyncId(maxAckedId);
        Serial.println("[DEVICE SYNC] ACKed " + String(ackCount));

        if (showSyncUi)
            UI_ShowHome(tft, wifiConnected, settings.authMode,
                        getTimeStr(), getDisplayDateStr());
    }
}

bool applyDeviceUpdate(DeviceUpdate &update)
{
    Serial.print("[DEVICE SYNC] Apply ");
    Serial.print(update.id);
    Serial.print(" ");
    Serial.println(update.type);

    if (update.type == "employee_delete")
    {
        String userId = update.userId;
        if (userId.isEmpty())
        {
            StaticJsonDocument<256> doc;
            if (deserializeJson(doc, update.payload))
                return false;
            userId = doc["userId"] | "";
        }

        if (userId.isEmpty())
            return false;

        Storage_DeleteEmployee(userId);
        return true;
    }

    if (update.type == "employee_upsert" ||
        update.type == "enrollment_request")
    {
        StaticJsonDocument<1024> doc;
        DeserializationError err =
            deserializeJson(doc, update.payload);
        if (err)
        {
            Serial.print("[DEVICE SYNC] Payload JSON error: ");
            Serial.println(err.c_str());
            return false;
        }

        String userId = doc["userId"] | update.userId;
        if (userId.isEmpty())
            return false;

        String status = doc["status"] | "Active";
        if (status != "Active")
        {
            Storage_DeleteEmployee(userId);
            return true;
        }

        EmployeeRecord existing =
            Storage_FindEmployeeByUserId(userId);
        if (existing.userId.isEmpty())
        {
            existing.rfidCard = "";
            existing.pin = "";
            existing.fingerprintId = -1;
        }

        EmployeeRecord emp;
        emp.userId = userId;
        emp.name =
            doc["name"] | existing.name;
        emp.department =
            doc["department"] | existing.department;
        emp.rfidCard =
            doc["rfidCard"] | existing.rfidCard;
        emp.pin =
            doc["pinValue"] | existing.pin;
        emp.fingerprintId =
            doc["fingerprintId"] | existing.fingerprintId;

        if (emp.fingerprintId == 0 &&
            existing.fingerprintId == -1)
            emp.fingerprintId = -1;

        Storage_SaveEmployee(emp);
        return true;
    }

    Serial.println("[DEVICE SYNC] Unknown update type, ACKing");
    return true;
}

// ══════════════════════════════════════════════════════
// BUZZER
// ══════════════════════════════════════════════════════
void buzzSuccess()
{
    // Short high beep
    AttendanceSettings cfg = Storage_LoadSettings();
    if (!cfg.buzzerEnabled)
        return;

    tone(BUZZER_PIN, 1200, 120);
    delay(140);
    tone(BUZZER_PIN, 1600, 80);
    delay(100);
    noTone(BUZZER_PIN);
}

void buzzFail()
{
    AttendanceSettings cfg = Storage_LoadSettings();
    if (!cfg.buzzerEnabled)
        return;

    tone(BUZZER_PIN, 400, 300);
    delay(350);
    tone(BUZZER_PIN, 300, 200);
    delay(250);
    noTone(BUZZER_PIN);
}

void buzzWelcome()
{
    AttendanceSettings cfg = Storage_LoadSettings();
    if (!cfg.buzzerEnabled)
        return;

    tone(BUZZER_PIN, 800, 80);
    delay(100);

    tone(BUZZER_PIN, 1200, 80);
    delay(100);

    tone(BUZZER_PIN, 1600, 120);
    delay(140);

    noTone(BUZZER_PIN);
}

void buzzBtnPress(){

    AttendanceSettings cfg = Storage_LoadSettings();
    if (!cfg.buzzerEnabled)
        return;

    tone(BUZZER_PIN, 1000, 50);
    delay(70);
    noTone(BUZZER_PIN);

}

// ══════════════════════════════════════════════════════
// MASTER PIN STORAGE
// ══════════════════════════════════════════════════════
String getMasterPIN()
{
    if (!sdReady)
        return DEFAULT_MASTER_PIN;

    File f = SD.open("/masterpin.txt", FILE_READ);
    if (!f)
        return DEFAULT_MASTER_PIN;

    String pin = "";
    while (f.available())
        pin += (char)f.read();
    f.close();

    pin.trim();
    return pin.isEmpty() ? DEFAULT_MASTER_PIN : pin;
}

void saveMasterPIN(String pin)
{
    if (!sdReady)
        return;
    File f = SD.open("/masterpin.txt", FILE_WRITE);
    if (!f)
        return;
    f.print(pin);
    f.close();
}

// ══════════════════════════════════════════════════════
// SPI / WIFI / HARDWARE INIT
// ══════════════════════════════════════════════════════
void initSPI()
{
    pinMode(RFID_SS, OUTPUT);
    digitalWrite(RFID_SS, HIGH);
    pinMode(SD_CS, OUTPUT);
    digitalWrite(SD_CS, HIGH);
    pinMode(TFT_CS_PIN, OUTPUT);
    digitalWrite(TFT_CS_PIN, HIGH);
    pinMode(TOUCH_CS_PIN, OUTPUT);
    digitalWrite(TOUCH_CS_PIN, HIGH);

    SPI.begin(RFID_SPI_SCK, RFID_SPI_MISO, RFID_SPI_MOSI, RFID_SS);
    SPI.setFrequency(RFID_SPI_FREQUENCY);
}

void initSD()
{
    digitalWrite(RFID_SS, HIGH);
    digitalWrite(TFT_CS_PIN, HIGH);
    digitalWrite(TOUCH_CS_PIN, HIGH);
    digitalWrite(SD_CS, HIGH);
    delay(20);

    SD.end();
    sdReady = SD.begin(SD_CS, TFT_eSPI::getSPIinstance(), 1000000) &&
              SD.cardType() != CARD_NONE;

    if (sdReady)
    {
        SD.mkdir("/employees");
        SD.mkdir("/attendance");
        Serial.println("[SD] Ready");
    }
    else
    {
        Serial.println("[SD] Not ready");
    }

    digitalWrite(SD_CS, HIGH);
    SPI.setFrequency(RFID_SPI_FREQUENCY);
}

void initRFID()
{
    digitalWrite(SD_CS, HIGH);
    digitalWrite(TFT_CS_PIN, HIGH);
    digitalWrite(TOUCH_CS_PIN, HIGH);
    digitalWrite(RFID_SS, HIGH);

    pinMode(RFID_RST, OUTPUT);
    digitalWrite(RFID_RST, HIGH);
    delay(50);

    SPI.setFrequency(RFID_SPI_FREQUENCY);
    rfid.PCD_Init();
    rfid.PCD_AntennaOn();

    byte version = rfid.PCD_ReadRegister(MFRC522::VersionReg);
    rfidReady = !(version == 0x00 || version == 0xFF);

    Serial.println(rfidReady ? "[RFID] Ready" : "[RFID] Not ready");
}

void initFingerprint()
{
    fpSerial.begin(FP_BAUD, SERIAL_8N1, FP_RX_PIN, FP_TX_PIN);
    finger.begin(FP_BAUD);
    fpReady = finger.verifyPassword();

    if (fpReady)
    {
        Serial.println("[FP] Ready on RX=44 TX=43");
    }
    else
    {
        Serial.println("[FP] Not ready on RX=44 TX=43");
    }
}

void connectWiFiWithCreds(String ssid, String pass)
{
    WiFi.mode(WIFI_STA);
    WiFi.begin(ssid.c_str(), pass.c_str());
    lastWifiAttempt = millis();
    wifiReconnectInProgress = true;
    Serial.print("[WiFi] Connecting");

    int attempts = 0;
    while (WiFi.status() != WL_CONNECTED && attempts < 6)
    {
        delay(500);
        Serial.print(".");
        attempts++;
    }

    wifiConnected = (WiFi.status() == WL_CONNECTED);
    wifiReconnectInProgress = !wifiConnected;
    if (wifiConnected)
    {
        Serial.println(" connected: " + WiFi.localIP().toString());
        syncRtcFromNtp();
        startMdns();
    }
    else
    {
        Serial.println(" failed");
    }
}

void connectWiFi()
{
    // No credentials saved — start AP mode
    Serial.println("[WiFi] No credentials — starting AP mode");
    startAPMode();
}

void startAPMode()
{
    WiFi.mode(WIFI_AP);
    WiFi.softAP(AP_SSID); // open network, no password
    apModeActive = true;
    wifiConnected = false;
    currentState = STATE_AP_MODE;

    Serial.println("[AP] Hotspot started: " + String(AP_SSID));
    Serial.println("[AP] IP: " + WiFi.softAPIP().toString());

    // Show on screen
    UI_ShowAPMode(tft); // we'll add this UI call
}

void checkWiFi()
{
    // Don't interfere if in AP mode waiting for config
    if (apModeActive)
        return;

    bool wasConnected = wifiConnected;
    unsigned long now = millis();

    if (WiFi.status() == WL_CONNECTED)
    {
        wifiConnected = true;
        wifiReconnectInProgress = false;
        if (!wasConnected)
        {
            Serial.println("[WiFi] Reconnected");
            startMdns();
            syncRtcFromNtp();
            syncOfflineRecords();
        }
        return;
    }

    wifiConnected = false;

    if (wifiReconnectInProgress &&
        now - lastWifiAttempt < WIFI_CONNECT_GRACE_MS)
        return;

    if (now - lastWifiAttempt < WIFI_RETRY_INTERVAL_MS)
        return;

    DeviceConfig cfg = Storage_LoadConfig();
    if (cfg.wifiSSID.isEmpty())
        return; // no creds, stay offline

    WiFi.disconnect(false);
    WiFi.begin(cfg.wifiSSID.c_str(), cfg.wifiPassword.c_str());
    lastWifiAttempt = now;
    wifiReconnectInProgress = true;
    Serial.println("[WiFi] Reconnect started");
}

void startMdns()
{
    MDNS.end();

    if (!MDNS.begin(MDNS_HOSTNAME))
    {
        Serial.println("[mDNS] Failed");
        return;
    }

    MDNS.addService("http", "tcp", 5001);
    MDNS.addServiceTxt("http", "tcp", "type", "SmartAttend");
    MDNS.addServiceTxt("http", "tcp", "device", DEVICE_NAME);

    Serial.print("[mDNS] ");
    Serial.print(MDNS_HOSTNAME);
    Serial.println(".local ready");
}

// ══════════════════════════════════════════════════════
// HTTP SERVER
// ══════════════════════════════════════════════════════
void setupHttpServer()
{
    // Get Ping
    server.on("/ping", HTTP_GET, []()
              {
        String ip = apModeActive
            ? WiFi.softAPIP().toString()
            : WiFi.localIP().toString();
        String json = "{\"status\":\"online\",\"ip\":\"" + ip + "\"}";
        server.send(200, "application/json", json); });

    // Get Info
    server.on("/info", HTTP_GET, []()
              {
        String ip = apModeActive
            ? WiFi.softAPIP().toString()
            : WiFi.localIP().toString();
        String json = "{";
        json += "\"device\":\"SmartAttend-001\",";
        json += "\"version\":\"2.0\",";
        json += "\"ip\":\"" + ip + "\",";
        json += "\"port\":5001,";
        json += "\"sdReady\":" + String(sdReady ? "true" : "false");
        json += "}";
        server.send(200, "application/json", json); });

    // Post Config
    server.on("/config", HTTP_POST, []()
              {
        String body = server.arg("plain");
        Serial.println("[CONFIG] Received Wi-Fi configuration request.");

        StaticJsonDocument<1024> doc;
        if (deserializeJson(doc, body))
        {
            server.send(400,
                        "application/json",
                        "{\"status\":\"invalid_json\"}");
            return;
        }

        DeviceConfig cfg = Storage_LoadConfig();

        // Save WiFi credentials
        cfg.wifiSSID = doc["ssid"].as<String>();
        cfg.wifiPassword = doc["password"].as<String>();

        Serial.println("[CONFIG] Saving Wi-Fi configuration.");

        Storage_SaveConfig(cfg);

        server.send(200,
                    "application/json",
                    "{\"status\":\"configured\"}");

        delay(1000);

        ESP.restart(); });

    // Post Settings
    server.on("/settings", HTTP_POST, []()
              {
                  settings = Storage_LoadSettings();
                  StaticJsonDocument<512> doc;
                  DeserializationError err =
                      deserializeJson(doc, server.arg("plain"));

                  if (err)
                  {
                      server.send(400,
                                  "application/json",
                                  "{\"success\":false}");
                      return;
                  }

                  if (doc.containsKey("authMode"))
                      settings.authMode =
                          (AuthMode)doc["authMode"].as<int>();

                  if (doc.containsKey("workStart"))
                      settings.workStart =
                          doc["workStart"].as<String>();

                  if (doc.containsKey("workEnd"))
                      settings.workEnd =
                          doc["workEnd"].as<String>();

                  if (doc.containsKey("gracePeriod"))
                      settings.gracePeriod =
                          doc["gracePeriod"].as<int>();

                  if (doc.containsKey("buzzer"))
                      settings.buzzerEnabled =
                          doc["buzzer"].as<bool>();

                  settings.sdBackupEnabled = true;
                  settings.autoSync = true;

                  Storage_SaveSettings(settings);

                  // Refresh home screen if visible
                  if (currentState == STATE_HOME)
                  {
                      UI_ShowHome(tft, wifiConnected, settings.authMode,
                                  getTimeStr(), getDisplayDateStr());
                  }

                  server.send(200,
                              "application/json",
                              "{\"success\":true}");
              });

    // Get settings
    server.on("/settings", HTTP_GET, []()
              {
        AttendanceSettings s =
            Storage_LoadSettings();

        StaticJsonDocument<512> doc;

        doc["authMode"] = (int)s.authMode;
        doc["workStart"] = s.workStart;
        doc["workEnd"] = s.workEnd;
        doc["gracePeriod"] = s.gracePeriod;
        doc["buzzer"] = s.buzzerEnabled;
        doc["sdBackup"] = true;
        doc["autoSync"] = true;

        String out;
        serializeJson(doc, out);

        server.send(200,
            "application/json",
            out); });

    // Post employee record
    server.on("/employee", HTTP_POST, []()
              {
        String body = server.arg("plain");
        StaticJsonDocument<256> doc;
        if (deserializeJson(doc, body))
        {
            server.send(400, "application/json",
                        "{\"status\":\"invalid_json\"}");
            return;
        }

        EmployeeRecord emp;
        emp.userId        = doc["userId"].as<String>();
        emp.name          = doc["name"].as<String>();
        emp.department    = doc["department"] | "";
        emp.rfidCard      = doc["rfidCard"].as<String>();
        emp.pin           = doc["pin"].as<String>();
        emp.fingerprintId = doc["fingerprintId"].as<int>();
        Storage_SaveEmployee(emp);

        server.send(200, "application/json",
                    "{\"status\":\"saved\"}"); });

    // Get Discovery
    server.on("/discovery", HTTP_GET, []()
              {
        String ip = apModeActive
            ? WiFi.softAPIP().toString()
            : WiFi.localIP().toString();
        String json = "{\"type\":\"SmartAttend\",";
        json += "\"device\":\"SmartAttend-001\",";
        json += "\"ip\":\"" + ip + "\",";
        json += "\"port\":5001,\"version\":\"2.0\"}";
        server.send(200, "application/json", json); });

    // server.on("/factory-reset", HTTP_POST, []()
    //           {
    //     server.send(403, "application/json",
    //                 "{\"status\":\"button_required\",\"gpio\":44,\"holdSeconds\":5}"); });

    // Post server config (for AP mode)
    server.on("/server-config", HTTP_POST, []()
              {
        StaticJsonDocument<256> doc;

        if (deserializeJson(doc, server.arg("plain")))
        {
            server.send(400,
                "application/json",
                "{\"success\":false}");
            return;
        }

        settings =
            Storage_LoadSettings();

        settings.serverIP =
            doc["server"].as<String>();

        settings.serverPort =
            doc["port"] | 5000;

        Storage_SaveSettings(settings);
        deviceConfig = settings.serverIP;

        server.send(200,
            "application/json",
            "{\"success\":true}");

        Serial.println("[SERVER CONFIG] Saved."); });
}

// ══════════════════════════════════════════════════════
// TIME HELPERS
// ══════════════════════════════════════════════════════
void syncRtcFromNtp()
{
    if (!rtcReady || WiFi.status() != WL_CONNECTED)
        return;

    configTime(LOCAL_GMT_OFFSET_SEC,
               LOCAL_DAYLIGHT_OFFSET_SEC,
               "pool.ntp.org",
               "time.google.com",
               "time.cloudflare.com");

    struct tm timeInfo;
    if (!getLocalTime(&timeInfo, 5000))
    {
        Serial.println("[RTC] NTP sync failed");
        return;
    }

    DateTime networkTime(timeInfo.tm_year + 1900,
                         timeInfo.tm_mon + 1,
                         timeInfo.tm_mday,
                         timeInfo.tm_hour,
                         timeInfo.tm_min,
                         timeInfo.tm_sec);
    DateTime rtcTime = rtc.now();
    long drift = (long)networkTime.unixtime() - (long)rtcTime.unixtime();

    if (abs(drift) > 2)
    {
        rtc.adjust(networkTime);
        Serial.print("[RTC] Adjusted from NTP, drift seconds: ");
        Serial.println(drift);
    }
    else
    {
        Serial.println("[RTC] NTP checked, RTC already accurate");
    }
}

String getDateStr()
{
    DateTime now = rtc.now();

    char buf[11];
    sprintf(buf,
            "%04d-%02d-%02d",
            now.year(),
            now.month(),
            now.day());

    return String(buf);
}

String getTimeStr()
{
    DateTime now = rtc.now();

    char buf[6];
    sprintf(buf,
            "%02d:%02d",
            now.hour(),
            now.minute());

    return String(buf);
}

String getDisplayDateStr()
{
    static const char *days[] = {
        "SUN", "MON", "TUE", "WED", "THU", "FRI", "SAT"};
    static const char *months[] = {
        "JAN", "FEB", "MAR", "APR", "MAY", "JUN",
        "JUL", "AUG", "SEP", "OCT", "NOV", "DEC"};

    DateTime now = rtc.now();
    uint8_t dow = now.dayOfTheWeek();
    uint8_t month = now.month();

    char buf[17];
    sprintf(buf,
            "%s %02d %s %04d",
            days[dow < 7 ? dow : 0],
            now.day(),
            months[(month >= 1 && month <= 12) ? month - 1 : 0],
            now.year());

    return String(buf);
}
