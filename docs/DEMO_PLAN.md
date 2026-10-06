# Screenshot and demo plan

Create media only with a disposable database, fictional names/IDs, non-real RFID cards, and no real fingerprint data. Review every frame for Wi-Fi names, LAN addresses, usernames, email/contact fields, serial output, backup paths, QR/barcodes, and unredacted reports before publishing.

## Recommended screenshots

| # | Capture | What it should show | Privacy note |
| --- | --- | --- | --- |
| 1 | First-launch restore/start-fresh screen | New-user entry path | Do not show real backup paths or filenames. |
| 2 | Setup or login screen | Desktop access workflow | Use a fictional organization and username. Do not show a password. |
| 3 | Dashboard | Summary cards, recent activity, weekly display | Use a synthetic dataset. |
| 4 | Employee directory | Search/filter and employee state | Redact or use fictional names, contacts, IDs, and email. |
| 5 | Enrollment page | Profile form and generated test ID | Leave contact/email blank or synthetic. |
| 6 | Terminal enrollment menu | RFID/PIN/fingerprint choices | Do not capture a finger, actual card UID, or PIN entry. |
| 7 | Terminal attendance home/success state | Hardware UI and a generic success message | Use a demo identity only. |
| 8 | Device Configuration | Discovery/connect UI or saved test device status | Redact actual IP addresses, SSIDs, and hostnames if shown. |
| 9 | Attendance page | Filtered synthetic attendance table | Avoid real attendance records. |
| 10 | Reports page | Weekly/monthly report visualization | Use fictional employees and do not show a saved export path. |
| 11 | Settings/backup page | Rules, settings, or backup controls | Do not display database/OneDrive paths or account data. |
| 12 | Hardware overview | Fully assembled ESP32-S3 terminal, peripherals, and display | Avoid visible Wi-Fi labels or SD-card data. |
| 13 | Architecture image (optional) | Desktop app + device connected on a test network | Use documentation diagram rather than a real network screenshot. |

## Do not create fake screenshots

Capture the running application and real test hardware only. If a feature is not working or is not implemented (for example, remote HTTP factory reset), do not represent it as working. You may use source-verified architecture diagrams from the documentation, clearly labeled as diagrams.

## Short demo video sequence (2–3 minutes)

1. **Context (10–15 seconds):** introduce SmartAttend as a WPF + ESP32-S3 portfolio/academic project and show the sanitized architecture diagram.
2. **Desktop overview (20 seconds):** show the dashboard and employee/enrollment pages using synthetic data.
3. **Device setup (20–30 seconds):** show the device in setup mode, then the desktop Device Configuration flow at a high level. Hide credentials and real network identifiers.
4. **Synchronization (20 seconds):** create a fictional employee profile, show the generated test ID, then show the terminal’s enrollment list after sync.
5. **Enrollment and attendance (30–45 seconds):** enroll a test RFID card or demonstrate a non-biometric method, then show check-in/check-out and the desktop Attendance page.
6. **Offline resilience (20 seconds):** briefly disable network access, record a synthetic event, reconnect, and show it arrive. Do not expose SD-card contents.
7. **Reporting and close (15–20 seconds):** show the report page/PDF export prompt without a real path; mention security hardening and test coverage as future work.

## Capture checklist

- [ ] All data is fictional and you own permission to display it.
- [ ] No passwords, PINs, SSIDs, Wi-Fi QR codes, local paths, serial logs, or database file paths are visible.
- [ ] No real biometric image/template, RFID UID, employee contact data, attendance record, or device configuration is visible.
- [ ] The video and screenshots agree with the current source and documentation.
- [ ] Exports/screenshots are stored outside the repository unless they are sanitized assets intentionally approved for publication.

