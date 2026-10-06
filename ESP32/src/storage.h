#pragma once
#include <Arduino.h>
#include <vector>

// ── Auth modes ─────────────────────────────────────────
enum AuthMode {
    AUTH_ANY    = 0,  // Any single method works
    AUTH_RFID   = 1,  // RFID only
    AUTH_FP     = 2,  // Fingerprint only
    AUTH_PIN    = 3,  // PIN only
    AUTH_FP_RFID = 4, // Fingerprint + RFID (2FA)
    AUTH_FP_PIN  = 5, // Fingerprint + PIN (2FA)
    AUTH_PIN_RFID = 6 // RFID + PIN (2FA)
};

// ── Data structures ────────────────────────────────────
struct DeviceConfig {
    String wifiSSID;
    String wifiPassword;
    // String boundAppIP;      // empty = unbound
    String deviceName;
    
};
struct AttendanceSettings
{
    AuthMode authMode;

    String workStart;
    String workEnd;
    int gracePeriod;

    bool buzzerEnabled;
    bool sdBackupEnabled;
    bool autoSync;
    
    String serverIP;
    int serverPort;
};

struct EmployeeRecord {
    String userId;
    String name;
    String department;
    String rfidCard;
    String pin;
    int    fingerprintId;   // -1 = not enrolled
};

struct AttendanceRecord {
    String userId;
    String name;
    String cardId;
    String method;
    String checkIn;
    String checkOut;
    String hours;
    String date;
    String status;
    int    lateMinutes;
    bool   synced;
    String lastActivity;
};


// ── Pending employee item ──────────────────────────────
struct PendingEmployee {
    String userId;
    String name;
    String department;
    bool   rfidDone;
    bool   pinDone;
    bool   fpDone;
};


// ── Storage functions ──────────────────────────────────
DeviceConfig  Storage_LoadConfig();
void          Storage_SaveConfig(DeviceConfig& cfg);
void          Storage_FactoryReset();

AttendanceSettings Storage_LoadSettings();
void Storage_SaveSettings(AttendanceSettings& settings);

void          Storage_SaveEmployee(EmployeeRecord& emp);
int           Storage_GetEmployees(PendingEmployee* list,
                  int maxCount, bool pendingOnly);
int           Storage_SearchEmployees(PendingEmployee* list,
                  int maxCount, String query);
EmployeeRecord Storage_FindEmployeeByRFID(String cardId);
EmployeeRecord Storage_FindEmployeeByPIN(String pin);
EmployeeRecord Storage_FindEmployeeByUserId(String userId);
std::vector<EmployeeRecord> Storage_GetAllEmployees();
void          Storage_DeleteEmployee(String userId);

void          Storage_SaveAttendance(AttendanceRecord& rec);
bool          Storage_HasCheckedInToday(String userId, String date);
String        Storage_GetCheckInTime(String userId, String date);
void          Storage_UpdateCheckOut(String userId, String date,
                  String checkOut, String hours, String lastActivity);
void          Storage_MarkSynced(String userId, String date);
int           Storage_GetUnsyncedCount();
std::vector<AttendanceRecord> Storage_GetUnsyncedRecords();

long          Storage_LoadLastDeviceSyncId();
void          Storage_SaveLastDeviceSyncId(long syncId);
