# User guide

## Before you begin

Use only synthetic/test data while evaluating this project. The app stores employee information and attendance locally; the terminal SD card also holds sensitive configuration and authentication-related data. Review [SECURITY.md](../SECURITY.md) before enrolling real employees.

## First launch

1. Start the desktop application.
2. On a new computer/database, the **Restore or Start Fresh** window appears.
3. To restore, select a SmartAttend database backup (`.db`) that you trust. The app copies the current database to a `.old` recovery file, replaces it with the selected database, then restarts.
4. To create a new deployment, choose **Start Fresh**.
5. In the setup window, enter company name, administrator username, password, confirmation, security question, and security answer. Passwords must be at least six characters.
6. Finish setup, then sign in. Login can later be toggled in Settings.

The initial setup adds the Production, Admin, and Security departments. Add or remove departments in Settings as needed.

## Connect a new ESP32 terminal

### First-time access-point mode

When the terminal has no saved Wi-Fi configuration, it creates a setup access point named `SmartAttend-Setup`. Connect the desktop computer to that access point. The terminal uses `192.168.4.1` and its control service uses port `5001`.

The access point and setup HTTP request are not protected. Configure only on a trusted, private network, then move the terminal to its intended network.

### From the desktop app

1. Open **Settings**, then the Device Configuration section.
2. Select **Scan**. The app checks `smartattend.local`, the setup address, then tries the local subnet.
3. Enter a display name for the device, the discovered address, and port `5001`.
4. Enter the target Wi-Fi SSID and password, then choose **Connect Device**.
5. The app sends Wi-Fi configuration and waits while the terminal restarts and joins the network.
6. The app looks for the terminal using mDNS or subnet discovery, configures the desktop API address, saves the device, and queues active employees for initial synchronization.

After connection, use **Test** beside a saved device to refresh its online/offline state. **Remove** only removes the device record from the desktop app; it does not erase the terminal.

## Manage departments and employees

### Departments

In Settings, use the Departments section to add or delete department names. Deleting a department does not automatically update existing employee records; review employees before removing a department in use.

### Create an employee profile

1. Open **Enrollment**.
2. Enter name, department, and position. Contact and email are optional fields in the current UI.
3. Choose **Create Profile**.
4. Record or copy the generated employee ID. The profile begins as active and pending enrollment.

The new profile queues employee data for the terminal. Keep the terminal online or let it synchronize later. The device can also refresh its local employee cache when an administrator begins terminal enrollment.

### Employees page

Use **Employees** to browse, search, edit, change status, or delete profiles. Changes are queued for the terminal as employee upserts/deletes. Deleting a desktop employee does not remove records already present in historical desktop attendance rows.

## Enroll authentication methods on the terminal

1. Confirm the employee has synchronized to the terminal.
2. On the terminal home screen, open the device's administrator control and authenticate with the device administrator PIN.
3. Choose the workflow to enroll a pending employee, or manage/search an existing employee.
4. Select the employee.
5. Choose RFID, PIN, or Fingerprint.

For RFID, scan a new card when requested. For PIN, choose a PIN with the touchscreen keypad; the terminal rejects a PIN that already belongs to a different locally stored employee. For fingerprint, follow the two captures requested by the sensor and screen. Enrollment updates are posted to the desktop when the network is available and are retained locally otherwise.

The terminal can require any single method, RFID only, fingerprint only, PIN only, fingerprint + RFID, fingerprint + PIN, or RFID + PIN. Configure the authentication mode in the desktop Settings attendance-rules section, then save the rules to push them to connected devices.

The project has a built-in factory administrator credential in firmware. Do not rely on it as a security control; change it through the device administrator flow during controlled setup and plan a stronger provisioning design before real use.

## Record attendance

1. Confirm the terminal shows its normal home screen and the required hardware is ready.
2. Authenticate using the configured method. For a two-factor mode, complete the second prompt within the device's flow.
3. The first successful event for the employee/date is a check-in. The next successful event for the same employee/date is a check-out.
4. The terminal calculates late status from work-start time plus grace period and calculates duration at check-out.
5. The terminal saves the record to the SD card first, then sends it to the desktop app when available.

After the configured work end time, the terminal records an `Absent` entry for locally cached employees without a check-in that day. Ensure the RTC time and attendance rules are correct before relying on this behavior.

## View attendance and reports

- **Attendance** lets you choose a date, search by employee name or ID, filter by All/Present/Absent/Late, and export the shown data to PDF.
- **Reports** supports weekly (a selected end date and preceding six days) and monthly reports, an optional department filter, summary values, trend display, and PDF export.
- **Dashboard** displays employee, enrollment, daily attendance/late/absent counts, a rate, recent check-ins, and a weekly chart.

PDF exports contain personal attendance data. Save them outside the repository and share them only under your organization’s privacy policy.

## Settings

The Settings page includes:

- General organization name and administrator username.
- Attendance rules: start time, end time, grace period, and authentication mode.
- Device toggles and buzzer setting. In the current firmware, auto-sync and SD-backup options are forced on when device settings are saved.
- Device connection management.
- Department management.
- Desktop login enablement and password change.
- Database backup folder selection and manual backup.

When no backup location is set, a manual database backup uses a OneDrive `SmartAttend Backups` folder if OneDrive exists, otherwise a Documents `SmartAttend Backups` folder. The service deletes named backups older than 30 days. A backup does not include terminal SD-card data or fingerprint templates.

## Offline operation and sync queue

The terminal needs a working SD card. It caches synchronized employees and records attendance by date in JSON files. If Wi-Fi or the desktop app is unavailable, it continues to write local attendance. Once online, it retries attendance delivery and pulls pending desktop updates. It currently sends up to two offline attendance records in a synchronization pass, so a large backlog may take multiple cycles.

Keep the SD card secure and never upload its files to GitHub, cloud storage, or a support ticket without sanitizing them.

## Factory reset

The supported reset is physical: hold the terminal factory-reset button for five seconds, then confirm on the touchscreen. This deletes terminal configuration, settings, employee cache, attendance, and sync state from the SD card before rebooting into setup mode. Synchronize/export data first.

The desktop code contains an unfinished HTTP reset client, but the current firmware does not expose that endpoint; do not expect a remote reset from the desktop UI.

## Common issues

See [TROUBLESHOOTING.md](TROUBLESHOOTING.md) for discovery, Wi-Fi, mDNS, database, sensor, and synchronization issues.

