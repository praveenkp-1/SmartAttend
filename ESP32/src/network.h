#pragma once
#include <Arduino.h>
#include "storage.h"

#define APP_PORT 5000

struct DeviceUpdate {
    long id;
    String type;
    String userId;
    String payload;
};

// Send attendance to app
bool Network_SendAttendance(String appIP, AttendanceRecord& rec);

// Register RFID card in app
bool Network_RegisterRFID(String appIP, String cardId,
    String userId);

// Register PIN in app
bool Network_RegisterPIN(String appIP, String userId,
    String pin);

// Register fingerprint enrollment in app
bool Network_RegisterFingerprint(String appIP, String userId);

// Get pending employees from app
// Returns count, fills list[]
int Network_GetPendingEmployees(String appIP,
    PendingEmployee* list, int maxCount);

// Pull app-to-device updates from WPF sync queue
bool Network_GetDeviceUpdates(String appIP, long lastSyncId,
    DeviceUpdate* updates, int maxCount, int& count);

// Acknowledge successfully applied app-to-device updates
bool Network_AckDeviceUpdates(String appIP, long* ids, int count);
