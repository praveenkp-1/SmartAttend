# Setup guide

This guide sets up a development/test installation with no real employee, biometric, attendance, Wi-Fi, or account data in the repository.

## 1. Clone safely

```powershell
git clone <your-fork-or-repository-url>
cd smartAttendanceSystem
git status
```

The working tree should not contain copied databases, backups, SD-card files, `.env` files, or build output to be committed.

## 2. Install prerequisites

- Windows 10/11 with the .NET 8 SDK.
- Visual Studio 2022 with the **.NET desktop development** workload, or another WPF-capable editor.
- PlatformIO Core or VS Code with the PlatformIO extension for the firmware.
- An ESP32-S3 DevKitC-1-compatible board and the hardware listed in [HARDWARE.md](HARDWARE.md) for device testing.

## 3. Restore and build the WPF app

```powershell
dotnet restore .\smartAttendanceSystem.sln
dotnet build .\smartAttendanceSystem.sln
dotnet run --project .\SmartAttend\SmartAttend.csproj
```

The project targets `net8.0-windows`, so build/run it on Windows. At first launch it creates `%LOCALAPPDATA%\SmartAttend\smartattend.db`; this file is intentionally outside the repository.

## 4. Initialize desktop data

1. In the restore/start-fresh prompt, choose **Start Fresh** for a test environment.
2. Enter non-personal test values for company, administrator username/password, security question, and answer.
3. Sign in and create test departments/employees as needed.

Do not use a copied production database as a demo database. If you need a test dataset, create it locally with obviously fictional values and keep its SQLite file ignored.

## 5. Build the ESP32 firmware

Use the `ESP32/` directory as the PlatformIO project:

```powershell
cd .\ESP32
platformio run -e esp32-s3-devkitc-1
```

Connect the board and upload with an explicitly selected local port, for example:

```powershell
platformio run -e esp32-s3-devkitc-1 --target upload --upload-port <serial-port>
```

The public `platformio.ini` intentionally does not include a machine-specific upload or monitor port. PlatformIO downloads listed dependencies and creates `.pio/`, which must remain ignored.
The project pins the official `espressif32 @ 6.13.0` platform so firmware builds use a known-compatible toolchain; do not change it to an unpinned platform without validating the full terminal build.

## 6. Wire and start the terminal

Wire only the verified assignments in [HARDWARE.md](HARDWARE.md). Insert an SD card before booting; the terminal uses it for configuration, employee cache, attendance, and sync state. Do not reuse an SD card with production records for testing.

On first boot without terminal Wi-Fi configuration, the device starts the `SmartAttend-Setup` access point. Connect the desktop computer to it for setup.

## 7. Connect the device to the desktop app

1. In the desktop app, open **Settings → Device Configuration**.
2. Choose **Scan**. It tries mDNS, access-point mode, then a subnet scan.
3. Use the discovered terminal address and port `5001`.
4. Enter a terminal name and the Wi-Fi network credentials the terminal should join.
5. Choose **Connect Device** and keep the device powered while it restarts.
6. Wait for the device to be discovered again and for the desktop app to set the terminal’s desktop API address.
7. Create a synthetic employee profile and let the initial sync queue deliver it to the terminal.

The desktop service must be running and reachable from the terminal on port `5000`. Desktop firewall/network settings may need adjustment on a trusted private network.

## 8. Test an end-to-end flow

1. Create a fictional employee profile and confirm a generated employee ID.
2. On the terminal, enter its administrator flow, refresh/synchronize local data, and enroll an RFID card, PIN, or fingerprint test record.
3. Configure a compatible authentication mode in desktop Settings and save it.
4. Authenticate at the terminal once to create a check-in and again to create a check-out.
5. Confirm the record appears in the desktop Attendance page.
6. Disconnect the terminal from Wi-Fi, perform a test attendance event, reconnect, and confirm eventual delivery.

## 9. Clean up before publishing

Do not add any generated `.pio`, `bin`, `obj`, `.vs`, `.tmp`, database, backup, SD-card, or device-log files. Follow [PUBLIC_REPOSITORY_CHECKLIST.md](PUBLIC_REPOSITORY_CHECKLIST.md) before committing/pushing.
