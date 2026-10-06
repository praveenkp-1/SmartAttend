# Contributing to SmartAttend

Thanks for contributing. This project combines a .NET 8 WPF desktop app and ESP32-S3 firmware, so changes should preserve the protocol between them.

## Development setup

1. Read [docs/DEVELOPER_GUIDE.md](docs/DEVELOPER_GUIDE.md), [docs/API.md](docs/API.md), and [SECURITY.md](SECURITY.md).
2. Build the WPF solution with the .NET 8 SDK on Windows.
3. Use the `ESP32/` folder—not the residual `SmartAttend/` PlatformIO starter scaffold—for firmware work.
4. Keep a test device/SD card and test database free of real employee and biometric data.

## Branches, commits, and pull requests

- Create a focused branch from the current integration branch.
- Use clear, imperative commit messages, for example `Add validation for device settings`.
- Keep desktop and firmware protocol changes in the same pull request when one depends on the other.
- Describe manual test steps, device/board used, and any protocol/schema impact in the pull request.
- Do not mix formatting-only changes with functional changes unless they are inseparable.

## Coding expectations

- Follow the existing C# naming conventions and WPF/MVVM separation where the current app uses it.
- Keep SQL parameterized. If a column name must be selected dynamically, constrain it to a fixed allow-list.
- Keep HTTP request and response fields backward-compatible, or document and test the coordinated desktop/firmware migration.
- In firmware, avoid blocking loops where possible, check hardware/network failures, and never log credentials or personal data.
- Add or update documentation when changing hardware wiring, API behavior, runtime storage, or setup steps.

## Testing expectations

At minimum, verify the affected desktop build and, for firmware changes, a PlatformIO build for `esp32-s3-devkitc-1`. Manually test relevant flows with synthetic data: setup, employee sync, enrollment, attendance, offline delivery, and settings propagation. Automated tests do not currently exist; adding focused tests is welcome.

## Security and privacy rules

Never commit credentials, `.env` files, database files, SD-card contents, backups, logs, real employee details, RFID identifiers, PINs, fingerprint templates, or screenshots containing production data. Check `git status --ignored` and [docs/PUBLIC_REPOSITORY_CHECKLIST.md](docs/PUBLIC_REPOSITORY_CHECKLIST.md) before opening a pull request.

Report security issues privately as described in [SECURITY.md](SECURITY.md), not in a public issue.

