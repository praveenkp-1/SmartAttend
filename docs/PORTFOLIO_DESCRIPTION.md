# Portfolio descriptions

Use only the wording that accurately reflects your contribution and the project context. These descriptions deliberately avoid claiming production deployment, production-grade security, or ownership of data you did not create.

## CV

### Short

Built SmartAttend, a .NET WPF and ESP32-S3 attendance-management project with SQLite, RFID, fingerprint/PIN enrollment, local device communication, and offline SD-card synchronization.

### Medium

Developed SmartAttend, a desktop-and-IoT employee attendance project. The WPF application manages employees, attendance, reporting, device setup, and SQLite data, while an ESP32-S3 terminal supports RFID, fingerprint, PIN, and offline attendance storage with later synchronization.

### Detailed

Designed and implemented SmartAttend as a desktop-and-embedded attendance-management project using C#/.NET WPF, SQLite, and ESP32-S3 firmware. The project includes employee/enrollment workflows, a local HTTP protocol between desktop and device, mDNS/access-point device setup, PDF report export, and SD-card-based offline attendance delivery. The project is documented as a portfolio/academic system and identifies its remaining production security hardening work.

## LinkedIn

### Short

SmartAttend is a WPF and ESP32-S3 attendance-management project that combines SQLite, RFID, fingerprint/PIN workflows, device setup, and offline synchronization.

### Medium

I worked on SmartAttend, a local-first attendance-management project that links a C# WPF desktop application with an ESP32-S3 terminal. It explores embedded hardware integration, RFID/fingerprint/PIN enrollment, SQLite data management, local API communication, reporting, and resilience when the terminal is temporarily offline.

### Detailed

SmartAttend is a project exploring how a desktop management application and an embedded attendance terminal can work together. The Windows WPF app manages employee profiles, attendance, device settings, backups, and reports using SQLite. An ESP32-S3 terminal provides a touchscreen workflow with RFID, fingerprint, and PIN options, retains records on an SD card when connectivity drops, and later synchronizes with the desktop app. I documented the actual architecture, hardware interfaces, API contract, database schema, and security/privacy limitations so the project can be discussed honestly as a portfolio or academic build.

## GitHub repository summary

### Short

SmartAttend is a WPF and ESP32-S3 biometric attendance-management project with RFID, fingerprint, PIN, SQLite, and offline SD-card synchronization.

### Medium

SmartAttend combines a .NET 8 WPF management application with an ESP32-S3 attendance terminal. It includes employee/enrollment workflows, SQLite storage, local HTTP device communication, mDNS/access-point setup, attendance/report PDF export, and offline terminal storage.

### Detailed

SmartAttend is a local-network attendance-management project built around a .NET 8 WPF desktop app and ESP32-S3 firmware. The desktop app handles setup, employee records, attendance, reports, device configuration, database backups, and a local API. The terminal supports a TFT touch interface, RFID, fingerprint, and PIN flows; it maintains SD-card records and synchronizes attendance and employee updates when the desktop app is reachable. The repository includes accurate setup, hardware, API, database, security, and publication documentation.

## Internship application

### Short

Created a cross-domain attendance project that integrates C# WPF, SQLite, ESP32 firmware, hardware peripherals, and local network synchronization.

### Medium

For SmartAttend, I applied desktop development and embedded-systems concepts in one project: a .NET WPF application manages SQLite data and reports, while ESP32-S3 firmware handles RFID/fingerprint/PIN terminal workflows, device setup, and offline SD-card synchronization. I also documented the architecture and identified security/privacy improvements required before real deployment.

### Detailed

SmartAttend gave me practical experience connecting a desktop application to embedded hardware. I worked with .NET 8/WPF, SQLite, HTTP APIs, device discovery, PlatformIO/Arduino firmware, RFID, fingerprint-sensor integration, RTC timing, and SD-card persistence. The system queues employee changes for a terminal, records attendance locally during outages, and synchronizes when connectivity returns. I prepared developer and user documentation and performed a security/privacy review rather than overstating the project as production-ready.

## Suggested speaking points

- Explain the separation between the desktop source of record and terminal offline cache.
- Discuss why device setup needs access-point, mDNS, and subnet-discovery fallback paths.
- Describe the trade-offs of SD-card offline resilience versus sensitive local storage.
- Mention that security hardening—authenticated transport, protected credentials/PINs, and test coverage—is documented future work, not a completed production claim.

