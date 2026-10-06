#include "ui.h"
#include "storage.h"

#define W 240
#define H 320

extern const uint16_t C_BG       = 0x0882; // #0D1117
extern const uint16_t C_CARD     = 0x10C4; // #161B22
extern const uint16_t C_BORDER   = 0x2125; // #21262D
extern const uint16_t C_TEXT     = 0xE77E; // #E6EDF3
extern const uint16_t C_MUTED    = 0x8CB3; // #8B949E
extern const uint16_t C_BLUE     = 0x5D3F; // #58A6FF
extern const uint16_t C_GREEN    = 0x3DCA; // #3FB950
extern const uint16_t C_RED      = 0xFA89; // #F85149
extern const uint16_t C_AMBER    = 0xD4C4; // #D29922
extern const uint16_t C_DARK     = 0x31A7; // #30363D

extern const uint16_t BG_HOME_1  = 0x0043; // #050A1A
extern const uint16_t BG_HOME_2  = 0x08A5; // #0A1628
static const uint16_t BG_SCAN_2  = 0x0884; // #091020
static const uint16_t BG_OK_1    = 0x00A1; // #051A0A
static const uint16_t BG_OK_2    = 0x08E2; // #0A1F10
static const uint16_t BG_BAD_1   = 0x1800; // #1A0505
static const uint16_t BG_BAD_2   = 0x2001; // #1F0A0A
static const uint16_t BG_OUT_2   = 0x0862; // #0A0D14

static char lastTimeStr[6] = "";
static char lastDateStr[20] = "";
static bool lastWifi = false;

uint16_t mix565(TFT_eSPI& tft, uint16_t a, uint16_t b, uint8_t amount)
{
    uint8_t ar = ((a >> 11) & 0x1F) << 3;
    uint8_t ag = ((a >> 5) & 0x3F) << 2;
    uint8_t ab = (a & 0x1F) << 3;
    uint8_t br = ((b >> 11) & 0x1F) << 3;
    uint8_t bg = ((b >> 5) & 0x3F) << 2;
    uint8_t bb = (b & 0x1F) << 3;

    uint8_t r = ar + (((int16_t)br - ar) * amount) / 255;
    uint8_t g = ag + (((int16_t)bg - ag) * amount) / 255;
    uint8_t bl = ab + (((int16_t)bb - ab) * amount) / 255;
    return tft.color565(r, g, bl);
}

void fillGradient(TFT_eSPI& tft, uint16_t top, uint16_t bottom)
{
    for (int y = 0; y < H; y++)
    {
        uint8_t amount = (uint8_t)((y * 255) / (H - 1));
        tft.drawFastHLine(0, y, W, mix565(tft, top, bottom, amount));
    }
}

void textCenter(TFT_eSPI& tft, const String& txt, int y,
                uint16_t fg, uint16_t bg, uint8_t size = 1)
{
    tft.setTextColor(fg, bg);
    tft.setTextSize(size);
    tft.setTextDatum(TL_DATUM);
    tft.setCursor((W - tft.textWidth(txt)) / 2, y);
    tft.print(txt);
}

void textLeft(TFT_eSPI& tft, const String& txt, int x, int y,
              uint16_t fg, uint16_t bg, uint8_t size = 1)
{
    tft.setTextColor(fg, bg);
    tft.setTextSize(size);
    tft.setTextDatum(TL_DATUM);
    tft.setCursor(x, y);
    tft.print(txt);
}

static void drawWifi(TFT_eSPI& tft, int x, int y, bool connected)
{
    uint16_t color = connected ? C_GREEN : C_RED;
    const uint8_t heights[] = {5, 8, 11, 15};
    for (int i = 0; i < 4; i++)
    {
        uint16_t barColor = connected || i == 0 ? color : C_DARK;
        tft.fillRoundRect(x + i * 6, y + 15 - heights[i], 4, heights[i],
                          1, barColor);
    }
}

static void divider(TFT_eSPI& tft, int y, uint16_t bg)
{
    for (int x = 24; x < W - 24; x++)
    {
        uint16_t col = (x < 58 || x > W - 58) ? bg : C_BORDER;
        tft.drawPixel(x, y, col);
    }
}

static void progress(TFT_eSPI& tft, int x, int y, int w, int h,
                     int pct, uint16_t fill)
{
    tft.fillRoundRect(x, y, w, h, h / 2, C_BORDER);
    int fw = (w * pct) / 100;
    if (fw > 0)
        tft.fillRoundRect(x, y, fw, h, h / 2, fill);
}

static void animatedProgress(TFT_eSPI& tft, int x, int y, int w, int h,
                             uint16_t fill, int startPct,
                             int endPct, int stepDelayMs)
{
    int lastFw = 0;
    tft.fillRoundRect(x, y, w, h, h / 2, C_BORDER);

    for (int pct = startPct; pct <= endPct; pct += 2)
    {
        int fw = (w * pct) / 100;
        if (fw > lastFw)
        {
            tft.fillRoundRect(x, y, fw, h, h / 2, fill);
            lastFw = fw;
        }
        delay(stepDelayMs);
    }

    if (endPct < 100)
        tft.fillRoundRect(x + lastFw, y, w - lastFw, h, h / 2, C_BORDER);
}

static void indeterminateProgress(TFT_eSPI& tft, int x, int y, int w, int h,
                                  uint16_t bg, uint16_t fill,
                                  int cycles, int stepDelayMs)
{
    int chunk = w / 4;
    uint16_t tail = mix565(tft, bg, fill, 80);

    tft.fillRoundRect(x, y, w, h, h / 2, C_BORDER);

    for (int c = 0; c < cycles; c++)
    {
        for (int pos = -chunk; pos <= w; pos += 6)
        {
            tft.fillRoundRect(x, y, w, h, h / 2, C_BORDER);

            int tx = x + max(pos - 12, 0);
            int tw = min(chunk + 12, w - max(pos - 12, 0));
            if (tw > 0)
                tft.fillRoundRect(tx, y, tw, h, h / 2, tail);

            int sx = x + max(pos, 0);
            int sw = min(chunk, w - max(pos, 0));
            if (sw > 0)
                tft.fillRoundRect(sx, y, sw, h, h / 2, fill);

            delay(stepDelayMs);
        }
    }
}

static void badge(TFT_eSPI& tft, int x, int y, int w, int h,
                  const String& label, uint16_t bg, uint16_t accent)
{
    uint16_t fill = mix565(tft, bg, accent, 28);
    uint16_t border = mix565(tft, bg, accent, 75);
    tft.fillRoundRect(x, y, w, h, h / 2, fill);
    tft.drawRoundRect(x, y, w, h, h / 2, border);
    textCenter(tft, label, y + (h - 8) / 2, accent, fill);
}

static void circleIcon(TFT_eSPI& tft, int cx, int cy, int r,
                       uint16_t bg, uint16_t accent)
{
    tft.fillCircle(cx, cy, r, mix565(tft, bg, accent, 18));
    tft.drawCircle(cx, cy, r, mix565(tft, bg, accent, 95));
    tft.drawCircle(cx, cy, r - 1, mix565(tft, bg, accent, 55));
}

static void rfidGlyph(TFT_eSPI& tft, int cx, int cy, uint16_t color)
{
    tft.drawRoundRect(cx - 18, cy - 12, 36, 24, 3, color);
    tft.drawFastHLine(cx - 11, cy - 5, 22, color);
    tft.drawFastHLine(cx - 11, cy + 1, 15, color);
    tft.fillCircle(cx + 11, cy + 5, 2, color);
}

static void fingerprintGlyph(TFT_eSPI& tft, int cx, int cy, uint16_t color)
{
    tft.drawCircle(cx, cy - 1, 20, mix565(tft, BG_SCAN_2, color, 120));
    tft.drawCircle(cx, cy - 1, 14, color);
    tft.drawCircle(cx, cy - 1, 8, mix565(tft, BG_SCAN_2, color, 170));

    tft.drawFastVLine(cx, cy - 17, 35, color);
    tft.drawFastHLine(cx - 14, cy - 9, 28, color);
    tft.drawFastHLine(cx - 17, cy, 34, color);
    tft.drawFastHLine(cx - 11, cy + 9, 22, color);

    tft.fillCircle(cx - 18, cy - 1, 2, color);
    tft.fillCircle(cx + 18, cy - 1, 2, color);
    tft.fillCircle(cx, cy + 18, 2, color);
}

static void settingsGlyph(TFT_eSPI& tft, int cx, int cy, uint16_t color)
{
    tft.drawCircle(cx, cy, 8, color);
    tft.fillCircle(cx, cy, 3, color);
    for (int i = 0; i < 8; i++)
    {
        float a = i * 0.785398f;
        int x1 = cx + (int)(cos(a) * 11);
        int y1 = cy + (int)(sin(a) * 11);
        int x2 = cx + (int)(cos(a) * 14);
        int y2 = cy + (int)(sin(a) * 14);
        tft.drawLine(x1, y1, x2, y2, color);
    }
}

static void checkGlyph(TFT_eSPI& tft, int cx, int cy, uint16_t color)
{
    for (int i = 0; i < 3; i++)
    {
        tft.drawLine(cx - 16, cy - 1 + i, cx - 5, cy + 12 + i, color);
        tft.drawLine(cx - 5, cy + 12 + i, cx + 18, cy - 15 + i, color);
    }
}

static void xGlyph(TFT_eSPI& tft, int cx, int cy, uint16_t color)
{
    for (int i = -1; i <= 1; i++)
    {
        tft.drawLine(cx - 16, cy - 16 + i, cx + 16, cy + 16 + i, color);
        tft.drawLine(cx + 16, cy - 16 + i, cx - 16, cy + 16 + i, color);
    }
}

static void handGlyph(TFT_eSPI& tft, int cx, int cy, uint16_t color)
{
    tft.fillRoundRect(cx - 10, cy - 10, 5, 25, 2, color);
    tft.fillRoundRect(cx - 3, cy - 15, 5, 30, 2, color);
    tft.fillRoundRect(cx + 4, cy - 13, 5, 27, 2, color);
    tft.drawLine(cx + 12, cy - 8, cx + 18, cy - 15, color);
    tft.drawLine(cx + 15, cy - 4, cx + 22, cy - 8, color);
    tft.fillRoundRect(cx - 11, cy + 14, 26, 8, 3, color);
}

void UI_Init(TFT_eSPI& tft)
{
#ifdef TFT_BL
    pinMode(TFT_BL, OUTPUT);
    digitalWrite(TFT_BL, LOW);
#endif
    tft.init();
    tft.setRotation(2);
    tft.setTextWrap(false);
    tft.setTextDatum(TL_DATUM);
    tft.fillScreen(C_BG);
#ifdef TFT_BL
    digitalWrite(TFT_BL, HIGH);
#endif
}

void UI_ShowBoot(TFT_eSPI& tft)
{
    fillGradient(tft, BG_HOME_1, BG_HOME_2);
    textCenter(tft, "SMARTATTEND", 55, C_BLUE, BG_HOME_1,3);

    int cx = W / 2;
    int cy = 151;
    int lsize = 60;

    int lx = (W - lsize) / 2;
    int ly = cy - 19;
    int pad = 5, gap = 2;
    int cell = (lsize - pad*2 - gap) / 2;

    // Outer box
    tft.fillRoundRect(lx, ly, lsize, lsize, 10, 0xEF7D); // #EEF3FA
    tft.drawRoundRect(lx, ly, lsize, lsize, 10, 0xDB18); // #D8E4F0

    // Top-left — dark blue
    tft.fillRoundRect(lx+pad,          ly+pad,          cell, cell, 2, 0x19CD);
    // Top-right — dark blue
    tft.fillRoundRect(lx+pad+cell+gap, ly+pad,          cell, cell, 2, 0x19CD);
    // Bottom-left — dark blue
    tft.fillRoundRect(lx+pad,          ly+pad+cell+gap, cell, cell, 2, 0x19CD);
    // Bottom-right — light blue
    tft.fillRoundRect(lx+pad+cell+gap, ly+pad+cell+gap, cell, cell, 2, 0x3C5B);

    textCenter(tft, "Attendance System", 208, C_MUTED, BG_HOME_2);
    textCenter(tft, "Starting...", 270, C_DARK, BG_HOME_2);
    animatedProgress(tft, 48, 248, 144, 4, C_BLUE, 0, 92, 12);
}

void UI_ShowHome(TFT_eSPI& tft, bool wifiConnected, AuthMode authMode,
                 const String& timeStr, const String& dateStr)
{
    fillGradient(tft, BG_HOME_1, BG_HOME_2);
    lastTimeStr[0] = '\0';
    lastDateStr[0] = '\0';
    lastWifi = !wifiConnected;

    UI_UpdateWifiDot(tft, wifiConnected);
    textCenter(tft, "SMARTATTEND", 33, C_BLUE, BG_HOME_1);
    divider(tft, 123, BG_HOME_1);

    switch(authMode)
{
    case AUTH_ANY:
        textCenter(tft, "PLACE CARD TO SCAN", 143, C_BLUE, BG_HOME_2);
        textCenter(tft, "OR FINGERPRINT", 164, C_BLUE, BG_HOME_2);
        break;

    case AUTH_RFID:
        textCenter(tft, "PLACE CARD TO SCAN", 143, C_BLUE, BG_HOME_2);
        textCenter(tft, "RFID ONLY", 164, C_BLUE, BG_HOME_2);
        break;

    case AUTH_FP:
        textCenter(tft, "PLACE FINGER", 143, C_BLUE, BG_HOME_2);
        textCenter(tft, "FINGERPRINT ONLY", 164, C_BLUE, BG_HOME_2);
        break;

    case AUTH_PIN:
        textCenter(tft, "ENTER ATTENDANCE PIN", 143, C_BLUE, BG_HOME_2);
        textCenter(tft, "PIN ONLY", 164, C_BLUE, BG_HOME_2);
        break;

    case AUTH_FP_RFID:
        textCenter(tft, "PLACE FINGER", 143, C_BLUE, BG_HOME_2);
        textCenter(tft, "THEN SCAN RFID CARD", 164, C_BLUE, BG_HOME_2);
        break;

    case AUTH_FP_PIN:
        textCenter(tft, "PLACE FINGER", 143, C_BLUE, BG_HOME_2);
        textCenter(tft, "THEN ENTER PIN", 164, C_BLUE, BG_HOME_2);
        break;

    case AUTH_PIN_RFID:
        textCenter(tft, "PLACE CARD TO SCAN", 143, C_BLUE, BG_HOME_2);
        textCenter(tft, "THEN ENTER PIN", 164, C_BLUE, BG_HOME_2);
        break;
}

    bool showPinButton =
(
    authMode == AUTH_ANY ||
    authMode == AUTH_PIN
);

if(showPinButton)
{
    uint16_t pinFill = mix565(tft, BG_HOME_2, C_AMBER, 36);
    uint16_t pinLine = mix565(tft, BG_HOME_2, C_AMBER, 95);

    tft.fillRoundRect(22, 198, 196, 66, 10, pinFill);
    tft.drawRoundRect(22, 198, 196, 66, 10, pinLine);

    textCenter(tft, "PIN", 216, C_AMBER, pinFill, 2);
    textCenter(tft, "ENTER ATTENDANCE PIN", 242, C_MUTED, pinFill);
}

    textCenter(tft, "SMARTATTEND v2.0", 300, C_DARK, BG_HOME_2);

    uint16_t gearFill = mix565(tft, BG_HOME_2, C_BLUE, 22);
    uint16_t gearLine = mix565(tft, BG_HOME_2, C_BLUE, 85);
    tft.fillRoundRect(198, 286, 32, 28, 6, gearFill);
    tft.drawRoundRect(198, 286, 32, 28, 6, gearLine);
    settingsGlyph(tft, 214, 300, C_BLUE);

    if (!timeStr.isEmpty() && !dateStr.isEmpty())
        UI_UpdateClock(tft, timeStr, dateStr);
}

void UI_UpdateClock(TFT_eSPI& tft, const String& timeStr, const String& dateStr)
{
    if (timeStr != lastTimeStr)
    {
        timeStr.toCharArray(lastTimeStr, sizeof(lastTimeStr));
        tft.fillRect(0, 47, W, 41, BG_HOME_1);
        textCenter(tft, timeStr, 60, C_TEXT, BG_HOME_1, 4);
    }

    if (dateStr != lastDateStr)
    {
        dateStr.toCharArray(lastDateStr, sizeof(lastDateStr));
        tft.fillRect(0, 91, W, 14, BG_HOME_1);
        textCenter(tft, dateStr, 105, C_MUTED, BG_HOME_1);
    }
}

void UI_UpdateWifiDot(TFT_eSPI& tft, bool connected)
{
    if (connected == lastWifi)
        return;

    lastWifi = connected;
    tft.fillRect(14, 11, 28, 20, BG_HOME_1);
    drawWifi(tft, 16, 14, connected);
    tft.fillRect(W - 55, 12, 46, 12, BG_HOME_1);
    // textLeft(tft, connected ? "ONLINE" : "OFF", W - 52, 15,
    //          connected ? C_GREEN : C_RED, BG_HOME_1);
}


void UI_ShowScanning(TFT_eSPI& tft, String cardId)
{
    fillGradient(tft, BG_HOME_1, BG_SCAN_2);
    textCenter(tft, "SCANNING RFID", 48, C_BLUE, BG_HOME_1);

    int cx = W / 2;
    int cy = 135;
    tft.drawCircle(cx, cy, 45, mix565(tft, BG_SCAN_2, C_BLUE, 45));
    tft.drawCircle(cx, cy, 57, mix565(tft, BG_SCAN_2, C_BLUE, 25));
    circleIcon(tft, cx, cy, 36, BG_SCAN_2, C_BLUE);
    rfidGlyph(tft, cx, cy, C_BLUE);

    textCenter(tft, "Hold card near reader", 207, C_MUTED, BG_SCAN_2);
    textCenter(tft, "Keep still...", 224, C_MUTED, BG_SCAN_2);
    textCenter(tft, "Card ID: " + cardId, 271, C_DARK, BG_SCAN_2);
    indeterminateProgress(tft, 48, 252, 144, 4, BG_SCAN_2, C_BLUE, 2, 14);
}

void UI_ShowSuccess(TFT_eSPI& tft, String name, String action,
                    String timeStr, String method)
{
    bool checkedOut = (action == "Checked Out");
    uint16_t accent = checkedOut ? C_AMBER : C_GREEN;
    uint16_t bg2 = checkedOut ? BG_OUT_2 : BG_OK_2;

    fillGradient(tft, BG_OK_1, bg2);

    int cx = W / 2;
    circleIcon(tft, cx, 82, 36, bg2, accent);
    if (checkedOut)
        handGlyph(tft, cx, 80, accent);
    else
        checkGlyph(tft, cx, 82, accent);

    String title = checkedOut ? "CHECKED OUT" : "WELCOME!";
    textCenter(tft, title, checkedOut ? 130 : 137, accent, bg2, 2);

    if (name.length() > 18)
        name = name.substring(0, 17) + ".";

    textCenter(tft, name, checkedOut ? 162 : 168, C_TEXT, bg2);
    textCenter(tft, timeStr, checkedOut ? 187 : 187, C_MUTED, bg2);
    if (method.length() > 13)
        method = method.substring(0, 12) + ".";
    badge(tft, 60, 210, 120, 19, method, bg2, accent);
    textCenter(tft, checkedOut ? "Check-out Successful" : "Check-in Successful",
               242, accent, bg2);
    progress(tft, 48, 265, 144, 4, 75, accent);
    textCenter(tft, "Closing in 3s...", 287, C_DARK, bg2);
}

void UI_ShowFail(TFT_eSPI& tft, String reason, String cardId)
{
    fillGradient(tft, BG_BAD_1, BG_BAD_2);

    int cx = W / 2;
    circleIcon(tft, cx, 82, 36, BG_BAD_1, C_RED);
    xGlyph(tft, cx, 82, C_RED);

    textCenter(tft, "DENIED!", 139, C_RED, BG_BAD_2, 2);
    textCenter(tft, reason, 170, C_MUTED, BG_BAD_2);

    tft.fillRoundRect(49, 213, 142, 20, 4, mix565(tft, BG_BAD_2, C_RED, 22));
    tft.drawRoundRect(49, 213, 142, 20, 4, mix565(tft, BG_BAD_2, C_RED, 55));
    textCenter(tft, "ID: " + cardId, 219, C_RED,
               mix565(tft, BG_BAD_2, C_RED, 22));

    textCenter(tft, "Contact administrator", 253, C_MUTED, BG_BAD_2);
    textCenter(tft, "to register your card", 269, C_MUTED, BG_BAD_2);
    textCenter(tft, "Closing in 3s...", 293, C_DARK, BG_BAD_2);
}

void UI_ShowWrongPin(TFT_eSPI& tft){
    fillGradient(tft, BG_HOME_1, BG_HOME_2);
    textCenter(tft, "Wrong PIN!", 150, C_RED, BG_HOME_2, 2);
    textCenter(tft, "Try again", 180, C_MUTED, BG_HOME_2);
}

void UI_ShowProcessing(TFT_eSPI& tft){
    fillGradient(tft, BG_HOME_1, BG_HOME_2);
    textCenter(tft, "Processing...", 150, C_BLUE, BG_HOME_2, 2);
}

void UI_ShowDeviceSync(TFT_eSPI& tft, int current, int total)
{
    fillGradient(tft, BG_HOME_1, BG_HOME_2);
    textCenter(tft, "SYNCING FROM APP", 112, C_BLUE, BG_HOME_2, 2);
    textCenter(tft, "Please keep device on", 145, C_TEXT, BG_HOME_2);
    textCenter(tft, "Do not restart or remove SD", 164, C_MUTED, BG_HOME_2);

    if (total > 0)
    {
        String progress = "Applying update " +
            String(current) + " / " + String(total);
        textCenter(tft, progress, 205, C_AMBER, BG_HOME_2);
    }
}

void UI_ShowFingerprint(TFT_eSPI& tft, String title,
                        String line1, String line2)
{
    fillGradient(tft, BG_HOME_1, BG_SCAN_2);
    textCenter(tft, title, 38, C_BLUE, BG_HOME_1, 2);
    divider(tft, 70, BG_HOME_1);

    int cx = W / 2;
    int cy = 134;
    tft.drawCircle(cx, cy, 54, mix565(tft, BG_SCAN_2, C_BLUE, 24));
    tft.drawCircle(cx, cy, 45, mix565(tft, BG_SCAN_2, C_BLUE, 45));
    circleIcon(tft, cx, cy, 36, BG_SCAN_2, C_BLUE);
    fingerprintGlyph(tft, cx, cy, C_BLUE);

    textCenter(tft, line1, 205, C_TEXT, BG_SCAN_2);
    if (!line2.isEmpty())
        textCenter(tft, line2, 224, C_MUTED, BG_SCAN_2);

    badge(tft, 72, 252, 96, 19, "FINGERPRINT", BG_SCAN_2, C_BLUE);
}

void UI_ShowFingerprintScanning(TFT_eSPI& tft, String line1, String line2)
{
    fillGradient(tft, BG_HOME_1, BG_SCAN_2);
    textCenter(tft, "SCANNING FINGER", 38, C_BLUE, BG_HOME_1, 2);
    divider(tft, 70, BG_HOME_1);

    int cx = W / 2;
    int cy = 134;
    tft.drawCircle(cx, cy, 57, mix565(tft, BG_SCAN_2, C_BLUE, 25));
    tft.drawCircle(cx, cy, 45, mix565(tft, BG_SCAN_2, C_BLUE, 45));
    circleIcon(tft, cx, cy, 36, BG_SCAN_2, C_BLUE);
    fingerprintGlyph(tft, cx, cy, C_BLUE);

    textCenter(tft, line1, 205, C_TEXT, BG_SCAN_2);
    textCenter(tft, line2, 224, C_MUTED, BG_SCAN_2);
    indeterminateProgress(tft, 48, 252, 144, 4, BG_SCAN_2, C_BLUE, 2, 14);
    textCenter(tft, "Matching enrolled finger", 276, C_DARK, BG_SCAN_2);
}

void UI_ShowAPMode(TFT_eSPI& tft)
{
    fillGradient(tft, BG_HOME_1, BG_HOME_2);

    // Title
    textCenter(tft, "SMARTATTEND", 22, C_BLUE, BG_HOME_1);
    textCenter(tft, "SETUP MODE", 38, C_AMBER, BG_HOME_1);

    tft.drawFastHLine(24, 60, W - 48, C_BORDER);

    // WiFi hotspot icon — concentric arcs
    int cx = W / 2;
    int cy = 128;

    // Outer glow rings
    tft.drawCircle(cx, cy, 52, mix565(tft, BG_HOME_2, C_AMBER, 18));
    tft.drawCircle(cx, cy, 44, mix565(tft, BG_HOME_2, C_AMBER, 30));

    // Icon background
    tft.fillCircle(cx, cy, 36, mix565(tft, BG_HOME_2, C_AMBER, 16));
    tft.drawCircle(cx, cy, 36, mix565(tft, BG_HOME_2, C_AMBER, 90));
    tft.drawCircle(cx, cy, 34, mix565(tft, BG_HOME_2, C_AMBER, 50));

    // WiFi arcs (3 arcs + dot)
    // Draw as partial horizontal lines to simulate arcs
    tft.fillCircle(cx, cy + 14, 4, C_AMBER);

    for (int r = 10; r <= 28; r += 9)
    {
        // Top arc approximation using drawCircle clipped to top half
        for (int angle = -50; angle <= 50; angle += 2)
        {
            float rad = angle * 0.01745f;
            int ax = cx + (int)(r * sin(rad));
            int ay = cy + 14 - (int)(r * cos(rad));
            tft.drawPixel(ax, ay, C_AMBER);
            tft.drawPixel(ax, ay + 1, mix565(tft, BG_HOME_2, C_AMBER, 160));
        }
    }

    // SSID label
    uint16_t ssidBg = mix565(tft, BG_HOME_2, C_AMBER, 22);
    uint16_t ssidBorder = mix565(tft, BG_HOME_2, C_AMBER, 70);
    tft.fillRoundRect(20, 178, W - 40, 28, 6, ssidBg);
    tft.drawRoundRect(20, 178, W - 40, 28, 6, ssidBorder);
    textCenter(tft, "SmartAttend-Setup", 186, C_AMBER, ssidBg);

    // Instructions
    textCenter(tft, "1. Connect PC to hotspot above", 222, C_TEXT, BG_HOME_2);
    textCenter(tft, "2. Open SmartAttend app", 238, C_TEXT, BG_HOME_2);
    textCenter(tft, "3. Click Scan & Connect", 254, C_TEXT, BG_HOME_2);

    tft.drawFastHLine(24, 276, W - 48, C_BORDER);

    // Status dot + text
    tft.fillCircle(26, 287, 4, C_AMBER);
    tft.drawCircle(26, 287, 4, mix565(tft, BG_HOME_2, C_AMBER, 180));
    textLeft(tft, "Waiting for configuration...", 34, 283, C_MUTED, BG_HOME_2);
}
