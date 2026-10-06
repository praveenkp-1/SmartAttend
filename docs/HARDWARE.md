# Hardware reference

## Verified platform

The active firmware project is `ESP32/`, configured for the PlatformIO environment `esp32-s3-devkitc-1` with a 16 MB, OPI-PSRAM ESP32-S3 configuration. The code names the device `SmartAttend-001` and reports firmware version `2.0` through its terminal API.

## Components used by the firmware

| Component | Interface / library | Purpose |
| --- | --- | --- |
| ESP32-S3 DevKitC-1 | Arduino / PlatformIO | Terminal controller |
| ILI9341 240×320 TFT + touch | TFT_eSPI / shared SPI | Operator screen and touch input |
| MFRC522 | MFRC522 library / separate SPI pin set | RFID attendance and enrollment |
| AS608-compatible fingerprint sensor | UART2 / Adafruit Fingerprint Sensor Library | Fingerprint enrollment and matching |
| DS3231 RTC | I2C / RTClib | Local date/time and offline attendance timestamps |
| SD card | SD library / TFT SPI instance | Configuration, employee cache, attendance, and sync storage |
| Buzzer | GPIO | Success/failure/welcome feedback |
| Physical reset button | GPIO with pull-up | Starts confirmed factory-reset flow after a hold |

## Pin assignments from `ESP32/src/main.cpp`

| Function | GPIO / setting |
| --- | --- |
| TFT MOSI / MISO / SCK | 11 / 13 / 12 |
| TFT CS / DC / reset | 10 / 9 / 8 |
| Touch CS | 14 |
| SD CS | 6 |
| RFID MOSI / MISO / SCK | 5 / 15 / 4 |
| RFID CS / reset | 17 / 18 |
| Fingerprint UART RX / TX | 44 / 43 at 57600 baud |
| Buzzer | 16 |
| Factory reset button | 42, `INPUT_PULLUP` |
| RTC I2C SDA / SCL | 3 / 7 |

The PlatformIO build flags configure the display as ILI9341 with 240×320 dimensions, and set TFT CS/DC/reset/MOSI/SCLK/MISO plus `TOUCH_CS`. The firmware uses the TFT SPI instance for SD and a separate pin configuration for RFID. Verify electrical compatibility, voltage levels, display/touch controller requirements, and your exact board revision before powering the system.

## Firmware dependencies

`ESP32/platformio.ini` declares:

- `bodmer/TFT_eSPI`
- `miguelbalboa/MFRC522`
- `bblanchon/ArduinoJson`
- `adafruit/RTClib`
- `adafruit/Adafruit Fingerprint Sensor Library`

PlatformIO resolves these dependencies during build. Do not commit `.pio/` output.

## Storage and data handling

The SD card is a sensitive part of the device, not removable build media. It may contain Wi-Fi credentials, application address/settings, employee name/ID/department, RFID identifier, PIN, attendance, and synchronization files. Fingerprint templates are managed by the fingerprint sensor and the SD card stores a corresponding numeric fingerprint ID.

Factory reset requires holding the physical button for five seconds and confirming on the touchscreen. It removes device configuration, settings, sync state, employee files, and attendance files from the SD card. It does not establish a backup or synchronization guarantee; export/sync required records before reset.

## Power and assembly

The repository does not contain a wiring schematic, regulator specification, current budget, enclosure design, or board-revision matrix. Those details are **Needs verification** before recreating hardware. Use a stable supply suitable for the ESP32-S3 and all connected modules, and test each peripheral independently.

