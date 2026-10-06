# Security policy and data handling

## Scope and current security posture

SmartAttend is a local-network, portfolio/academic project. It is not currently designed or reviewed as a production-grade biometric system. Do not deploy it with real employees, biometric templates, or credentials until the risks below have been addressed and the deployment has been independently reviewed.

The firmware serial diagnostics were adjusted to avoid printing submitted Wi-Fi configuration and the raw device configuration file. That does not remove the larger storage and transport risks described below.

## Do not commit

- Wi-Fi SSIDs or passwords, device SD-card contents, or exported terminal configuration files.
- SQLite databases, backups, journal/WAL files, or copied LocalApplicationData folders.
- Employee names, contact details, email addresses, identifiers, RFID card IDs, PINs, attendance reports, or screenshots containing them.
- Fingerprint templates, sensor exports, certificates, private keys, API tokens, or production configuration.
- Generated IDE folders, PlatformIO output, diagnostics, or logs that may contain local paths or operational data.

The root `.gitignore` covers common local database, PlatformIO, `.env`, build, and IDE artifacts. It is a safeguard, not a replacement for review.

## Runtime data that is sensitive

- The desktop database at `%LOCALAPPDATA%\SmartAttend\smartattend.db` contains employee details, attendance, application settings, devices, RFID identifiers, PIN-related values, and queued device payloads.
- The terminal SD card stores Wi-Fi configuration, terminal settings, employee JSON records, attendance JSON records, synchronization state, and a master administrator PIN file after it is changed. The fingerprint sensor maintains its own enrolled templates; the repository does not export templates, but the physical device is still sensitive.
- Backups are copies of the database and are written under a user-selected path, or by default under a OneDrive or Documents `SmartAttend Backups` folder. Treat every backup as confidential.

## Known limitations that must be addressed for production

1. Both the desktop service and terminal service use plain HTTP and do not authenticate requests. A host on the reachable network can potentially read or alter attendance/enrollment/device data.
2. The setup access point is open, and Wi-Fi credentials are submitted over HTTP during setup.
3. Employee PIN values are not protected with a one-way password hash: the desktop stores an encoding in the `PINCards` table and the terminal stores PIN values in SD-card employee JSON. The sync path can recover and transmit them.
4. The terminal has a compile-time default administrator PIN and stores changed values on the SD card. Change it immediately in a controlled setup, and replace the design with secure provisioning before production.
5. Desktop login passwords and recovery answers use unsalted SHA-256, and the login flow has no throttling or lockout.
6. Fingerprint mapping IDs, RFID card identifiers, attendance data, names, and device configuration remain unencrypted at rest.

Restrict use to an isolated, trusted network, protect the computer and SD card physically, and avoid collecting real biometric data until these are remediated. Do not expose ports `5000` or `5001` to the Internet.

## Reporting a vulnerability

Do not open a public issue containing credentials, personal data, reproducible employee records, or exploit details. Contact the repository owner privately and include a minimal, sanitized description, affected component, impact, and safe reproduction steps. The owner should acknowledge the report, confirm remediation scope, rotate affected credentials, and remove exposed data from Git history before disclosure.

## If sensitive data is committed

1. Immediately stop publishing/pushing further copies.
2. Rotate the exposed secret or re-provision affected devices; changing a file alone is insufficient.
3. Remove the artifact from the current branch and rewrite reachable Git history if it was committed.
4. Force-push the cleaned history only after coordinating with every collaborator, then invalidate old clones/tokens as appropriate.
5. Verify with a fresh clone and a repository-wide scan before making the repository public.

