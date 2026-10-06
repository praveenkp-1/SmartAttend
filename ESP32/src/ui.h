#pragma once
#include <TFT_eSPI.h>
#include "storage.h"
// ── App screen states ──────────────────────────────────
enum AppState {
    STATE_HOME,
    STATE_AP_MODE,
    STATE_SCANNING,
    STATE_SUCCESS,
    STATE_FAIL,
    STATE_PIN_ATTENDANCE,

    // ── Admin flow ─────────────────────────────────────
    STATE_ADMIN_PIN,        // master PIN entry
    STATE_ADMIN_MENU,       // main admin menu
    STATE_ENROLL_LIST,      // pending employees list
    STATE_ENROLL_MENU,      // RFID / PIN / FP choice
    STATE_ENROLL_RFID,      // scan card screen
    STATE_ENROLL_PIN,       // set PIN screen
    STATE_ENROLL_SUCCESS,   // enrollment done
    STATE_CHANGE_PIN,       // change master PIN
    STATE_FACTORY_RESET
};



// ── Touch result ───────────────────────────────────────
struct TouchResult {
    bool     pressed;
    uint16_t x;
    uint16_t y;
    uint16_t rawX;
    uint16_t rawY;
};

// ── Core UI ────────────────────────────────────────────
void UI_Init(TFT_eSPI& tft);
void UI_ShowBoot(TFT_eSPI& tft);
void UI_ShowHome(TFT_eSPI& tft, bool wifiConnected, AuthMode authMode,
                 const String& timeStr = "", const String& dateStr = "");
void UI_UpdateClock(TFT_eSPI& tft, const String& timeStr, const String& dateStr);
void UI_UpdateWifiDot(TFT_eSPI& tft, bool connected);
void UI_ShowScanning(TFT_eSPI& tft, String cardId);
void UI_ShowSuccess(TFT_eSPI& tft, String name, String action,
                    String time, String method = "RFID CARD");
void UI_ShowFail(TFT_eSPI& tft, String reason, String cardId);
void UI_ShowWrongPin(TFT_eSPI& tft);
void UI_ShowProcessing(TFT_eSPI& tft);
void UI_ShowDeviceSync(TFT_eSPI& tft, int current, int total);
void UI_ShowFingerprint(TFT_eSPI& tft, String title,
                        String line1, String line2 = "");
void UI_ShowFingerprintScanning(TFT_eSPI& tft, String line1,
                                String line2 = "Keep finger still");
void UI_ShowAPMode(TFT_eSPI& tft);



// ── Touch ──────────────────────────────────────────────
void        UI_InitTouch(TFT_eSPI& tft);
TouchResult UI_GetTouch(TFT_eSPI& tft);
bool        UI_IsTapped(TouchResult& t, int x, int y, int w, int h);
void        UI_ShowTouchDebug(TFT_eSPI& tft, TouchResult& t);

// ── Admin PIN screen ───────────────────────────────────
bool UI_ShowAdminPIN(TFT_eSPI& tft, String title, String& pinOut);
bool UI_ShowAttendancePIN(TFT_eSPI& tft, String& pinOut);

// ── Admin menu ─────────────────────────────────────────
// Returns: 0=Enroll, 1=ChangePIN, 2=Back
int UI_ShowAdminMenu(TFT_eSPI& tft);

// ── Pending employee list ──────────────────────────────
// Returns selected index, -1 = back
int UI_ShowEnrollList(TFT_eSPI& tft,
                      PendingEmployee* list, int count,
                      int& scrollOffset,
                      bool pendingOnly = true);
bool UI_ShowEmployeeSearch(TFT_eSPI& tft, String& queryOut);

// ── Enroll method menu ─────────────────────────────────
// Returns: 0=RFID, 1=PIN, 2=FP(future), 3=Back
int UI_ShowEnrollMenu(TFT_eSPI& tft,
                      String name, String userId,
                      bool rfidDone, bool pinDone, bool fpDone);

// ── Enroll RFID screen ─────────────────────────────────
void UI_ShowEnrollRFID(TFT_eSPI& tft, String name);

// ── Enroll PIN entry ───────────────────────────────────
bool UI_ShowEnrollPINEntry(TFT_eSPI& tft, String name, String& pinOut);

// ── Enrollment result ──────────────────────────────────
void UI_ShowEnrollSuccess(TFT_eSPI& tft, String name, String method);
void UI_ShowEnrollFail(TFT_eSPI& tft, String reason);
bool UI_ShowFactoryResetConfirm(TFT_eSPI& tft, int unsyncedCount);

// ── Change master PIN ──────────────────────────────────
bool UI_ShowChangeMasterPIN(TFT_eSPI& tft,
                             String currentPin, String& newPinOut);
