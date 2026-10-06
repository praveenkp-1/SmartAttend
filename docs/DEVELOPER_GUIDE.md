# Developer guide

## Development environment

The solution `smartAttendanceSystem.sln` contains one WPF project, `SmartAttend/SmartAttend.csproj`, targeting .NET 8 for Windows. The active embedded project is `ESP32/platformio.ini`, targeting `esp32-s3-devkitc-1` using the Arduino framework. It pins the official `espressif32 @ 6.13.0` platform for repeatable firmware builds. There are no test projects or CI workflows in the repository.

Use:

- .NET 8 SDK and Windows for WPF build/debug.
- Visual Studio 2022 with .NET desktop development for the desktop app.
- PlatformIO for the firmware, including the libraries declared in `ESP32/platformio.ini`.
- A test ESP32-S3 terminal and disposable SD card with fictional test data for end-to-end validation.

## Desktop architecture

`App.xaml.cs` is the composition point: it initializes SQLite, starts the embedded `ApiServer`, then opens setup/login/main windows. `MainWindow` creates views directly for page navigation.

The code uses a mixed MVVM approach. Most page data and workflows are in `ViewModels/`, while WPF code-behind still owns several event handlers and presentation updates. `DatabaseHelper` centralizes all current SQL and schema creation as static methods; there is no repository abstraction, dependency injection container, or migration package.

Key desktop services:

- `ApiServer`: ASP.NET minimal API embedded in the WPF process, listening on HTTP port 5000.
- `BackupService`: copies the SQLite file and removes dated backups beyond 30 days.
- `RestoreService`: copies a user-selected database over the live database and restarts the app.
- `DeviceService` / `DeviceConfigViewModel`: terminal ping, discovery, setup, and configuration.

## Database architecture

The runtime SQLite database path is `%LOCALAPPDATA%\SmartAttend\smartattend.db`. Initialization uses idempotent `CREATE TABLE IF NOT EXISTS` calls. See [DATABASE.md](DATABASE.md) for the actual schema and its logical—not database-enforced—relationships.

When adding a table or column:

1. Update `DatabaseHelper.Initialize()` with a backward-compatible creation/migration path.
2. Add parameterized CRUD methods and avoid interpolating data into SQL.
3. Update model/view-model/UI use sites.
4. Update [DATABASE.md](DATABASE.md) and assess whether any new data is personal/sensitive.
5. Test a fresh database and an existing database. A new `CREATE TABLE IF NOT EXISTS` statement alone will not add columns to an existing table.

## Firmware architecture

`ESP32/src/main.cpp` owns boot, hardware setup, state handling, terminal HTTP routes, enrollment, attendance, time, sync scheduling, and reset handling. The firmware uses:

- `network.*` for HTTP calls to the desktop API.
- `storage.*` for SD-card JSON config, settings, employee records, daily attendance, and last device sync ID.
- `ui.*` and `ui_admin.cpp` for TFT/touch rendering and administrator/enrollment input.

The terminal's default settings are any authentication method, work day 08:30–17:00, ten-minute grace period, buzzer enabled, and automatic sync/SD backup flags enabled. Wi-Fi configuration and server configuration are stored on the SD card. The terminal uses mDNS hostname `smartattend` and serves control endpoints on port 5001.

## Networking and API contract

Read [API.md](API.md) before changing either side. The app is the local source of employee records; the terminal pulls queue updates and acknowledges them. The terminal is the first writer of device attendance; it posts it to the desktop API and retains it locally until delivery succeeds.

Important current constraints:

- The desktop API listens on all network interfaces at port 5000.
- The terminal client composes desktop URLs with port 5000 even though a server port is saved in terminal settings.
- Device discovery checks mDNS, setup AP, then concurrently probes a `/24` subnet.
- The terminal has no TLS or request authentication. Keep testing on an isolated network.

## Build and debug

Desktop:

```powershell
dotnet restore .\smartAttendanceSystem.sln
dotnet build .\smartAttendanceSystem.sln
dotnet run --project .\SmartAttend\SmartAttend.csproj
```

Firmware:

```powershell
cd .\ESP32
platformio run -e esp32-s3-devkitc-1
platformio run -e esp32-s3-devkitc-1 --target upload --upload-port <serial-port>
platformio device monitor --port <serial-port> --baud 115200
```

The project uses a 115200 baud monitor configuration. The changed firmware avoids dumping Wi-Fi configuration values to serial output, but operational logs can still identify device state; keep logs out of commits.

## Error handling and diagnosis

Desktop networking methods commonly catch exceptions and return `false`/an empty result. The UI exposes these as connection messages. Firmware network calls use a 5-second HTTP timeout and preserve attendance locally if delivery fails. Start debugging with:

1. `GET /ping` on the terminal (`:5001`) and desktop (`:5000`).
2. Terminal serial output, without copying secrets or personal data.
3. SD-card availability and files on a disposable test card.
4. Desktop database state with a local SQLite viewer only on synthetic data.

## Adding a feature safely

1. Identify source of truth and offline behavior before writing UI.
2. Define protocol fields and failure/retry behavior if the feature crosses desktop/terminal boundary.
3. Update both implementations atomically when changing API payloads.
4. Validate input at the API boundary and preserve backward compatibility or provide migration.
5. Test online, offline, device reboot, and malformed-payload cases with synthetic data.
6. Update user, developer, API, database, and security documentation as applicable.

## Updating firmware

Before uploading, back up or use a disposable SD card; employee cache, attendance, Wi-Fi configuration, and device settings are not source files. Build first, then upload to the explicitly selected serial port. Re-test peripheral initialization, setup AP, mDNS, device configuration, employee sync, each enabled authentication flow, attendance, and offline retry.

## Security requirements for contributors

Do not use real biometric, employee, attendance, Wi-Fi, or account data during development. Never commit database/backup/SD-card content. Treat plain HTTP, local PIN storage, default device administration, and absence of API authentication as known high-priority work items; do not present the system as production-ready without addressing them. See [SECURITY.md](../SECURITY.md).
