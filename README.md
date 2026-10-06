# SmartAttend

SmartAttend is a Windows desktop and ESP32-S3 attendance-management project for recording employee check-ins and check-outs with RFID, fingerprint, and PIN-based authentication modes. The WPF desktop app manages employee, attendance, device, and reporting data; an ESP32-S3 terminal performs local authentication and continues to record attendance on an SD card when the desktop app is unavailable.

> This is a portfolio/academic-style project. It is not currently hardened for production biometric deployments; read [SECURITY.md](SECURITY.md) before using it with real people or data.

## Overview

The desktop application creates its SQLite database at `%LOCALAPPDATA%\SmartAttend\smartattend.db`; no database is required in this repository. At first launch, an operator can restore a chosen backup or start setup, then create an administrator account and departments. The application also hosts a local HTTP service on port `5000` for the terminal. The terminal exposes its own setup/control service on port `5001`.

## Key features

- WPF desktop dashboard, employee directory, enrollment, attendance, reporting, and settings pages.
- SQLite-backed employee, department, attendance, device, and synchronization-queue records.
- Generated employee IDs and enrollment status tracking for RFID, fingerprint, and PIN methods.
- ESP32-S3 terminal with TFT touch UI, MFRC522 RFID reader, AS608-compatible UART fingerprint sensor, DS3231 RTC, buzzer, and SD-card storage.
- RFID, fingerprint, PIN, and implemented two-factor combinations (fingerprint + RFID, fingerprint + PIN, RFID + PIN).
- Device setup through a temporary access point, mDNS (`smartattend.local`), or subnet probing.
- Offline attendance persistence on the terminal SD card and later delivery to the desktop app.
- Device-update pull queue for employee changes; PDF attendance and report exports; local database backup and restore.

## System architecture

```text
                    HTTP :5000 (desktop API)
┌──────────────────┐ <------------------------ ┌────────────────────┐
│ WPF desktop app  │                            │ ESP32-S3 terminal │
│ - Views/MVVM     │ -------------------------> │ - touch UI         │
│ - SQLite         │  employee update pull      │ - RFID / FP / PIN  │
│ - local API      │                            │ - SD + RTC         │
└────────┬─────────┘                            └─────────┬──────────┘
         │                                                │
         ▼                                                ▼
%LOCALAPPDATA%\SmartAttend\smartattend.db          SD-card JSON files
                                                    (config, employees,
                                                     attendance, sync state)
```

The detailed system view is in [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md). The two HTTP interfaces are documented in [docs/API.md](docs/API.md).

## Technologies used

| Area | Implementation in this repository |
| --- | --- |
| Desktop application | C#, .NET 8 (`net8.0-windows`), WPF |
| Desktop data | `Microsoft.Data.Sqlite` / SQLite |
| Embedded firmware | Arduino framework with PlatformIO |
| Board | ESP32-S3 DevKitC-1 environment (`esp32-s3-devkitc-1`) |
| Terminal libraries | TFT_eSPI, MFRC522, ArduinoJson, RTClib, Adafruit Fingerprint Sensor Library |
| Networking | HTTP, mDNS, Wi-Fi access-point setup |
| PDF export | QuestPDF |

## Hardware

The firmware defines the verified wiring for the ESP32-S3 terminal, including TFT/touch, RFID, SD, fingerprint UART, RTC, buzzer, and physical factory-reset button. See [docs/HARDWARE.md](docs/HARDWARE.md) before wiring or flashing hardware.

## Desktop application

The WPF app contains pages for:

- Dashboard: summary statistics, recent activity, and a weekly attendance visual.
- Employees: searchable employee management and status changes.
- Enrollment: profile creation and the hand-off of a generated employee ID to the terminal.
- Attendance: date/search/status filters and PDF export.
- Reports: weekly or monthly reports, trend data, and PDF export.
- Settings: organization details, rules, devices, departments, login/password, and database backups.

The full operator workflow is in [docs/USER_GUIDE.md](docs/USER_GUIDE.md).

## Attendance and enrollment workflow

1. An operator creates an employee profile in the desktop app. This queues an employee update for the terminal.
2. A connected terminal pulls queued updates, normally every 30 seconds and also before the terminal enrollment flow.
3. An authorized operator selects the employee on the terminal and enrolls RFID, PIN, and/or fingerprint methods.
4. The terminal records a check-in or check-out locally, calculates late status using configured work rules, and posts the record to the desktop app when reachable.
5. If the app is unreachable, the record remains on the SD card and is retried later.

## Device configuration and synchronization

On a new terminal, the firmware starts an access point named `SmartAttend-Setup` at `192.168.4.1`. The desktop app's Device Configuration section attempts discovery in this order: `smartattend.local`, the setup address, then a `/24` subnet scan. It posts Wi-Fi credentials to the terminal, waits for reboot, sends the desktop computer's address for the embedded API, and queues a full employee sync.

The queue is a one-way desktop-to-device pull mechanism. The terminal acknowledges applied updates; attendance delivery is a separate terminal-to-desktop flow. Details and limitations are in [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md).

## Installation and running

Prerequisites:

- Windows with the .NET 8 SDK and a WPF-capable IDE such as Visual Studio 2022.
- PlatformIO and a compatible ESP32-S3 terminal only when working with the firmware.

```powershell
git clone <your-fork-or-repository-url>
cd smartAttendanceSystem
dotnet restore .\smartAttendanceSystem.sln
dotnet build .\smartAttendanceSystem.sln
dotnet run --project .\SmartAttend\SmartAttend.csproj
```

Open `ESP32/platformio.ini` as the firmware project. The repository intentionally does not fix an upload port: select the port on the development machine or pass it through PlatformIO. Complete instructions are in [docs/SETUP.md](docs/SETUP.md) and [docs/DEVELOPER_GUIDE.md](docs/DEVELOPER_GUIDE.md).

## Database

Database initialization happens automatically during application startup. The schema uses `CREATE TABLE IF NOT EXISTS`, so it creates a new empty database but does not include an explicit migration framework. The database is intentionally not versioned in Git because it holds personal and attendance data. See [docs/DATABASE.md](docs/DATABASE.md).

## Project structure

```text
smartAttendanceSystem/
├── ESP32/                         # Primary PlatformIO/Arduino terminal firmware
│   ├── platformio.ini
│   └── src/
│       ├── main.cpp               # hardware, terminal workflow, device HTTP API
│       ├── network.*              # desktop API client
│       ├── storage.*              # SD-card JSON persistence
│       └── ui*                    # TFT/touch rendering and admin UI
├── SmartAttend/                   # .NET 8 WPF application
│   ├── Data/DatabaseHelper.cs     # SQLite schema and data access
│   ├── Services/                  # embedded desktop API, backup, restore
│   ├── ViewModels/                # presentation logic
│   ├── Views/                     # WPF windows and pages
│   ├── Models/ and Helpers/
│   └── SmartAttend.csproj
├── docs/                          # user, developer, API, hardware, and audit docs
├── smartAttendanceSystem.sln
├── CONTRIBUTING.md
└── SECURITY.md
```

`SmartAttend/platformio.ini` and its `src/` are an unused PlatformIO starter scaffold that is separate from the WPF project and the primary `ESP32/` firmware. It is retained for review rather than presented as an active part of SmartAttend.

## Screenshots and demo

No screenshots or demo media are committed. Capture only sanitized data before publishing. [docs/DEMO_PLAN.md](docs/DEMO_PLAN.md) provides an exact capture plan.

## Limitations and future improvements

- The desktop client is Windows-only because it uses WPF.
- There are no automated unit/integration tests in the repository.
- The embedded and desktop HTTP services use unauthenticated HTTP on the local network; do not expose them outside a trusted, isolated network.
- Current terminal storage contains sensitive operational data, including Wi-Fi configuration and employee authentication data; do not publish or share the SD-card contents.
- Production use should add authenticated transport, protected secret/PIN handling, authentication throttling, database migrations, test coverage, and a documented data-retention/consent process.

## Documentation

- [User guide](docs/USER_GUIDE.md)
- [Developer guide](docs/DEVELOPER_GUIDE.md)
- [System architecture](docs/ARCHITECTURE.md)
- [API reference](docs/API.md)
- [Database schema](docs/DATABASE.md)
- [Hardware guide](docs/HARDWARE.md)
- [Troubleshooting](docs/TROUBLESHOOTING.md)
- [Public-repository checklist](docs/PUBLIC_REPOSITORY_CHECKLIST.md)
- [Code review](docs/CODE_REVIEW.md)
- [Portfolio descriptions](docs/PORTFOLIO_DESCRIPTION.md)

## License

No license has been selected for this repository. Do not assume reuse permission. The repository owner should choose a license before making the project public; see the recommendation in [docs/PUBLIC_REPOSITORY_CHECKLIST.md](docs/PUBLIC_REPOSITORY_CHECKLIST.md).

## Suggested GitHub metadata

- **Suggested repository name:** `smartattend`
- **Description / About:** SmartAttend is a WPF and ESP32-S3 biometric attendance-management project with RFID, fingerprint, PIN, SQLite, and offline SD-card synchronization.
- **Tagline:** Local-first biometric attendance for a connected ESP32 terminal and desktop management app.
- **Topics:** `csharp`, `dotnet`, `wpf`, `esp32`, `esp32-s3`, `platformio`, `sqlite`, `mvvm`, `iot`, `rfid`, `fingerprint`, `attendance-system`, `embedded-systems`

