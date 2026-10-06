# HTTP API reference

SmartAttend has two unauthenticated local HTTP services. They are not public web APIs and should only be reachable on a trusted, isolated local network. All examples below describe fields verified in the source; example values are synthetic.

## Ports and direction

| Service | Host | Port | Used by |
| --- | --- | --- | --- |
| Desktop API | WPF app, all interfaces | `5000` | ESP32 terminal |
| Terminal API | ESP32 terminal | `5001` | WPF device configuration/discovery |

Neither service currently requires authentication, authorization, TLS, an API key, or a device key. The `Devices.DeviceKey` database column is not used by the API.

## Desktop API — port 5000

### `GET /ping`

Returns desktop service availability.

**Response `200`**

```json
{ "status": "online", "server": "SmartAttend" }
```

### `GET /employee/{userId}`

Returns a minimal employee profile for a known employee ID.

**Response `200`**

```json
{ "status": "found", "userId": "EMP-10001", "name": "Example Employee", "enrolled": "Partial" }
```

**Response `404`**

```json
{ "status": "not_found" }
```

### `POST /attendance`

Creates a check-in or updates the check-out state for the supplied employee/date. The server first tries to interpret `userId` as an RFID card ID, then falls back to it as an employee ID. The terminal supplies the attendance status; the desktop uses `Present` if it is blank.

**Request**

```json
{
  "userId": "EMP-10001",
  "method": "RFID",
  "checkIn": "08:35",
  "checkOut": "",
  "status": "Present",
  "hours": "",
  "lateMinutes": 0,
  "date": "2026-01-15",
  "lastActivity": "2026-01-15 08:35"
}
```

**Response `200`** contains `status: "success"`, employee `name`, an `attendance` label, and a message. A missing/invalid JSON request produces `400 { "status": "invalid_data" }`; an unknown employee produces `404 { "status": "not_found" }`.

### `POST /enroll`

Marks an enrollment method for an employee. The terminal uses this for fingerprint enrollment.

**Request**

```json
{ "userId": "EMP-10001", "type": "fingerprint" }
```

`type` is interpreted by the desktop as `fingerprint`, `rfid`, or `pin`; an unknown type does not update an enrollment column. The endpoint currently only checks that a JSON object deserializes.

**Response `200`**

```json
{ "status": "enrolled" }
```

Invalid JSON returns `400 { "status": "invalid_data" }`.

### `POST /register-rfid`

Registers an RFID card ID and marks RFID enrollment.

**Request**

```json
{ "cardId": "A1B2C3D4", "userId": "EMP-10001" }
```

**Response `200`**: `{ "status": "registered" }`.

Invalid JSON returns `400 { "status": "invalid_data" }`. Application-level foreign-key validation is not performed before the card mapping write.

### `POST /register-pin`

Stores the employee PIN-related value and marks PIN enrollment.

**Request**

```json
{ "userId": "EMP-10001", "pin": "<employee-pin>" }
```

**Response `200`**: `{ "status": "registered" }`.

Do not send real PINs outside a controlled local setup. This endpoint uses unauthenticated HTTP, and current storage is not a secure PIN-hashing design.

### `GET /pending-employees`

Returns active employees whose aggregate enrollment state is `Pending` or `Partial`.

**Response `200`**

```json
{
  "status": "ok",
  "employees": [
    {
      "userId": "EMP-10001",
      "name": "Example Employee",
      "department": "Example Department",
      "fingerprint": false,
      "rfid": false,
      "pin": false,
      "enrolled": "Pending"
    }
  ]
}
```

The current terminal enrollment flow mainly relies on its local synchronized employee cache; this endpoint remains implemented.

### `GET /device-updates?lastSyncId={id}&limit={count}`

Returns pending desktop-to-terminal updates with IDs larger than `lastSyncId`. The desktop default is `lastSyncId=0` and `limit=25`; the current terminal asks for up to eight.

**Response `200`**

```json
{
  "status": "ok",
  "updates": [
    {
      "id": 12,
      "type": "employee_upsert",
      "userId": "EMP-10001",
      "payload": "{...}",
      "createdAt": "2026-01-15 08:30:00"
    }
  ]
}
```

`payload` is serialized JSON. Current update types are `employee_upsert`, `enrollment_request`, and `employee_delete`. It may contain sensitive employee/authentication data, so do not log or expose this response.

### `POST /device-updates/ack`

Marks queue records as synchronized after the terminal applies them.

**Request**

```json
{ "ids": [12, 13] }
```

**Response `200`**

```json
{ "status": "ok", "count": 2 }
```

An absent/empty `ids` list returns `400 { "status": "invalid_data" }`.

### `GET /enroll-info/{userId}`

Returns an employee's enrollment flags.

**Response `200`**

```json
{
  "status": "found",
  "userId": "EMP-10001",
  "name": "Example Employee",
  "fingerprintEnrolled": false,
  "rfidEnrolled": false,
  "pinEnrolled": false
}
```

An unknown employee returns `404 { "status": "not_found" }`.

## Terminal API — port 5001

The ESP32 `WebServer` serves these routes. A new terminal operates in access-point mode at `192.168.4.1`; a connected terminal advertises `smartattend.local` through mDNS. The terminal route implementation does not enforce authentication.

### `GET /ping`

**Response `200`**

```json
{ "status": "online", "ip": "<current-device-address>" }
```

### `GET /info`

Returns terminal identity, firmware version, active address, port, and SD readiness.

**Response `200`**

```json
{
  "device": "SmartAttend-001",
  "version": "2.0",
  "ip": "<current-device-address>",
  "port": 5001,
  "sdReady": true
}
```

### `GET /discovery`

Used by subnet probing to recognize the terminal.

**Response `200`**

```json
{ "type": "SmartAttend", "device": "SmartAttend-001", "ip": "<current-device-address>", "port": 5001, "version": "2.0" }
```

### `POST /config`

Saves Wi-Fi configuration to terminal SD-card `/config.json`, responds, then restarts the device.

**Request**

```json
{ "ssid": "<wifi-network-name>", "password": "<wifi-password>" }
```

**Response `200`**: `{ "status": "configured" }`.

Malformed JSON returns `400 { "status": "invalid_json" }`. The request is not encrypted or authenticated; use only during controlled setup and treat it as sensitive.

### `GET /settings`

Returns active terminal settings:

```json
{
  "authMode": 0,
  "workStart": "08:30",
  "workEnd": "17:00",
  "gracePeriod": 10,
  "buzzer": true,
  "sdBackup": true,
  "autoSync": true
}
```

### `POST /settings`

Updates supplied terminal attendance settings and redraws the home screen if it is visible.

**Request**

```json
{
  "authMode": 0,
  "workStart": "08:30",
  "workEnd": "17:00",
  "gracePeriod": 10,
  "buzzer": true,
  "sdBackup": true,
  "autoSync": true
}
```

Only `authMode`, `workStart`, `workEnd`, `gracePeriod`, and `buzzer` are conditionally read. The firmware sets `sdBackup` and `autoSync` true when saving, regardless of request value.

**Response `200`**: `{ "success": true }`; malformed JSON returns `400 { "success": false }`.

### `POST /server-config`

Saves the desktop API address/settings on the terminal.

**Request**

```json
{ "server": "<desktop-lan-address>", "port": 5000 }
```

**Response `200`**: `{ "success": true }`; malformed JSON returns `400 { "success": false }`.

The terminal client currently constructs desktop URLs with its compiled `APP_PORT` value of `5000`, so the saved port is not honored by that client. See [CODE_REVIEW.md](CODE_REVIEW.md).

### `POST /employee`

Stores a terminal-local employee record. It is implemented by the firmware but is not the normal desktop synchronization mechanism; the normal flow is `/device-updates` on the desktop service.

**Request**

```json
{
  "userId": "EMP-10001",
  "name": "Example Employee",
  "department": "Example Department",
  "rfidCard": "A1B2C3D4",
  "pin": "<employee-pin>",
  "fingerprintId": -1
}
```

**Response `200`**: `{ "status": "saved" }`; invalid JSON returns `400 { "status": "invalid_json" }`.

### Factory reset route status

The WPF view model contains a client for `POST /factory-reset`, but the firmware route is commented out and is therefore not available. The supported implemented reset path is the physical five-second button hold followed by on-device confirmation.

