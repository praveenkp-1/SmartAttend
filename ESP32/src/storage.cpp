/*
 * ╔══════════════════════════════════════╗
 * ║   SMARTATTEND — SD CARD STORAGE      ║
 * ╚══════════════════════════════════════╝
 *
 * SD Card file structure:
 *   /config.json          — device config + WiFi + auth mode
 *   /employees/           — one file per employee
 *     EMP-10001.json
 *     EMP-10002.json
 *   /attendance/          — one file per date
 *     2025-06-01.json
 *     2025-06-02.json
 */

#include "storage.h"
#include <SD.h>
#include <ArduinoJson.h>

// ═══════════════════════════════════════════════════════
// HELPERS
// ═══════════════════════════════════════════════════════

static bool ensureDir(const char* path)
{
    if (!SD.exists(path))
        return SD.mkdir(path);
    return true;
}

static String readFile(String path)
{
    File f = SD.open(path, FILE_READ);
    if (!f) return "";
    String content = "";
    while (f.available())
        content += (char)f.read();
    f.close();
    return content;
}


static bool writeFile(String path, String content)
{
    
    if (SD.exists(path))
        SD.remove(path);

        Serial.println("[SD] Writing file:");
        Serial.println(path);
        File f = SD.open(path, FILE_WRITE);
    // Important: check if file opened successfully before writing
    if (!f)
    {
        Serial.println("[SD] OPEN FAILED!");
        Serial.println(path);
        return false;
    }
        f.print(content);
        f.close();
        Serial.println("[SD] Written: " + path);
        return true;
    }

// ═══════════════════════════════════════════════════════
// CONFIG
// ═══════════════════════════════════════════════════════

static String childPath(const String& parent, const char* name)
{
    String child = String(name);
    if (child.startsWith("/"))
        return child;
    return parent + "/" + child;
}

static void removeTree(const String& path)
{
    File node = SD.open(path);
    if (!node)
        return;

    if (!node.isDirectory())
    {
        node.close();
        SD.remove(path);
        return;
    }

    while (true)
    {
        File entry = node.openNextFile();
        if (!entry)
            break;

        String pathToRemove = childPath(path, entry.name());
        bool isDir = entry.isDirectory();
        entry.close();

        if (isDir)
            removeTree(pathToRemove);
        else
            SD.remove(pathToRemove);
    }

    node.close();
    SD.rmdir(path);
}

DeviceConfig Storage_LoadConfig()
{

    DeviceConfig cfg;
    cfg.wifiSSID       = "";
    cfg.wifiPassword   = "";
    // cfg.boundAppIP     = "";
    cfg.deviceName     = "SmartAttend-001";
    

    String raw = readFile("/config.json");
    if (raw.isEmpty()) return cfg;

    StaticJsonDocument<384> doc;
    if (deserializeJson(doc, raw) != DeserializationError::Ok)
        return cfg;

    cfg.wifiSSID        = doc["ssid"]       | "";
    cfg.wifiPassword    = doc["password"]   | "";
    cfg.deviceName      = doc["name"]       | "SmartAttend-001";
    
    return cfg;
}

void Storage_SaveConfig(DeviceConfig& cfg)
{
    StaticJsonDocument<384> doc;
    doc["ssid"]      = cfg.wifiSSID;
    doc["password"]  = cfg.wifiPassword;
    doc["name"]      = cfg.deviceName;
    
    String out;
    serializeJson(doc, out);
    writeFile("/config.json", out);
}

void Storage_FactoryReset()
{
    Serial.println("[STORAGE] FULL SD FACTORY RESET STARTED");

    // 1. Delete system files
    SD.remove("/config.json");
    SD.remove("/settings.json");
    SD.remove("/sync.json");

    // 2. Delete entire folders (recursive)
    removeTree("/employees");
    removeTree("/attendance");

    // 3. Recreate empty folders (important to avoid future errors)
    ensureDir("/employees");
    ensureDir("/attendance");

    Serial.println("[STORAGE] Factory reset completed - ALL SD DATA CLEARED");
}

// ═══════════════════════════════════════════════════════
// DEVICE SETINGS
// ═══════════════════════════════════════════════════════

AttendanceSettings Storage_LoadSettings()
{
    AttendanceSettings s;

    s.authMode = AUTH_ANY;
    s.workStart = "08:30";
    s.workEnd = "17:00";
    s.gracePeriod = 10;
    s.buzzerEnabled = true;
    s.sdBackupEnabled = true;
    s.autoSync = true;

    s.serverIP = "";
    s.serverPort = 5000;

    String raw = readFile("/settings.json");

    if(raw.isEmpty())
        return s;

    StaticJsonDocument<512> doc;

    if(deserializeJson(doc, raw))
        return s;

    s.authMode = (AuthMode)(doc["authMode"] | 0);
    s.workStart = doc["workStart"] | "08:30";
    s.workEnd = doc["workEnd"] | "17:00";
    s.gracePeriod = doc["gracePeriod"] | 10;
    s.buzzerEnabled = doc["buzzer"] | true;
    s.sdBackupEnabled = true;
    s.autoSync = true;

    s.serverIP = doc["serverIP"] | "";
    s.serverPort = doc["serverPort"] | 5000;

    return s;
}
void Storage_SaveSettings(AttendanceSettings& s)
{
    Serial.println("[SETTINGS] Storage_SaveSettings called");

    s.sdBackupEnabled = true;
    s.autoSync = true;

    StaticJsonDocument<512> doc;

    doc["authMode"] = (int)s.authMode;
    doc["workStart"] = s.workStart;
    doc["workEnd"] = s.workEnd;
    doc["gracePeriod"] = s.gracePeriod;
    doc["buzzer"] = s.buzzerEnabled;
    doc["sdBackup"] = s.sdBackupEnabled;
    doc["autoSync"] = s.autoSync;

    doc["serverIP"] = s.serverIP;
    doc["serverPort"] = s.serverPort;

    String out;
    serializeJson(doc, out);

    bool ok = writeFile("/settings.json", out);

    Serial.print("[SETTINGS] Write result = ");
    Serial.println(ok ? "SUCCESS" : "FAILED");
}

// ═══════════════════════════════════════════════════════
// EMPLOYEES
// ═══════════════════════════════════════════════════════

void Storage_SaveEmployee(EmployeeRecord& emp)
{
    ensureDir("/employees");

    StaticJsonDocument<256> doc;
    doc["userId"]        = emp.userId;
    doc["name"]          = emp.name;
    doc["department"]    = emp.department;
    doc["rfidCard"]      = emp.rfidCard;
    doc["pin"]           = emp.pin;
    doc["fingerprintId"] = emp.fingerprintId;

    String out;
    serializeJson(doc, out);
    writeFile("/employees/" + emp.userId + ".json", out);
}

int Storage_GetEmployees(PendingEmployee* list,
    int maxCount, bool pendingOnly)
{
    int count = 0;
    File dir = SD.open("/employees");
    if (!dir) return 0;

    while (true)
    {
        File entry = dir.openNextFile();
        if (!entry) break;

        String raw = "";
        while (entry.available())
            raw += (char)entry.read();
        entry.close();

        StaticJsonDocument<512> doc;
        if (deserializeJson(doc, raw) != DeserializationError::Ok)
            continue;

        String userId = doc["userId"] | "";
        if (userId.isEmpty())
            continue;

        bool rfidDone =
            !String(doc["rfidCard"] | "").isEmpty();
        bool pinDone =
            !String(doc["pin"] | "").isEmpty();
        int fpId = doc["fingerprintId"] | -1;
        bool fpDone = fpId >= 0;

        if (pendingOnly && rfidDone && pinDone && fpDone)
            continue;

        if (count >= maxCount)
            break;

        list[count].userId = userId;
        list[count].name = doc["name"] | "";
        list[count].department = doc["department"] | "";
        list[count].rfidDone = rfidDone;
        list[count].pinDone = pinDone;
        list[count].fpDone = fpDone;
        count++;
    }

    dir.close();
    return count;
}

int Storage_SearchEmployees(PendingEmployee* list,
    int maxCount, String query)
{
    query.trim();
    query.toLowerCase();
    if (query.isEmpty())
        return Storage_GetEmployees(list, maxCount, false);

    int count = 0;
    File dir = SD.open("/employees");
    if (!dir) return 0;

    while (true)
    {
        File entry = dir.openNextFile();
        if (!entry) break;

        String raw = "";
        while (entry.available())
            raw += (char)entry.read();
        entry.close();

        StaticJsonDocument<512> doc;
        if (deserializeJson(doc, raw) != DeserializationError::Ok)
            continue;

        String userId = doc["userId"] | "";
        String name = doc["name"] | "";
        String department = doc["department"] | "";

        String haystack = userId + " " + name + " " + department;
        haystack.toLowerCase();
        if (haystack.indexOf(query) < 0)
            continue;

        if (count >= maxCount)
            break;

        bool rfidDone =
            !String(doc["rfidCard"] | "").isEmpty();
        bool pinDone =
            !String(doc["pin"] | "").isEmpty();
        int fpId = doc["fingerprintId"] | -1;

        list[count].userId = userId;
        list[count].name = name;
        list[count].department = department;
        list[count].rfidDone = rfidDone;
        list[count].pinDone = pinDone;
        list[count].fpDone = fpId >= 0;
        count++;
    }

    dir.close();
    return count;
}

EmployeeRecord Storage_FindEmployeeByRFID(String cardId)
{
    EmployeeRecord empty;
    empty.userId = "";

    File dir = SD.open("/employees");
    if (!dir) return empty;

    while (true)
    {
        File entry = dir.openNextFile();
        if (!entry) break;

        String raw = "";
        while (entry.available())
            raw += (char)entry.read();
        entry.close();

        StaticJsonDocument<256> doc;
        if (deserializeJson(doc, raw) != DeserializationError::Ok)
            continue;

        String storedCard = doc["rfidCard"] | "";
        if (storedCard == cardId)
        {
            EmployeeRecord emp;
            emp.userId        = doc["userId"]        | "";
            emp.name          = doc["name"]          | "";
            emp.department    = doc["department"]    | "";
            emp.rfidCard      = doc["rfidCard"]      | "";
            emp.pin           = doc["pin"]           | "";
            emp.fingerprintId = doc["fingerprintId"] | -1;
            dir.close();
            return emp;
        }
    }

    dir.close();
    return empty;
}

EmployeeRecord Storage_FindEmployeeByPIN(String pin)
{
    EmployeeRecord empty;
    empty.userId = "";

    File dir = SD.open("/employees");
    if (!dir) return empty;

    while (true)
    {
        File entry = dir.openNextFile();
        if (!entry) break;

        String raw = "";
        while (entry.available())
            raw += (char)entry.read();
        entry.close();

        StaticJsonDocument<256> doc;
        if (deserializeJson(doc, raw) != DeserializationError::Ok)
            continue;

        String storedPin = doc["pin"] | "";
        if (!storedPin.isEmpty() && storedPin == pin)
        {
            EmployeeRecord emp;
            emp.userId        = doc["userId"]        | "";
            emp.name          = doc["name"]          | "";
            emp.department    = doc["department"]    | "";
            emp.rfidCard      = doc["rfidCard"]      | "";
            emp.pin           = doc["pin"]           | "";
            emp.fingerprintId = doc["fingerprintId"] | -1;
            dir.close();
            return emp;
        }
    }

    dir.close();
    return empty;
}

EmployeeRecord Storage_FindEmployeeByUserId(String userId)
{
    EmployeeRecord empty;
    empty.userId = "";

    String raw = readFile("/employees/" + userId + ".json");
    if (raw.isEmpty()) return empty;

    StaticJsonDocument<256> doc;
    if (deserializeJson(doc, raw) != DeserializationError::Ok)
        return empty;

    EmployeeRecord emp;
    emp.userId        = doc["userId"]        | "";
    emp.name          = doc["name"]          | "";
    emp.department    = doc["department"]    | "";
    emp.rfidCard      = doc["rfidCard"]      | "";
    emp.pin           = doc["pin"]           | "";
    emp.fingerprintId = doc["fingerprintId"] | -1;
    return emp;
}

std::vector<EmployeeRecord> Storage_GetAllEmployees()
{
    std::vector<EmployeeRecord> employees;

    File dir = SD.open("/employees");
    if (!dir) return employees;

    while (true)
    {
        File entry = dir.openNextFile();
        if (!entry) break;

        String raw = "";
        while (entry.available())
            raw += (char)entry.read();
        entry.close();

        StaticJsonDocument<512> doc;
        if (deserializeJson(doc, raw) != DeserializationError::Ok)
            continue;

        EmployeeRecord emp;
        emp.userId        = doc["userId"]        | "";
        emp.name          = doc["name"]          | "";
        emp.department    = doc["department"]    | "";
        emp.rfidCard      = doc["rfidCard"]      | "";
        emp.pin           = doc["pin"]           | "";
        emp.fingerprintId = doc["fingerprintId"] | -1;

        if (!emp.userId.isEmpty())
            employees.push_back(emp);
    }

    dir.close();
    return employees;
}

void Storage_DeleteEmployee(String userId)
{
    SD.remove("/employees/" + userId + ".json");
}

long Storage_LoadLastDeviceSyncId()
{
    String raw = readFile("/sync.json");
    if (raw.isEmpty())
        return 0;

    StaticJsonDocument<128> doc;
    if (deserializeJson(doc, raw) != DeserializationError::Ok)
        return 0;

    return doc["lastDeviceSyncId"] | 0;
}

void Storage_SaveLastDeviceSyncId(long syncId)
{
    StaticJsonDocument<128> doc;
    doc["lastDeviceSyncId"] = syncId;

    String out;
    serializeJson(doc, out);
    writeFile("/sync.json", out);
}

// ═══════════════════════════════════════════════════════
// ATTENDANCE
// Each date file is a JSON array of attendance records
// ═══════════════════════════════════════════════════════

static String attendancePath(String date)
{
    return "/attendance/" + date + ".json";
}

// Load full day attendance array into doc
static bool loadDayAttendance(String date,
    DynamicJsonDocument& doc)
{
    String raw = readFile(attendancePath(date));
    if (raw.isEmpty())
    {
        doc.to<JsonArray>();
        return false;
    }
    deserializeJson(doc, raw);
    return true;
}

static void saveDayAttendance(String date,
    DynamicJsonDocument& doc)
{
    ensureDir("/attendance");
    String out;
    serializeJson(doc, out);
    writeFile(attendancePath(date), out);
}

void Storage_SaveAttendance(AttendanceRecord& rec)
{
    DynamicJsonDocument doc(4096);
    loadDayAttendance(rec.date, doc);

    JsonArray arr = doc.as<JsonArray>();
    for (JsonObject obj : arr)
    {
        String uid = obj["userId"] | "";
        if (uid != rec.userId)
            continue;

        String existingCheckIn = obj["checkIn"] | "";
        String existingStatus = obj["status"] | "";

        if (existingCheckIn.isEmpty() ||
            existingStatus == "Absent" ||
            rec.status == "Absent")
        {
            obj["name"]        = rec.name;
            obj["cardId"]      = rec.cardId;
            obj["method"]      = rec.method;
            obj["checkIn"]     = rec.checkIn;
            obj["checkOut"]    = rec.checkOut;
            obj["hours"]       = rec.hours;
            obj["date"]        = rec.date;
            obj["status"]      = rec.status;
            obj["lateMinutes"] = rec.lateMinutes;
            obj["synced"]      = rec.synced;
            obj["lastActivity"] = rec.lastActivity;
            saveDayAttendance(rec.date, doc);
            return;
        }
    }

    JsonObject obj = arr.createNestedObject();
    obj["userId"]   = rec.userId;
    obj["name"]     = rec.name;
    obj["cardId"]   = rec.cardId;
    obj["method"]   = rec.method;
    obj["checkIn"]  = rec.checkIn;
    obj["checkOut"] = rec.checkOut;
    obj["hours"]    = rec.hours;
    obj["date"]     = rec.date;
    obj["status"]   = rec.status;
    obj["lateMinutes"] = rec.lateMinutes;
    obj["synced"]   = rec.synced;
    obj["lastActivity"] = rec.lastActivity;

    saveDayAttendance(rec.date, doc);
}

bool Storage_HasCheckedInToday(String userId, String date)
{
    DynamicJsonDocument doc(4096);
    loadDayAttendance(date, doc);

    JsonArray arr = doc.as<JsonArray>();
    for (JsonObject obj : arr)
    {
        String uid = obj["userId"] | "";
        String ci  = obj["checkIn"] | "";
        if (uid == userId && !ci.isEmpty() && ci != "")
            return true;
    }
    return false;
}

String Storage_GetCheckInTime(String userId, String date)
{
    DynamicJsonDocument doc(4096);
    loadDayAttendance(date, doc);

    JsonArray arr = doc.as<JsonArray>();
    for (JsonObject obj : arr)
    {
        String uid = obj["userId"] | "";
        if (uid == userId)
            return obj["checkIn"] | "";
    }
    return "";
}

void Storage_UpdateCheckOut(String userId, String date,
    String checkOut, String hours, String lastActivity)
{
    DynamicJsonDocument doc(4096);
    loadDayAttendance(date, doc);

    JsonArray arr = doc.as<JsonArray>();
    for (JsonObject obj : arr)
    {
        String uid = obj["userId"] | "";
        if (uid == userId)
        {
            obj["checkOut"] = checkOut;
            obj["hours"]    = hours;
            obj["lastActivity"] = lastActivity;
            obj["synced"]   = false;
            break;
        }
    }
    saveDayAttendance(date, doc);
}

void Storage_MarkSynced(String userId, String date)
{
    DynamicJsonDocument doc(4096);
    loadDayAttendance(date, doc);

    JsonArray arr = doc.as<JsonArray>();
    for (JsonObject obj : arr)
    {
        String uid = obj["userId"] | "";
        if (uid == userId)
        {
            obj["synced"] = true;
            break;
        }
    }
    saveDayAttendance(date, doc);
}

int Storage_GetUnsyncedCount()
{
    int count = 0;
    File dir = SD.open("/attendance");
    if (!dir) return 0;

    while (true)
    {
        File entry = dir.openNextFile();
        if (!entry) break;

        String raw = "";
        while (entry.available())
            raw += (char)entry.read();
        entry.close();

        DynamicJsonDocument doc(4096);
        if (deserializeJson(doc, raw) != DeserializationError::Ok)
            continue;

        JsonArray arr = doc.as<JsonArray>();
        for (JsonObject obj : arr)
        {
            if (!(obj["synced"] | false))
                count++;
        }
    }
    dir.close();
    return count;
}

std::vector<AttendanceRecord> Storage_GetUnsyncedRecords()
{
    std::vector<AttendanceRecord> records;

    File dir = SD.open("/attendance");
    if (!dir) return records;

    while (true)
    {
        File entry = dir.openNextFile();
        if (!entry) break;

        String raw = "";
        while (entry.available())
            raw += (char)entry.read();
        entry.close();

        DynamicJsonDocument doc(4096);
        if (deserializeJson(doc, raw) != DeserializationError::Ok)
            continue;

        JsonArray arr = doc.as<JsonArray>();
        for (JsonObject obj : arr)
        {
            if (!(obj["synced"] | false))
            {
                AttendanceRecord rec;
                rec.userId   = obj["userId"]   | "";
                rec.name     = obj["name"]     | "";
                rec.cardId   = obj["cardId"]   | "";
                rec.method   = obj["method"]   | "";
                rec.checkIn  = obj["checkIn"]  | "";
                rec.checkOut = obj["checkOut"] | "";
                rec.hours    = obj["hours"]    | "";
                rec.date     = obj["date"]     | "";
                rec.status   = obj["status"]   | "";
                rec.lateMinutes = obj["lateMinutes"] | 0;
                rec.synced   = false;
                rec.lastActivity = obj["lastActivity"] | "";
                records.push_back(rec);
            }
        }
    }
    dir.close();
    return records;
}
