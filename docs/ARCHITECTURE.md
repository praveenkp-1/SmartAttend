# SmartAttend system architecture

## Components

SmartAttend has two active components and two local persistence layers:

```text
┌──────────────────────────────────────────────────────────────────────────┐
│ Windows desktop computer                                                  │
│                                                                          │
│  WPF views  <->  ViewModels  <->  DatabaseHelper  <-> SQLite             │
│       │                            │                                     │
│       └──── desktop API service on HTTP port 5000 ────────┐              │
└───────────────────────────────────────────────────────────┼──────────────┘
                                                            │ local HTTP
                                                            ▼
┌──────────────────────────────────────────────────────────────────────────┐
│ ESP32-S3 attendance terminal                                              │
│                                                                          │
│  WebServer :5001     main.cpp workflow     TFT/touch UI                  │
│         │                 │                                               │
│         ├── MFRC522 RFID  ├── AS608-compatible fingerprint sensor        │
│         ├── DS3231 RTC    ├── buzzer / physical reset button              │
│         └── SD-card JSON: config, settings, employees, attendance, sync  │
└──────────────────────────────────────────────────────────────────────────┘
```

There is no independent cloud backend or separate .NET API project. `SmartAttend/Services/ApiServer.cs` is an ASP.NET minimal API hosted inside the WPF application. The primary firmware project is `ESP32/`. The `SmartAttend/platformio.ini` folder is an unused PlatformIO starter scaffold and not part of the active system.

## Desktop application

`App.OnStartup` initializes the local SQLite database, starts `ApiServer` in a background task, then opens a restore prompt, login window, or main window depending on the `setup_complete` and `require_login` settings.

The WPF UI uses a pragmatic MVVM split:

- `Views/` contains XAML windows/pages and several code-behind event handlers.
- `ViewModels/` loads/filters data, validates setup/settings, discovers devices, and renders PDF reports.
- `Data/DatabaseHelper.cs` is a static, direct SQLite data-access class.
- `Services/` contains the embedded API and desktop backup/restore behavior.
- `Helpers/` provides password hashing, device ping, name/avatar, and animation helpers.

The main window navigates between Dashboard, Employees, Attendance, Reports, Enrollment, and Settings.

## Desktop persistence

The SQLite file is created at:

```text
%LOCALAPPDATA%\SmartAttend\smartattend.db
```

The database schema is created via `CREATE TABLE IF NOT EXISTS` each startup. Its tables hold settings, departments, employee metadata, attendance, RFID mapping, PIN-related values, device records, and pending desktop-to-device updates. See [DATABASE.md](DATABASE.md).

## Terminal workflow

At startup the ESP32 initializes I2C/RTC, buzzer, shared SPI peripherals, RFID, fingerprint sensor, TFT/touch, and SD card. It loads SD-card configuration and settings.

- With saved Wi-Fi credentials, it joins the configured network, tries NTP-to-RTC synchronization, and advertises `smartattend.local` over mDNS.
- Without Wi-Fi credentials, it starts the `SmartAttend-Setup` access point and serves its terminal API at `192.168.4.1:5001`.
- The terminal accepts RFID, fingerprint, or PIN flows according to `authMode`. It supports the defined two-factor paths by requesting a second linked credential after the first.
- A physical reset button must be held for five seconds, then confirmed on the touchscreen. This clears terminal SD-card configuration, employee records, attendance, and sync state before rebooting.

The fingerprint sensor stores its enrolled models in the sensor. The terminal SD card stores a mapping from employee to fingerprint ID; the desktop database only stores an enrolled/not-enrolled flag.

## Device discovery and configuration

The desktop tries terminal discovery in this order:

1. `smartattend.local:5001` through mDNS.
2. `192.168.4.1:5001` while the terminal is in first-time access-point mode.
3. A concurrent probe of the local `/24` subnet's addresses on the terminal discovery endpoint.

During connection the desktop posts Wi-Fi details to the terminal, waits for the restart, rediscovers it, determines a suitable local desktop IPv4 address, posts desktop server configuration, then queues active employees for terminal download.

## Communication and synchronization

The desktop API listens on all interfaces at port `5000`. The terminal client constructs HTTP URLs to that fixed port. The terminal API listens on port `5001`.

```text
Desktop -> terminal
  POST /config          Wi-Fi config during access-point setup
  POST /server-config   desktop API host/port
  POST /settings        attendance rules and device settings

Terminal -> desktop
  POST /attendance      check-in / check-out delivery
  POST /enroll          fingerprint enrollment state
  POST /register-rfid   RFID mapping
  POST /register-pin    PIN registration
  GET  /device-updates  queued employee updates
  POST /device-updates/ack
```

The desktop `SyncQueue` is pull-based. Employee creation, edits, status changes, enrollment updates, and deletion enqueue records. The terminal stores the last acknowledged queue ID in `/sync.json` and applies up to eight downloaded updates per sync attempt. The firmware checks periodically while online and before terminal enrollment. Queue acknowledgement marks updates as synced in SQLite.

Attendance is independent of that queue. The terminal writes a daily JSON record first, marks it unsynced, and calls the desktop `/attendance` endpoint when available. Periodic delivery sends no more than two unsynced attendance records in one pass; successful records are marked synced on the SD card.

## Offline behavior

The terminal requires a ready SD card to enroll or register attendance. When the desktop service or Wi-Fi is unavailable, it retains employee/cache and daily attendance files on the SD card. A connected terminal later pushes unsynced attendance and pulls queued employee changes.

The desktop itself has no offline synchronization to another server; it is the local source of record while it is running.

## Security boundary

The implementation is intended for a trusted local network, not an Internet-facing deployment. Both HTTP services are unauthenticated, and the terminal holds sensitive SD-card state. Network access controls, secure provisioning, authentication, encrypted/protected data handling, and consent/retention controls must be added before production use. See [SECURITY.md](../SECURITY.md).

