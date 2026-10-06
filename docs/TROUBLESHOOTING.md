# Troubleshooting

Use synthetic data when investigating issues. Do not paste database contents, Wi-Fi credentials, PINs, RFID identifiers, employee information, or SD-card files into issues or chat messages.

## Desktop app will not start or database errors

- Confirm the app is running on Windows with the .NET 8 runtime/SDK required by the project.
- Check that `%LOCALAPPDATA%\SmartAttend` is writable. The application creates `smartattend.db` there at startup.
- If the database was restored, restart the application after the restore prompt completes.
- Do not delete a real database to “fix” an error. First make a secure backup outside the repository. For a disposable test setup, choose Start Fresh and create a new test database.
- A backup is a database file; keep it out of the project directory and Git.

## Device is not discovered

1. Verify the device is powered and the terminal control port is `5001`.
2. For first-time setup, connect the computer to the `SmartAttend-Setup` access point and try scan again. The setup address is `192.168.4.1`.
3. For a configured terminal, try `http://smartattend.local:5001/ping` from the same network. If it fails, mDNS may be unavailable on the computer/network.
4. Use the Settings scan fallback. It probes the local `/24` subnet, which can be slow or fail on VPNs, guest networks, VLANs, firewalls, or non-/24 networks.
5. Check that the desktop and terminal are on the same routed network and that firewall rules allow trusted local traffic to port `5001`.

## `smartattend.local` does not resolve

- Verify the terminal joined Wi-Fi successfully; mDNS starts only after a successful station connection.
- Confirm the desktop and terminal are on the same multicast-capable LAN; guest Wi-Fi and some VPNs block mDNS.
- Restart the terminal and desktop app, then use the saved device IP/subnet scan as a fallback.
- The desktop code can update a saved address to the mDNS hostname after successful ping, so verify the device entry in Settings if behavior changes after reconnect.

## Wi-Fi setup fails or device never rejoins

- Ensure the PC is temporarily connected to the terminal setup access point while posting configuration.
- Confirm the selected device address and port are `192.168.4.1:5001` during initial setup.
- Re-enter the exact target SSID/password. The terminal requires an SD card to persist its configuration.
- Check the serial monitor for connection state only; do not copy credential-bearing output into a repository or issue.
- If configuration is saved but mDNS discovery fails, let the desktop use its subnet fallback or find the terminal's lease in the trusted network’s DHCP administration interface.

## Desktop cannot reach the terminal or push settings

- Use **Test** in Device Configuration. It calls the terminal `/ping` route.
- Confirm the stored device port is `5001`; the terminal API does not run on the desktop API port.
- Verify local firewall rules and wireless client isolation settings.
- The firmware accepts settings through `POST /settings`. Auto-sync and SD-backup values are forced on by current firmware when settings are saved, so those toggles will not stay off.

## Terminal cannot send attendance to the desktop app

- Keep the WPF app running; its embedded API exists only while the desktop application is running.
- Confirm the computer and terminal are on the same network and the computer allows trusted inbound TCP traffic on port `5000`.
- Device setup attempts to send the computer’s LAN address to the terminal. Multiple VPN/virtual adapters can cause it to choose an unusable address; reconnect/configure the device after disabling unrelated adapters where practical.
- The firmware currently uses port `5000` for its desktop API client even though it stores a server-port field. Do not choose a custom desktop API port without code changes.
- An offline terminal should retain records on the SD card. Reconnect Wi-Fi and allow several sync cycles; only two pending attendance records are sent per pass.

## Employee does not appear for terminal enrollment

- Create the profile in the desktop Enrollment page and wait for a queued update to reach the terminal.
- Keep the terminal online, or open the terminal’s administrator enrollment flow, which attempts a device-update sync first.
- Check the employee is Active and has a Pending or Partial enrollment state.
- If a terminal had been factory-reset, reconnect it and allow the desktop to queue a full active-employee sync.
- Do not manually copy employee JSON between SD cards; it can transfer personal data and stale authentication mappings.

## RFID, PIN, or fingerprint does not work

- Verify the RFID reader, fingerprint sensor, and SD-card initialization messages on the terminal serial monitor.
- Check the selected desktop authentication mode matches available enrolled methods.
- For two-factor modes, the second credential must belong to the employee selected by the first credential.
- Re-enroll only with authorized test data after checking the terminal’s local cache is current.
- A “SD Not Ready” message prevents enrollment/attendance because the terminal cannot safely retain local records.
- Fingerprint matching requires both a ready sensor and a matching local employee fingerprint ID. The source does not include a method to export/import sensor templates.

## Wrong date/time, late status, or absences

- Verify the DS3231 hardware and I2C wiring; the terminal uses it as the time source.
- With Wi-Fi, the terminal attempts NTP synchronization using the configured local UTC offset (currently Sri Lanka time, UTC+05:30) and no daylight saving offset. Change firmware if the deployment is in another timezone.
- Confirm Settings work-start, work-end, and grace period are valid and pushed successfully to the terminal.
- The terminal marks absent local employees after work end. Keep the terminal time correct and review [CODE_REVIEW.md](CODE_REVIEW.md) before depending on late-after-absence behavior.

## Factory reset questions

- Use the physical terminal reset button: hold for five seconds and confirm on the touchscreen.
- Sync/export records first. Reset clears the terminal SD-card configuration, employee cache, attendance, and sync state.
- The desktop contains a remote-reset client, but the firmware route is commented out; an HTTP reset is not currently supported.

