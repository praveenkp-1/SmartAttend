# Public repository checklist

## Audit result

The tracked source audit found no committed SQLite database, certificate/private-key artifact, `.env` file, or hard-coded Wi-Fi credential. The repository has local generated directories (`.vs`, `.pio`, `bin`, `obj`, `.tmp`) that are ignored and must stay untracked. A local VS Code generated configuration contains machine paths but is ignored; do not force-add it.

This is a source-publication assessment, not production security approval. The source can be published only after the manual checks below, with a clear security disclaimer. See [SECURITY.md](../SECURITY.md).

## SAFE TO PUBLISH

| Item | Why |
| --- | --- |
| `SmartAttend/` WPF source, excluding generated/build/user files | Application source and project definition; no database is tracked. |
| `ESP32/` active firmware source and `platformio.ini` | Required source/build configuration; Wi-Fi credentials are runtime supplied, not hard-coded. |
| `smartAttendanceSystem.sln`, `.gitattributes`, root `.gitignore` | Build/repository metadata. |
| `docs/`, `README.md`, `CONTRIBUTING.md`, `SECURITY.md` | Sanitized project and publication documentation. |
| `SmartAttend/Assets/smartattend.ico` | Application icon asset. |
| `.vscode/extensions.json` recommendation files | Extension recommendations only. |

## REVIEW BEFORE PUBLISHING

| Item | Reason / action |
| --- | --- |
| All source changes in `git diff` | Confirm current unpublished work is intended for the public portfolio version. |
| `ESP32/src/ui.cpp.bck` | Tracked backup source; review whether it should remain, be archived, or be removed in a separate cleanup commit. |
| `SmartAttend/platformio.ini`, `SmartAttend/src/`, `include/`, `lib/`, `test/` | Residual PlatformIO starter scaffold, not active SmartAttend firmware. Retain only if intentional; otherwise remove after confirming it is not needed. |
| Screenshots, PDFs, videos, issue attachments, and README links | Redact names, employee IDs, email/contact fields, RFID IDs, IP/SSID details, QR/barcodes, and biometric displays. |
| Git history and pull-request/issue history | The current reachable history has no obvious sensitive artifact filenames, but review historical content/attachments before changing visibility. Rewrite history and rotate any formerly exposed secret. |
| Firmware defaults and network model | The source is publishable as code, but present it as local-network/portfolio code; do not imply production-grade biometric security. |
| License choice | No license exists. Choose deliberately: MIT is permissive, Apache-2.0 adds an explicit patent grant, GPL-3.0 requires derivative source disclosure, and a proprietary/no-license posture reserves rights. Add one only after owner decision. |

## DO NOT PUBLISH

| Item | Reason |
| --- | --- |
| `%LOCALAPPDATA%\SmartAttend\smartattend.db` and any copied `.db`, `.sqlite`, `.sqlite3`, WAL, journal, or `.old` file | Contains application settings, personal employee/attendance data, RFID/PIN-related values, device data, and queue payloads. |
| SmartAttend database backups | Full sensitive database copies. |
| Terminal SD-card `/config.json`, `/settings.json`, `/sync.json`, `/employees/`, `/attendance/`, or administrator-PIN file | Includes Wi-Fi configuration, server data, employee/authentication data, attendance, and device state. |
| `.vs/`, `.pio/`, `bin/`, `obj/`, `.tmp/`, local logs, and serial captures | Generated/cache output may reveal local paths, build environment, data, or diagnostics. |
| `*.user`, `*.suo`, publish-profile user files, local `.vscode/c_cpp_properties.json`, `.vscode/launch.json` | User/machine-specific IDE data and local paths. |
| `.env*` except deliberately safe examples | Often contains credentials. |
| Certificates, private keys, deployment credentials, API tokens, and production configuration | Secrets. |
| Real employee data, fingerprints/templates, RFID IDs, PINs, personal contacts, attendance records, and unredacted exports | Personal, biometric, and operational data. |

## Final manual commands

Run before committing/pushing:

```powershell
git status --short
git diff --check
git ls-files | Select-String -Pattern '\.(db|sqlite|sqlite3|pfx|pem|key)$|(^|/)(\.env|appsettings)'
git check-ignore -v .vs ESP32/.pio SmartAttend/bin SmartAttend/obj
```

Then inspect every file listed by `git status`, check GitHub’s commit/issue/PR/attachment history, choose a license, capture sanitized screenshots, and make visibility changes manually. Do not publish from a machine with an unreviewed working tree.

