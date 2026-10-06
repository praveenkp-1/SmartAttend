# SmartAttend — Code Review

This document summarizes the main known limitations in the current SmartAttend implementation. It is a lightweight review, not a formal security audit.

## Known Limitations

### Security

* **Unauthenticated HTTP APIs** — Desktop (`5000`) and terminal (`5001`) APIs currently use HTTP without authentication or TLS. The system is intended for a trusted local network.
* **PIN protection** — Current PIN handling uses recoverable values for offline terminal authentication and should be replaced with a secure credential-verification design for production.
* **Password hashing** — Desktop credentials currently use SHA-256 without a password-specific work factor. A modern password-hashing algorithm should be used for production.
* **Terminal credentials** — The administrator credential and sensitive terminal data require stronger protection and secure first-run provisioning.

### Reliability

* **Attendance synchronization** — The terminal should only mark attendance as synchronized after receiving an expected successful (`2xx`) response.
* **Late attendance after absence** — An automatically generated `Absent` record can interfere with a later valid check-in and should be handled as a separate attendance state.
* **Synchronization acknowledgement** — Queue acknowledgements are not currently bound to an authenticated device.

### Configuration

* **Server port** — The terminal stores a server port but currently uses the compiled desktop API port (`5000`) for network requests.
* **Factory reset** — The desktop contains a remote `/factory-reset` client path, while the implemented firmware reset uses the physical five-second button hold and touchscreen confirmation.

### Database & Maintenance

* **Relationships** — Some SQLite relationships are not enforced with foreign keys, so related records require explicit cleanup.
* **Report duration** — Duration values such as `8h 30m` should eventually be stored/calculated as numeric minutes for accurate reporting.
* **Inactive files** — The repository contains an unused PlatformIO starter scaffold and a `.bck` firmware source file. These can be removed during repository cleanup.

## Recommended Future Improvements

1. Add authenticated and protected desktop ↔ terminal communication.
2. Improve credential and sensitive-data protection.
3. Add automated tests for attendance and synchronization edge cases.
4. Add database migrations and stronger relational constraints.
5. Add CI builds for the WPF application and ESP32 firmware.

These limitations do not require a complete redesign of SmartAttend; they represent areas for future security hardening, reliability improvements, and maintenance.
