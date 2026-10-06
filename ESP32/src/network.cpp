#include "network.h"
#include <HTTPClient.h>
#include <ArduinoJson.h>
#include <WiFi.h>

#define HTTP_TIMEOUT_MS 5000

static String appBaseUrl(String appIP)
{
    String host = appIP;
    host.trim();

    if (host.startsWith("http://"))
        host = host.substring(7);
    else if (host.startsWith("https://"))
        host = host.substring(8);

    int slash = host.indexOf('/');
    if (slash >= 0)
        host = host.substring(0, slash);

    if (host.indexOf(':') < 0)
        host += ":" + String(APP_PORT);

    return "http://" + host;
}

static bool postJSON(String url, String body)
{
    if (WiFi.status() != WL_CONNECTED) return false;

    HTTPClient http;
    http.begin(url);
    http.setTimeout(HTTP_TIMEOUT_MS);
    http.addHeader("Content-Type", "application/json");
    int code = http.POST(body);
    http.end();

    Serial.print("[HTTP POST] ");
    Serial.print(url);
    Serial.print(" -> ");
    Serial.println(code);

    return (code > 0 && code < 500);
}

// ── Send attendance ────────────────────────────────────
bool Network_SendAttendance(String appIP, AttendanceRecord& rec)
{
    StaticJsonDocument<320> doc;
    doc["userId"]      = rec.userId;
    doc["method"]      = rec.method;
    doc["checkIn"]     = rec.checkIn;
    doc["checkOut"]    = rec.checkOut;
    doc["status"]      = rec.status;
    doc["hours"]       = rec.hours;
    doc["lateMinutes"] = rec.lateMinutes;
    doc["date"]        = rec.date;
    doc["lastActivity"] = rec.lastActivity;

    String body;
    serializeJson(doc, body);

    return postJSON(appBaseUrl(appIP) + "/attendance", body);
}

// ── Register RFID card ─────────────────────────────────
bool Network_RegisterRFID(String appIP, String cardId,
    String userId)
{
    StaticJsonDocument<128> doc;
    doc["cardId"] = cardId;
    doc["userId"] = userId;

    String body;
    serializeJson(doc, body);

    return postJSON(appBaseUrl(appIP) + "/register-rfid", body);
}

// ── Register PIN ───────────────────────────────────────
bool Network_RegisterPIN(String appIP, String userId,
    String pin)
{
    StaticJsonDocument<128> doc;
    doc["userId"] = userId;
    doc["pin"]    = pin;

    String body;
    serializeJson(doc, body);

    return postJSON(appBaseUrl(appIP) + "/register-pin", body);
}

// ── Get pending employees ──────────────────────────────
bool Network_RegisterFingerprint(String appIP, String userId)
{
    StaticJsonDocument<128> doc;
    doc["userId"] = userId;
    doc["type"] = "fingerprint";

    String body;
    serializeJson(doc, body);

    return postJSON(appBaseUrl(appIP) + "/enroll", body);
}

int Network_GetPendingEmployees(String appIP,
    PendingEmployee* list, int maxCount)
{
    if (WiFi.status() != WL_CONNECTED)
    {
        Serial.println("[PENDING] WiFi not connected");
        return 0;
    }

    HTTPClient http;
    String url = appBaseUrl(appIP) + "/pending-employees";

    Serial.print("[PENDING] GET ");
    Serial.println(url);

    http.begin(url);
    http.setTimeout(HTTP_TIMEOUT_MS);
    int code = http.GET();

    Serial.print("[PENDING] HTTP code = ");
    Serial.println(code);

    if (code != 200)
    {
        http.end();
        return 0;
    }

    String body = http.getString();
    http.end();

    DynamicJsonDocument doc(4096);
    DeserializationError err = deserializeJson(doc, body);
    if (err)
    {
        Serial.print("[PENDING] JSON error: ");
        Serial.println(err.c_str());
        return 0;
    }

    JsonArray arr = doc["employees"].as<JsonArray>();
    if (arr.isNull())
    {
        Serial.println("[PENDING] Missing employees array");
        return 0;
    }

    int count     = 0;

    for (JsonObject emp : arr)
    {
        if (count >= maxCount)
            break;

        list[count].userId =
            emp["userId"] | "";
        list[count].name =
            emp["name"] | "";
        list[count].department =
            emp["department"] | "";
        list[count].rfidDone =
            emp["rfid"] | false;
        list[count].pinDone =
            emp["pin"] | false;
        list[count].fpDone =
            emp["fingerprint"] | false;

        count++;
    }

    Serial.print("Parsed Employees = ");
    Serial.println(count);
    return count;
}

bool Network_GetDeviceUpdates(String appIP, long lastSyncId,
    DeviceUpdate* updates, int maxCount, int& count)
{
    count = 0;

    if (WiFi.status() != WL_CONNECTED)
    {
        Serial.println("[DEVICE SYNC] WiFi not connected");
        return false;
    }

    HTTPClient http;
    String url = appBaseUrl(appIP) +
        "/device-updates?lastSyncId=" + String(lastSyncId) +
        "&limit=" + String(maxCount);

    Serial.print("[DEVICE SYNC] GET ");
    Serial.println(url);

    http.begin(url);
    http.setTimeout(HTTP_TIMEOUT_MS);
    int code = http.GET();

    Serial.print("[DEVICE SYNC] HTTP code = ");
    Serial.println(code);

    if (code != 200)
    {
        http.end();
        return false;
    }

    String body = http.getString();
    http.end();

    DynamicJsonDocument doc(8192);
    DeserializationError err = deserializeJson(doc, body);
    if (err)
    {
        Serial.print("[DEVICE SYNC] JSON error: ");
        Serial.println(err.c_str());
        return false;
    }

    JsonArray arr = doc["updates"].as<JsonArray>();
    if (arr.isNull())
    {
        Serial.println("[DEVICE SYNC] Missing updates array");
        return false;
    }

    for (JsonObject item : arr)
    {
        if (count >= maxCount)
            break;

        updates[count].id = item.containsKey("Id")
            ? item["Id"].as<long>()
            : item["id"].as<long>();
        updates[count].type = item.containsKey("Type")
            ? item["Type"].as<String>()
            : item["type"].as<String>();
        updates[count].userId = item.containsKey("UserId")
            ? item["UserId"].as<String>()
            : item["userId"].as<String>();
        updates[count].payload = item.containsKey("Payload")
            ? item["Payload"].as<String>()
            : item["payload"].as<String>();

        if (updates[count].id > 0 &&
            !updates[count].type.isEmpty())
        {
            count++;
        }
    }

    Serial.print("[DEVICE SYNC] Updates = ");
    Serial.println(count);
    return true;
}

bool Network_AckDeviceUpdates(String appIP, long* ids, int count)
{
    if (count <= 0)
        return true;

    StaticJsonDocument<512> doc;
    JsonArray arr = doc.createNestedArray("ids");

    for (int i = 0; i < count; i++)
        arr.add(ids[i]);

    String body;
    serializeJson(doc, body);

    return postJSON(appBaseUrl(appIP) + "/device-updates/ack", body);
}
