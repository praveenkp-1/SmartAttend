    /*
    * ╔══════════════════════════════════════╗
    * ║  SMARTATTEND — ADMIN & ENROLL UI     ║
    * ║  Touch-driven admin flow             ║
    * ╚══════════════════════════════════════╝
    */

    #include "ui.h"
    #include <time.h>

    // ── Reuse same color palette from ui.cpp ──────────────
    extern const uint16_t C_BG;
    extern const uint16_t C_CARD;
    extern const uint16_t C_BORDER;
    extern const uint16_t C_TEXT;
    extern const uint16_t C_MUTED;
    extern const uint16_t C_BLUE;
    extern const uint16_t C_GREEN;
    extern const uint16_t C_RED;
    extern const uint16_t C_AMBER;
    extern const uint16_t C_DARK;
    extern const uint16_t BG_HOME_1;
    extern const uint16_t BG_HOME_2;
    extern void buzzBtnPress();

    #define W 240
    #define H 320

    // Touch calibration from your working test code
    static uint16_t _calData[5] = {275, 3620, 264, 3532, 7};
    static uint32_t _lastTouch = 0;
    #define DEBOUNCE_MS 280

    // ── Internal helpers (forward declare from ui.cpp) ────
    uint16_t mix565(TFT_eSPI &tft, uint16_t a, uint16_t b, uint8_t amount);
    void fillGradient(TFT_eSPI &tft, uint16_t top, uint16_t bottom);
    void textCenter(TFT_eSPI &tft, const String &txt, int y,
                    uint16_t fg, uint16_t bg, uint8_t size = 1);
    void textLeft(TFT_eSPI &tft, const String &txt, int x, int y,
                uint16_t fg, uint16_t bg, uint8_t size = 1);

    // ── Draw a standard button ─────────────────────────────
    static void drawBtn(TFT_eSPI &tft, int x, int y, int w, int h,
                        const String &label, uint16_t accent, uint16_t bg,
                        bool filled = true)
    {
        uint16_t fill = filled
                            ? mix565(tft, bg, accent, 38)
                            : bg;
        uint16_t border = mix565(tft, bg, accent, 90);

        tft.fillRoundRect(x, y, w, h, 6, fill);
        tft.drawRoundRect(x, y, w, h, 6, border);

        tft.setTextColor(filled ? accent : C_MUTED, fill);
        tft.setTextSize(1);
        tft.setTextDatum(TL_DATUM);
        int tw = tft.textWidth(label);
        tft.setCursor(x + (w - tw) / 2, y + (h - 8) / 2);
        tft.print(label);
    }

    // ── Draw status dot (enrolled / not enrolled) ─────────
    static void enrollDot(TFT_eSPI &tft, int x, int y, bool done)
    {
        tft.fillCircle(x, y, 4, done ? C_GREEN : C_DARK);
        tft.drawCircle(x, y, 4, done ? C_GREEN : C_BORDER);
    }

    // ═══════════════════════════════════════════════════════
    // TOUCH INIT + READ
    // ═══════════════════════════════════════════════════════

    void UI_InitTouch(TFT_eSPI& tft)
    {
        // uint16_t calData[5] = { 259, 3558, 180, 3621, 2 }; rotaion0
        uint16_t calData[5] = { 242, 3529, 173, 3640, 4 };
    tft.setTouch(calData);

        
    }

    TouchResult UI_GetTouch(TFT_eSPI &tft)
    {
        TouchResult r = {false, 0, 0, 0, 0};
        uint32_t now = millis();

        if (now - _lastTouch < DEBOUNCE_MS)
            return r;

        uint16_t x, y;
        if (tft.getTouch(&x, &y))
        {

            buzzBtnPress();   // <-- Buzz on every touch
            _lastTouch = now;
            r.pressed = true;
            r.x = x;
            r.y = y;
            r.rawX = x;
            r.rawY = y;
            }
        return r;
        
        
    }

    bool UI_IsTapped(TouchResult &t, int x, int y, int w, int h)
    {
        if (!t.pressed)
            return false;
        return (t.x >= x && t.x <= x + w &&
                t.y >= y && t.y <= y + h);
    }

    // ═══════════════════════════════════════════════════════
    // PIN KEYPAD — shared by admin login + user PIN enroll
    // Blocks until PIN confirmed or cancelled
    // Returns true = confirmed, false = cancelled
    // ═══════════════════════════════════════════════════════

    static const char *KEYS[12] = {
        "1", "2", "3",
        "4", "5", "6",
        "7", "8", "9",
        "CLR", "0", "OK"};

    static const char KEY_VALUES[12] = {
        '1', '2', '3',
        '4', '5', '6',
        '7', '8', '9',
        '*', '0', '#'};

    #define KEY_X0 22
    #define KEY_Y0 96
    #define KEY_W 62
    #define KEY_H 39
    #define KEY_GAP 7

    static void drawPINKeypad(TFT_eSPI &tft, uint16_t bg)
    {
        for (int i = 0; i < 12; i++)
        {
            int col = i % 3;
            int row = i / 3;
            int x = KEY_X0 + col * (KEY_W + KEY_GAP);
            int y = KEY_Y0 + row * (KEY_H + KEY_GAP);

            uint16_t fill = C_CARD;
            uint16_t border = C_BORDER;
            uint16_t fg = C_TEXT;
            uint8_t size = 2;

            if (i == 9)
            {
                fg = C_MUTED;
                size = 1;
            }
            else if (i == 11)
            {
                fill = mix565(tft, bg, C_AMBER, 38);
                border = mix565(tft, bg, C_AMBER, 90);
                fg = C_AMBER;
                size = 1;
            }

            tft.fillRoundRect(x, y, KEY_W, KEY_H, 5, fill);
            tft.drawRoundRect(x, y, KEY_W, KEY_H, 5, border);
            tft.setTextColor(fg, fill);
            tft.setTextSize(size);
            tft.setTextDatum(TL_DATUM);
            int tw = tft.textWidth(KEYS[i]);
            tft.setCursor(x + (KEY_W - tw) / 2, y + (KEY_H - size * 8) / 2);
            tft.print(KEYS[i]);
        }
    }

    static void drawPINDots(TFT_eSPI &tft, int len, uint16_t bg)
    {
        tft.fillRoundRect(23, 47, 194, 32, 6, C_BG);

        int startX = (W - 61) / 2 + 5;

        for (int i = 0; i < 4; i++)
        {
            int x = startX + i * 17;
            int y = 63;
            if (i < len)
                tft.fillCircle(x, y, 5, C_AMBER);
            else
            {
                tft.fillCircle(x, y, 5, C_BG);
                tft.drawCircle(x, y, 5, C_DARK);
            }
        }
    }

    static const char *ATT_KEYS[12] = {
        "1", "2", "3",
        "4", "5", "6",
        "7", "8", "9",
        "DEL", "0", "BACK"};

    static const char ATT_KEY_VALUES[12] = {
        '1', '2', '3',
        '4', '5', '6',
        '7', '8', '9',
        '*', '0', 'B'};

    static const int ATT_KEY_X0 = 13;
    static const int ATT_KEY_Y0 = 108;
    static const int ATT_KEY_W = 64;
    static const int ATT_KEY_H = 44;
    static const int ATT_KEY_GAP = 10;

    static void drawAttendancePINKeypad(TFT_eSPI &tft, uint16_t bg)
    {
        for (int i = 0; i < 12; i++)
        {
            int col = i % 3;
            int row = i / 3;
            int x = ATT_KEY_X0 + col * (ATT_KEY_W + ATT_KEY_GAP);
            int y = ATT_KEY_Y0 + row * (ATT_KEY_H + ATT_KEY_GAP);

            uint16_t fill = C_CARD;
            uint16_t border = C_BORDER;
            uint16_t fg = C_TEXT;
            uint8_t size = 2;

            if (i == 9)
            {
                fg = C_MUTED;
                size = 1;
            }
            else if (i == 11)
            {
                fill = mix565(tft, bg, C_RED, 30);
                border = mix565(tft, bg, C_RED, 85);
                fg = C_RED;
                size = 1;
            }

            tft.fillRoundRect(x, y, ATT_KEY_W, ATT_KEY_H, 18, fill);
            tft.drawRoundRect(x, y, ATT_KEY_W, ATT_KEY_H, 18, border);
            tft.setTextColor(fg, fill);
            tft.setTextSize(size);
            tft.setTextDatum(TL_DATUM);
            int tw = tft.textWidth(ATT_KEYS[i]);
            tft.setCursor(x + (ATT_KEY_W - tw) / 2, y + (ATT_KEY_H - size * 8) / 2);
            tft.print(ATT_KEYS[i]);
        }
    }

    static void drawAttendancePINDots(TFT_eSPI &tft, int len, uint16_t bg)
    {
        const int boxX = 70;
        const int boxY = 56;
        const int boxW = 100;
        const int boxH = 30;
        const uint16_t boxBg = mix565(tft, bg, C_BG, 105);

        tft.fillRoundRect(boxX, boxY, boxW, boxH, 14, boxBg);
        tft.drawRoundRect(boxX, boxY, boxW, boxH, 14, C_BORDER);

        int startX = boxX + 21;

        for (int i = 0; i < 4; i++)
        {
            int x = startX + i * 19;
            int y = boxY + 15;
            if (i < len)
                tft.fillCircle(x, y, 5, C_AMBER);
            else
            {
                tft.fillCircle(x, y, 5, boxBg);
                tft.drawCircle(x, y, 5, C_DARK);
            }
        }
    }

    bool UI_ShowAdminPIN(TFT_eSPI &tft, String title, String &pinOut)
    {
        fillGradient(tft, BG_HOME_1, BG_HOME_2);

        textCenter(tft, "ENTER PIN", 22, C_AMBER, BG_HOME_1);
        textCenter(tft, title, 36, C_MUTED, BG_HOME_1);
        tft.fillRoundRect(22, 46, 196, 34, 6, C_BG);
        tft.drawRoundRect(22, 46, 196, 34, 6, C_BORDER);
        drawPINDots(tft, 0, C_BG);
        drawPINKeypad(tft, BG_HOME_2);

        drawBtn(tft, 22, 284, 62, 24, "BACK", C_RED, BG_HOME_2);

        String pin = "";

        while (true)
        {
            TouchResult t = UI_GetTouch(tft);
            if (!t.pressed)
                continue;

            if (UI_IsTapped(t, 22, 284, 62, 24))
            {
                pinOut = "";
                return false;
            }

            // Check key taps
            for (int i = 0; i < 12; i++)
            {
                int col = i % 3;
                int row = i / 3;
                int kx = KEY_X0 + col * (KEY_W + KEY_GAP);
                int ky = KEY_Y0 + row * (KEY_H + KEY_GAP);

                if (!UI_IsTapped(t, kx, ky, KEY_W, KEY_H))
                    continue;

                char key = KEY_VALUES[i];

                if (key == '*')
                {
                    pin = "";
                }
                else if (key == '#')
                {
                    pinOut = pin;
                    return true;
                }
                else
                {
                    // Digit
                    if (pin.length() < 4)
                        pin += key;
                }

                drawPINDots(tft, pin.length(), C_BG);
                break;
            }
        }
    }

    bool UI_ShowAttendancePIN(TFT_eSPI &tft, String &pinOut)
    {
        fillGradient(tft, BG_HOME_1, BG_HOME_2);

        textCenter(tft, "EMPLOYEE PIN", 22, C_AMBER, BG_HOME_1);
        textCenter(tft, "Enter your 4 digit PIN", 39, C_MUTED, BG_HOME_1);
        drawAttendancePINDots(tft, 0, C_BG);
        drawAttendancePINKeypad(tft, BG_HOME_2);

        String pin = "";

        while (true)
        {
            TouchResult t = UI_GetTouch(tft);
            if (!t.pressed)
                continue;

            for (int i = 0; i < 12; i++)
            {
                int col = i % 3;
                int row = i / 3;
                int kx = ATT_KEY_X0 + col * (ATT_KEY_W + ATT_KEY_GAP);
                int ky = ATT_KEY_Y0 + row * (ATT_KEY_H + ATT_KEY_GAP);

                if (!UI_IsTapped(t, kx, ky, ATT_KEY_W, ATT_KEY_H))
                    continue;

                char key = ATT_KEY_VALUES[i];

                if (key == '*')
                {
                    if (pin.length() > 0)
                        pin.remove(pin.length() - 1);
                }
                else if (key == 'B')
                {
                    pinOut = "";
                    return false;
                }
                else if (pin.length() < 4)
                {
                    pin += key;
                    if (pin.length() == 4)
                    {
                        drawAttendancePINDots(tft, pin.length(), C_BG);
                        pinOut = pin;
                        return true;
                    }
                }

                drawAttendancePINDots(tft, pin.length(), C_BG);
                break;
            }
        }
    }

    // ═══════════════════════════════════════════════════════
    // ADMIN MENU
    // Returns: 0=Enroll New User, 1=Employee List,
    // 2=Change Master PIN, 3=Back
    // ═══════════════════════════════════════════════════════

    int UI_ShowAdminMenu(TFT_eSPI &tft)
    {
        fillGradient(tft, BG_HOME_1, BG_HOME_2);

        textCenter(tft, "ADMIN MENU", 22, C_BLUE, BG_HOME_1);
        tft.drawFastHLine(24, 52, W - 48, C_BORDER);

        // Buttons
        drawBtn(tft, 20, 68, 200, 38, "ENROLL NEW USER", C_GREEN, BG_HOME_2);
        drawBtn(tft, 20, 118, 200, 38, "EMPLOYEE LIST", C_BLUE, BG_HOME_2);
        drawBtn(tft, 20, 168, 200, 38, "CHANGE MASTER PIN", C_AMBER, BG_HOME_2);
        drawBtn(tft, 20, 218, 200, 38, "BACK TO HOME", C_RED, BG_HOME_2);

        // Info text
        textCenter(tft, "Authorized access only", 262, C_DARK, BG_HOME_2);
        textCenter(tft, "All actions are logged", 278, C_DARK, BG_HOME_2);

        while (true)
        {
            TouchResult t = UI_GetTouch(tft);
            if (!t.pressed)
                continue;

            if (UI_IsTapped(t, 20, 68, 200, 38))
                return 0; // Enroll
            if (UI_IsTapped(t, 20, 118, 200, 38))
                return 1; // Employee List
            if (UI_IsTapped(t, 20, 168, 200, 38))
                return 2; // Change PIN
            if (UI_IsTapped(t, 20, 218, 200, 38))
                return 3; // Back
        }
    }

    // ═══════════════════════════════════════════════════════
    // PENDING EMPLOYEE LIST
    // Shows up to 4 employees at a time with scroll
    // Returns selected index (0-based), -1 = back
    // ═══════════════════════════════════════════════════════

    bool UI_ShowEmployeeSearch(TFT_eSPI &tft, String &queryOut)
    {
        fillGradient(tft, BG_HOME_1, BG_HOME_2);

        textCenter(tft, "FIND EMPLOYEE", 18, C_BLUE, BG_HOME_1);
        textCenter(tft, "Enter ID digits", 36, C_MUTED, BG_HOME_1);

        auto drawQuery = [&](const String &query)
        {
            tft.fillRoundRect(22, 52, 196, 34, 6, C_BG);
            tft.drawRoundRect(22, 52, 196, 34, 6, C_BORDER);
            String shown = query.isEmpty() ? "EMP-" : "EMP-" + query;
            textCenter(tft, shown, 63, C_TEXT, C_BG);
        };

        String query = "";
        drawQuery(query);
        drawPINKeypad(tft, BG_HOME_2);
        drawBtn(tft, 22, 284, 82, 24, "BACK", C_RED, BG_HOME_2);
        drawBtn(tft, 136, 284, 82, 24, "ALL", C_BLUE, BG_HOME_2);

        while (true)
        {
            TouchResult t = UI_GetTouch(tft);
            if (!t.pressed)
                continue;

            if (UI_IsTapped(t, 22, 284, 82, 24))
            {
                queryOut = "";
                return false;
            }

            if (UI_IsTapped(t, 136, 284, 82, 24))
            {
                queryOut = "";
                return true;
            }

            for (int i = 0; i < 12; i++)
            {
                int col = i % 3;
                int row = i / 3;
                int kx = KEY_X0 + col * (KEY_W + KEY_GAP);
                int ky = KEY_Y0 + row * (KEY_H + KEY_GAP);

                if (!UI_IsTapped(t, kx, ky, KEY_W, KEY_H))
                    continue;

                char key = KEY_VALUES[i];
                if (key == '*')
                {
                    query = "";
                }
                else if (key == '#')
                {
                    queryOut = query;
                    return true;
                }
                else if (query.length() < 8)
                {
                    query += key;
                }

                drawQuery(query);
                break;
            }
        }
    }

    int UI_ShowEnrollList(TFT_eSPI &tft,
                        PendingEmployee *list, int count,
                        int &scrollOffset,
                        bool pendingOnly)
    {
        auto drawList = [&]()
        {
            fillGradient(tft, BG_HOME_1, BG_HOME_2);
            textCenter(tft,
                       pendingOnly ? "SELECT EMPLOYEE" : "EMPLOYEE LIST",
                       14, C_BLUE, BG_HOME_1);

            String countStr = String(count) +
                (pendingOnly ? " pending" : " employees");
            textCenter(tft, countStr, 30, C_MUTED, BG_HOME_1);
            tft.drawFastHLine(24, 48, W - 48, C_BORDER);

            if (count == 0)
            {
                textCenter(tft,
                           pendingOnly ? "No pending employees" : "No employees",
                           160, C_MUTED, BG_HOME_2);
                textCenter(tft,
                           pendingOnly ? "All enrolled!" : "Sync app first",
                           178,
                           pendingOnly ? C_GREEN : C_AMBER,
                           BG_HOME_2);
            }
            else
            {
                // Draw up to 4 items
                for (int i = 0; i < 4; i++)
                {
                    int idx = scrollOffset + i;
                    if (idx >= count)
                        break;

                    int y = 58 + i * 54;

                    // Row background
                    uint16_t rowBg = mix565(tft, BG_HOME_2, C_BLUE, 10);
                    tft.fillRoundRect(14, y, W - 28, 48, 6, rowBg);
                    tft.drawRoundRect(14, y, W - 28, 48, 6, C_BORDER);

                    // Name
                    String name = list[idx].name;
                    if (name.length() > 16)
                        name = name.substring(0, 15) + ".";

                    tft.setTextColor(C_TEXT, rowBg);
                    tft.setTextSize(1);
                    tft.setCursor(24, y + 8);
                    tft.print(name);

                    // User ID
                    tft.setTextColor(C_MUTED, rowBg);
                    tft.setCursor(24, y + 22);
                    tft.print(list[idx].userId);

                    // Enrollment dots
                    enrollDot(tft, W - 58, y + 14, list[idx].rfidDone);
                    enrollDot(tft, W - 42, y + 14, list[idx].pinDone);
                    enrollDot(tft, W - 26, y + 14, list[idx].fpDone);

                    // Labels under dots
                    tft.setTextColor(C_DARK, rowBg);
                    tft.setCursor(W - 62, y + 26);
                    tft.print("R P F");
                }
            }

            // Bottom controls
            if (count <= 4)
            {
                drawBtn(tft, 14, H - 42, W - 28,
                        28, "BACK", C_RED, BG_HOME_2);
            }
            else
            {
                if (scrollOffset > 0)
                    drawBtn(tft, 14, H - 42, 62, 28,
                            "<", C_BLUE, BG_HOME_2);

                drawBtn(tft, 84, H - 42, 72, 28,
                        "BACK", C_RED, BG_HOME_2);

                if (scrollOffset + 4 < count)
                    drawBtn(tft, 164, H - 42, 62, 28,
                            ">", C_BLUE, BG_HOME_2);
            }
        };

        drawList();

        while (true)
        {
            TouchResult t = UI_GetTouch(tft);
            if (!t.pressed)
                continue;

            // Back button
            if ((count <= 4 &&
                 UI_IsTapped(t, 14, H - 42, W - 28, 28)) ||
                (count > 4 &&
                 UI_IsTapped(t, 84, H - 42, 72, 28)))
                return -1;

            // Scroll prev
            if (count > 4 &&
                scrollOffset > 0 &&
                UI_IsTapped(t, 14, H - 42, 62, 28))
            {
                scrollOffset = max(0, scrollOffset - 4);
                drawList();
                continue;
            }

            // Scroll next
            if (count > 4 &&
                scrollOffset + 4 < count &&
                UI_IsTapped(t, 164, H - 42, 62, 28))
            {
                scrollOffset += 4;
                drawList();
                continue;
            }

            // Row taps
            for (int i = 0; i < 4; i++)
            {
                int idx = scrollOffset + i;
                if (idx >= count)
                    break;
                int y = 58 + i * 54;
                if (UI_IsTapped(t, 14, y, W - 28, 48))
                    return idx;
            }
        }
    }

    // ═══════════════════════════════════════════════════════
    // ENROLL METHOD MENU
    // Returns: 0=RFID, 1=PIN, 2=FP, 3=Done/Back
    // ═══════════════════════════════════════════════════════

    int UI_ShowEnrollMenu(TFT_eSPI &tft,
                        String name, String userId,
                        bool rfidDone, bool pinDone, bool fpDone)
    {
        fillGradient(tft, BG_HOME_1, BG_HOME_2);

        textCenter(tft, "ENROLL USER", 14, C_BLUE, BG_HOME_1);

        // Name card
        uint16_t cardBg = mix565(tft, BG_HOME_2, C_BLUE, 14);
        tft.fillRoundRect(14, 32, W - 28, 42, 6, cardBg);
        tft.drawRoundRect(14, 32, W - 28, 42, 6, C_BORDER);

        if (name.length() > 16)
            name = name.substring(0, 15) + ".";
        textCenter(tft, name, 38, C_TEXT, cardBg);
        textCenter(tft, userId, 52, C_MUTED, cardBg);

        tft.drawFastHLine(24, 82, W - 48, C_BORDER);

        // RFID button
        uint16_t rfidAccent = rfidDone ? C_GREEN : C_BLUE;
        drawBtn(tft, 20, 92, 200, 40,
                rfidDone ? "RFID  ✓  Done" : "RFID  Scan Card",
                rfidAccent, BG_HOME_2);

        // PIN button
        uint16_t pinAccent = pinDone ? C_GREEN : C_AMBER;
        drawBtn(tft, 20, 144, 200, 40,
                pinDone ? "PIN   ✓  Done" : "PIN   Set PIN",
                pinAccent, BG_HOME_2);

        // Fingerprint button
        uint16_t fpColor = fpDone ? C_GREEN : C_BLUE;
        drawBtn(tft, 20, 196, 200, 40,
                fpDone ? "FP    Done" : "FP    Scan Finger",
                fpColor, BG_HOME_2);

        // Done / Back
        bool anyDone = rfidDone || pinDone || fpDone;
        drawBtn(tft, 20, 252, 200, 36,
                anyDone ? "DONE" : "BACK",
                anyDone ? C_GREEN : C_RED, BG_HOME_2);

        while (true)
        {
            TouchResult t = UI_GetTouch(tft);
            if (!t.pressed)
                continue;

            if (UI_IsTapped(t, 20, 92, 200, 40))
                return 0; // RFID
            if (UI_IsTapped(t, 20, 144, 200, 40))
                return 1; // PIN
            if (UI_IsTapped(t, 20, 196, 200, 40))
                return 2; // Fingerprint
            if (UI_IsTapped(t, 20, 252, 200, 36))
                return 3; // Done/Back
        }
    }

    // ═══════════════════════════════════════════════════════
    // ENROLL RFID — shows "scan card" and waits
    // (actual card reading happens in main.cpp)
    // ═══════════════════════════════════════════════════════

    void UI_ShowEnrollRFID(TFT_eSPI &tft, String name)
    {
        fillGradient(tft, BG_HOME_1, BG_HOME_2);

        textCenter(tft, "ENROLL RFID", 18, C_BLUE, BG_HOME_1);
        if (name.length() > 16)
            name = name.substring(0, 15) + ".";
        textCenter(tft, name, 38, C_MUTED, BG_HOME_1);
        tft.drawFastHLine(24, 58, W - 48, C_BORDER);

        // RFID icon
        int cx = W / 2, cy = 130;
        tft.drawCircle(cx, cy, 46, mix565(tft, BG_HOME_2, C_BLUE, 45));
        tft.drawCircle(cx, cy, 58, mix565(tft, BG_HOME_2, C_BLUE, 22));
        tft.fillCircle(cx, cy, 34, mix565(tft, BG_HOME_2, C_BLUE, 18));
        tft.drawCircle(cx, cy, 34, mix565(tft, BG_HOME_2, C_BLUE, 90));

        // RFID glyph
        tft.drawRoundRect(cx - 18, cy - 12, 36, 24, 3, C_BLUE);
        tft.drawFastHLine(cx - 11, cy - 5, 22, C_BLUE);
        tft.drawFastHLine(cx - 11, cy + 1, 15, C_BLUE);
        tft.fillCircle(cx + 11, cy + 5, 2, C_BLUE);

        textCenter(tft, "Scan the new RFID card", 192, C_TEXT, BG_HOME_2);
        textCenter(tft, "Hold card near reader", 210, C_MUTED, BG_HOME_2);

        // Animated progress bar (partial — actual wait in main.cpp)
        tft.fillRoundRect(48, 234, 144, 4, 2, C_BORDER);
        for (int i = 0; i <= 90; i += 6)
        {
            tft.fillRoundRect(48, 234, i + 54, 4, 2, C_BLUE);
            delay(40);
        }

        textCenter(tft, "Waiting for card...", 256, C_MUTED, BG_HOME_2);

        // Cancel button
        drawBtn(tft, 70, 282, 100, 26, "CANCEL", C_RED, BG_HOME_2);
    }

    // ═══════════════════════════════════════════════════════
    // ENROLL PIN — user sets their own PIN
    // Returns true = PIN set, false = cancelled
    // ═══════════════════════════════════════════════════════

    bool UI_ShowEnrollPINEntry(TFT_eSPI &tft, String name,
                            String &pinOut)
    {
        fillGradient(tft, BG_HOME_1, BG_HOME_2);

        textCenter(tft, "SET USER PIN", 18, C_BLUE, BG_HOME_1);
        if (name.length() > 16)
            name = name.substring(0, 15) + ".";
        textCenter(tft, name, 36, C_MUTED, BG_HOME_1);
        textCenter(tft, "Enter 4-6 digit PIN", 52, C_TEXT, BG_HOME_1);
        tft.drawFastHLine(24, 72, W - 48, C_BORDER);

        // Step 1 — enter PIN
        textCenter(tft, "Step 1: Enter PIN", 82, C_MUTED, BG_HOME_1);
        drawPINDots(tft, 0, BG_HOME_1);
        drawPINKeypad(tft, BG_HOME_2);
        drawBtn(tft, 24, H - 38, 80, 26, "BACK", C_RED, BG_HOME_2);

        String pin1 = "";

        // First entry
        while (true)
        {
            TouchResult t = UI_GetTouch(tft);
            if (!t.pressed)
                continue;

            if (UI_IsTapped(t, 24, H - 38, 80, 26))
            {
                pinOut = "";
                return false;
            }

            for (int i = 0; i < 12; i++)
            {
                int col = i % 3;
                int row = i / 3;
                int kx = KEY_X0 + col * (KEY_W + KEY_GAP);
                int ky = KEY_Y0 + row * (KEY_H + KEY_GAP);

                if (!UI_IsTapped(t, kx, ky, KEY_W, KEY_H))
                    continue;

                char key = KEY_VALUES[i];
                if (key == '*' && pin1.length() > 0)
                    pin1.remove(pin1.length() - 1);
                else if (key == '#' && pin1.length() >= 4)
                    goto confirmStep;
                else if (key != '#' && key != '*' && pin1.length() < 6)
                    pin1 += key;

                drawPINDots(tft, pin1.length(), BG_HOME_1);
                break;
            }
        }

    confirmStep:
        // Step 2 - confirm PIN
        fillGradient(tft, BG_HOME_1, BG_HOME_2);
        textCenter(tft, "SET USER PIN", 18, C_BLUE, BG_HOME_1);
        textCenter(tft, name, 36, C_MUTED, BG_HOME_1);
        textCenter(tft, "Confirm your PIN", 52, C_TEXT, BG_HOME_1);
        tft.drawFastHLine(24, 72, W - 48, C_BORDER);
        textCenter(tft, "Step 2: Confirm PIN", 82, C_MUTED, BG_HOME_1);
        drawPINDots(tft, 0, BG_HOME_1);
        drawPINKeypad(tft, BG_HOME_2);
        drawBtn(tft, 24, H - 38, 80, 26, "BACK", C_RED, BG_HOME_2);

        String pin2 = "";

        while (true)
        {
            TouchResult t = UI_GetTouch(tft);
            if (!t.pressed)
                continue;

            if (UI_IsTapped(t, 24, H - 38, 80, 26))
            {
                pinOut = "";
                return false;
            }

            for (int i = 0; i < 12; i++)
            {
                int col = i % 3;
                int row = i / 3;
                int kx = KEY_X0 + col * (KEY_W + KEY_GAP);
                int ky = KEY_Y0 + row * (KEY_H + KEY_GAP);

                if (!UI_IsTapped(t, kx, ky, KEY_W, KEY_H))
                    continue;

                char key = KEY_VALUES[i];
                if (key == '*' && pin2.length() > 0)
                    pin2.remove(pin2.length() - 1);
                else if (key == '#' && pin2.length() >= 4)
                {
                    if (pin1 == pin2)
                    {
                        pinOut = pin1;
                        return true;
                    }
                    else
                    {
                        tft.fillRect(50, 138, 140, 24, BG_HOME_2);
                        textCenter(tft, "PINs don't match!", 148,
                                C_RED, BG_HOME_2);
                        delay(1000);
                        pin2 = "";
                        drawPINDots(tft, 0, BG_HOME_1);
                    }
                }
                else if (key != '#' && key != '*' && pin2.length() < 6)
                    pin2 += key;

                drawPINDots(tft, pin2.length(), BG_HOME_1);
                break;
            }
        }
    }

    // ═══════════════════════════════════════════════════════
    // ENROLLMENT SUCCESS / FAIL
    // ═══════════════════════════════════════════════════════

    void UI_ShowEnrollSuccess(TFT_eSPI &tft, String name,
                            String method)
    {
        fillGradient(tft, 0x00A1, 0x08E2); // green tint

        int cx = W / 2, cy = 100;
        tft.fillCircle(cx, cy, 38, mix565(tft, 0x08E2, C_GREEN, 18));
        tft.drawCircle(cx, cy, 38, C_GREEN);
        tft.drawCircle(cx, cy, 36, mix565(tft, 0x08E2, C_GREEN, 60));

        // Check
        for (int i = 0; i < 3; i++)
        {
            tft.drawLine(cx - 16, cy - 1 + i, cx - 5, cy + 12 + i, C_GREEN);
            tft.drawLine(cx - 5, cy + 12 + i, cx + 18, cy - 15 + i, C_GREEN);
        }

        textCenter(tft, "ENROLLED!", 154, C_GREEN, 0x08E2, 2);
        tft.drawFastHLine(40, 178, W - 80, C_BORDER);

        if (name.length() > 16)
            name = name.substring(0, 15) + ".";
        textCenter(tft, name, 188, C_TEXT, 0x08E2);
        textCenter(tft, method + " registered", 206, C_MUTED, 0x08E2);
        textCenter(tft, "Data saved to device", 228, C_MUTED, 0x08E2);
        textCenter(tft, "& sent to application", 244, C_MUTED, 0x08E2);
        textCenter(tft, "Returning in 3s...", 280, C_DARK, 0x08E2);
    }

    void UI_ShowEnrollFail(TFT_eSPI &tft, String reason)
    {
        fillGradient(tft, 0x1800, 0x2001); // red tint

        int cx = W / 2, cy = 100;
        tft.fillCircle(cx, cy, 38, mix565(tft, 0x2001, C_RED, 18));
        tft.drawCircle(cx, cy, 38, C_RED);

        for (int i = -1; i <= 1; i++)
        {
            tft.drawLine(cx - 16, cy - 16 + i, cx + 16, cy + 16 + i, C_RED);
            tft.drawLine(cx + 16, cy - 16 + i, cx - 16, cy + 16 + i, C_RED);
        }

        textCenter(tft, "FAILED!", 154, C_RED, 0x2001, 2);
        textCenter(tft, reason, 184, C_MUTED, 0x2001);
        textCenter(tft, "Please try again", 206, C_TEXT, 0x2001);
        textCenter(tft, "Returning in 3s...", 280, C_DARK, 0x2001);
    }

    // ═══════════════════════════════════════════════════════
    // CHANGE MASTER PIN
    // Returns true = changed, false = cancelled
    // ═══════════════════════════════════════════════════════

    bool UI_ShowFactoryResetConfirm(TFT_eSPI &tft, int unsyncedCount)
{
    fillGradient(tft, 0x1800, 0x2001);

    int cx = W / 2;
    int cy = 82;

    tft.fillCircle(cx, cy, 34, mix565(tft, 0x2001, C_RED, 18));
    tft.drawCircle(cx, cy, 34, C_RED);
    tft.drawFastVLine(cx, cy - 18, 24, C_RED);
    tft.fillCircle(cx, cy + 16, 2, C_RED);
    textCenter(tft, "FACTORY RESET?", 150, C_RED, 0x2001, 2);
    textCenter(tft,"if you proceed with reset", 215, C_TEXT, 0x2001);
    textCenter(tft, "all data will be lost", 230, C_TEXT, 0x2001);
    

    // // ❌ ONLY HARD BLOCK: SD NOT READY
    // if (unsyncedCount < 0)
    // {
    //     textCenter(tft, "RESET BLOCKED", 130, C_RED, 0x2001, 2);
    //     textCenter(tft, "SD card not ready", 170, C_TEXT, 0x2001);
    //     textCenter(tft, "Cannot perform reset", 202, C_MUTED, 0x2001);

    //     drawBtn(tft, 62, 260, 116, 34, "OK", C_BLUE, 0x2001);

    //     while (true)
    //     {
    //         TouchResult t = UI_GetTouch(tft);
    //         if (UI_IsTapped(t, 62, 260, 116, 34))
    //             return false;
    //     }
    // }

    // ⚠️ INFO ONLY (NOT BLOCKING)
    if (unsyncedCount > 0)
    {
        textCenter(tft, String(unsyncedCount) + " unsynced records", 180, C_RED, 0x2001);
        textCenter(tft,"Will be lost if not synced", 195, C_RED, 0x2001);  
    }
    

    drawBtn(tft, 22, 260, 92, 34, "CANCEL", C_BLUE, 0x2001);
    drawBtn(tft, 126, 260, 92, 34, "RESET", C_RED, 0x2001);

    while (true)
    {
        TouchResult t = UI_GetTouch(tft);

        if (UI_IsTapped(t, 22, 260, 92, 34))
            return false;

        if (UI_IsTapped(t, 126, 260, 92, 34))
            return true;
    }
}

    bool UI_ShowChangeMasterPIN(TFT_eSPI &tft,
                                String currentPin, String &newPinOut)
    {
        // Step 1 — verify current PIN
        String entered = "";
        bool verified = UI_ShowAdminPIN(tft, "Verify Current PIN", entered);

        if (!verified || entered != currentPin)
        {
            if (verified)
            {
                // Wrong PIN feedback
                fillGradient(tft, BG_HOME_1, BG_HOME_2);
                textCenter(tft, "Wrong PIN!", 150, C_RED, BG_HOME_2, 2);
                textCenter(tft, "Try again", 180, C_MUTED, BG_HOME_2);
                delay(1500);
            }
            newPinOut = "";
            return false;
        }

        // Step 2 — enter new PIN
        String newPin = "";
        bool ok = UI_ShowEnrollPINEntry(tft, "New Master PIN", newPin);

        if (ok && !newPin.isEmpty())
        {
            newPinOut = newPin;
            return true;
        }

        newPinOut = "";
        return false;
    }

    
