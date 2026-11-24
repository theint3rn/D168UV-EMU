# D168UV stock V1.07 menus (emulator crawl)

Crawled 2026-11-22 with `crawl_all.py --depth 5` (one fresh emulator per main-menu item; `crawl.py` presses Menu on
every row, reads option lists, Exits back; text from the firmware's glyph renderers). Full tree with a screenshot of
every screen: `out/crawl/tree.md` (487 screens, 119 option lists), per item `out/crawl/item_NN/` (tree_N.json,
crawl.log, shots/). Comparison with the stock CPS and the cfw UI: `COMPARE_MENUS_CPS.md`.

Reading the lists: an option list opens on its current value, so the **first value listed is the value in the emulator's
codeplug** (factory format + K6RRR-based NAND image); the rest follow in the radio's (wrapping) order. "popup" = message
that closes by itself. Text-entry screens show their maximum length.

Main menu (13 items): Talk Group, SMS, Call Log, Zone, Scan, Roaming, Settings, Record, GPS, Satellite, Digi Moni, BT, APRS.
Not in any menu: FM broadcast radio (PF key function "FM Radio", Settings > Other Func > FM Radio enables it),
man-down (codeplug only: settings +0x24 / +0x4F; emulator test in README "Man down").


## Notes from hand tests (not in the automatic crawl)
* **Man down** (enabled with `nand Poke 0x2500024 1`, radio laid flat with `i2c Fall`): "Alarming" popup over the
  main screen (red "!"), no menu; standing the radio up (`i2c Upright`) closes it. Alarm type 0 (codeplug default) =
  local only; type 1 also keys TX for about 1 s. Screens: `out/mandown1/m.png` (local), `out/mandown3/m.png` (TX type).
* **BT** is Off in the codeplug, so the crawl shows "BT Not Open!" for Pairing. With BT On (first crawl, `out/crawl_v1/`):
  Pairing > Seek BT ("Seeking BT..") / Available BT (the model's devices `BT-Headset`, `BT-PTT`) / Paired List
  ("Device List Empty!") / Disconnect. The module dialogue at boot: AT+VER?, AGHF?, PIN?, DNAME?, CONN?, SPKEN=0 (bt.log).
* **Satellite > Location**: the first row of the Satellite list is the (empty) satellite list, so Menu shows
  "No GPS Position"; Down then Menu opens Location: GPS Beacon, FIX 1-8, each with Edit Name / View&Select
  (FIX 1 = 34.2121N 108.8333E OM44JF).
* **GPS**: GPS > On/Off = GPS On, then GPS > GPS Info shows the model's fix (32.715700N 117.161100W DM12KR,
  2026-01-01) and the clock follows GPS time (+ time zone, UTC+08:00 default).
* **FM radio**: Settings > Radio Set > Key Func > PF1 Short = FM Radio, PF1 -> VFO A shows "108.00 FM CH VFO".
* **Battery**: `analog Battery 5.8` -> "Please Charge The Battery!" popup and a red battery icon; 7.0 V -> normal.
* **PF key functions** (complete list, Up from VFO/MR): OFF, Volt, Tx Power, TalkAround, Reverse, Encryption, Call, VOX,
  VFO/MR, Sub Ptt, Scan, FM Radio, Alarm, Rec Switch, Record, SMS, Dial, GPS Info, Monitor, Main CH, HotKey 1-6, Work Alone,
  Scan skip, Digi Moni, Sub Ch off, Prior Zone, VFO Scan, Mic sound, LastCall, ChType, Ranging, Roaming, CH Ranging,
  Max VOL, Slot Set, Aprs Type, Zone Select, Roam Set, APRS Set, Time Mute, CDT Set, TBST Send, BT On/Off, Gps On/Off,
  ... GPS Roam, WX ALarm, Sq Level, CH Set, Predicting (58 in the Short lists).
* **Device Info** (scrolling page): Radio ID 12345678, Radio Name My Radio, Model Name D168UV, Freq Range 400-480 /
  136-174, Firmware Ver 1.07, Hardware Ver 1.00, Data Ver.

## Tree (two levels below each main item; values of option lists inline)

### 0. Talk Group

- TG List → TG 1/1
  - Contact1 → Option
    - View
    - Edit
    - Select → Contact
- New Cont
  - ID → Private ID: Confirm, Private ID
  - Name → 16 (text entry)
  - Address → 14 (text entry)
  - CallSign → 8 (text entry)
  - State → 16 (text entry)
  - Country → 16 (text entry)
  - Remarks → 16 (text entry)
- Manual Dial → Private ID: Option, Private ID
- Talker Alias
  - Tx Set
    - Tx Alias: Off, On
    - Tx Form: ISO 8bit, ISO 7bit, Unicode
  - Rx Dis: OFF, Contact, Alias

### 1. SMS

- New Msg → 100 (text entry)
- InBox: InBox List, Delete All
- OutBox: OutBox List, Delete All
- Text → Text 1/5
  - 你好 → 98 (text entry)
  - 欢迎！ → 97 (text entry)
  - 谢谢你！ → 96 (text entry)
- Draft: Draft List, Delete All

### 2. Call Log

- Last Call: List, Delete All
- Sent: Sent List, Delete All
- Answered: Ans List, Delete All
- Missed: Missed List, Delete All

### 3. Zone

- Zone 1 → Option: Select Zone, As Prior, Edit Name, Edit Chan, Zone Hide, Save, Delete
- Add Zone → Option
  - Edit Name → 16 (text entry)
  - Edit Chan
    - Add Chan
  - Save: popup "Zone Chan Empty!"

### 4. Scan

- On/Off
  - Off → 24-08-01 10:00 — 435.125 | 435.137 | 435.150
- Cdt Scan: popup "Not Work In Digi!"

### 5. Roaming

- OneceRoam → 24-08-01 10:00 — 435.125 | 00 | VFO A
- Roam Zone
  - Roam Zone 1 → Roam Edit
    - Roaming CH 1 → Option
    - Roaming CH 3 → Option
    - Add Chan
  - Add Zone → New Zone
    - Add Chan
    - Edit Name → 5 (text entry)
    - Save: popup "Roam Zone Chan Empty!"
- Auto Roam
  - On/Off: Off, On
  - Fixed Time: 1Min, 2Min, 3Min, 4Min, 5Min, 6Min, 7Min, 8Min, 9Min, 10Min, 11Min, 12Min … (121)
  - Start Roam: Fixed Time, Out of Range
- Rept Check
  - On/Off: Off, On
  - Intervals: 30S, 35S, 40S, 45S, 50S, 5S, 10S, 15S, 20S, 25S
- OutRange
  - Note Kind → OutRange: Off, Beep, Sound
  - Note Times → OutRange: 3 Times, 4 Times, 5 Times, 6 Times, 7 Times, 8 Times, 9 Times, 10 Times, 1 Times, 2 Times
- Effect Wait: OFF, 1Sec, 2Sec, 3Sec, 4Sec, 5Sec, 6Sec, 7Sec, 8Sec, 9Sec, 10Sec, 11Sec … (31)

### 6. Settings

- Radio Set
  - Voice Func
    - Beep Tone: Beep Off, Beep On
    - Digi Idle: Off, Type 1, Type 2, Type 3
    - Ana Idle: Off, On
    - StartSound: Off, On
    - Talk Permit: Off, Digi, Ana, Both
    - D-Rst Tone: Off, On
    - Vol Level: Level 4, Level 5, Level 6, Level 7, Level 8, Indoor, Level 1, Level 2, Level 3
    - Ear Vol: Level 4, Level 5, Level 6, Level 7, Level 8, Indoor, Level 1, Level 2, Level 3
    - Mic sound: Normal, Mic Enhance
    - SMS-Prompt: Off, Ring
    - Call Ring: Off, Ring
    - Digi Mic: Level 2, Level 3, Level 4, Level 5, Auto, Level 1
    - Ana Mic: Level 2, Level 3, Level 4, Level 5, Auto, Level 1
    - Time Mute: Off, On
    - TX NR: Off, On
    - Ana RX NR: Off, On
    - Rx AGC: Off, On
  - DISP Func
    - Back Light: Light 1, Light 2, Light 3, Light 4, Light 5
    - Night Mode: Off, On
    - Light Time: 5S, 10S, 15S, 20S, 25S, 30S, 1Min, 2Min, 3Min, 4Min, 5Min, 15Min … (16)
    - Ch.Name: CH, FREQ
    - Language: English, 简体中文
    - Exit Time: 15S, 20S, 25S, 30S, 35S, 40S, 45S, 50S, 55S, 60S, 5S, 10S
    - Start up: Picture, Character, Customer's
    - Background: Default, Cust's  1, Cust's  2
    - FontColor: White, Black, Orange, Red, Yellow, Green, Cyan, Blue
    - CH Color A: Yellow, Green, Cyan, Blue, White, Orange, Red
    - CH Color B: Yellow, Green, Cyan, Blue, White, Orange, Red
    - Zone CLR.A: Yellow, Green, Cyan, Blue, White, Orange, Red
    - Zone CLR.B: Yellow, Green, Cyan, Blue, White, Orange, Red
    - Main Ch: Chan A, Chan B
    - Sub Ch off: Off, On
  - Key Func
    - Key Lock: Manual, Auto
    - PF1 Short: VFO/MR, Sub Ptt, Scan, FM Radio, Alarm, Rec Switch, Record, SMS, Dial, GPS Info, Monitor, Main CH … (58)
    - PF2 Short: Monitor, Main CH, HotKey 1, HotKey 2, HotKey 3, HotKey 4, HotKey 5, HotKey 6, Work Alone, Scan skip, Digi Moni, Sub Ch off … (58)
    - PF1 Long1: Main CH, HotKey 1, HotKey 2, HotKey 3, HotKey 4, HotKey 5, HotKey 6, Work Alone, Scan skip, Digi Moni, Sub Ch off, Prior Zone … (54)
    - PF2 Long1: OFF, Volt, Tx Power, TalkAround, Reverse, Encryption, Call, VOX, VFO/MR, Scan, FM Radio, Rec Switch … (54)
    - PF1 Long2: OFF, Volt, Tx Power, TalkAround, Reverse, Encryption, Call, VOX, VFO/MR, Scan, FM Radio, Rec Switch … (54)
    - PF2 Long2: OFF, Volt, Tx Power, TalkAround, Reverse, Encryption, Call, VOX, VFO/MR, Scan, FM Radio, Rec Switch … (54)
  - Other Func
    - PowerOff: Off, 10Min, 30Min, 60Min, 120Min
    - TX Timer: 120S, 150S, 180S, 210S, 240S, Off, 30S, 60S, 90S
    - TOT Predict: Off, On
    - TxPow Agc: Off, On
    - Freq Step: 12.5K, 20K, 25K, 30K, 50K, 2.5K, 5K, 6.25K, 8.33K, 10K
    - Sq Level
    - Power Save: Save Off, Save 1:1, Save 2:1
    - TBST Sel: 1000Hz, 1450Hz, 1750Hz, 2100Hz
    - VOX: VOX OFF, VOX 1, VOX 2, VOX 3
    - Vox Delay: Delay 2.0S, Delay 2.1S, Delay 2.3S, Delay 2.6S, Delay 3.0S, Delay 0.9S, Delay 1.5S, Delay 2.2S
    - Scan Mod: SCM TO, SCM CO, SCM SE
    - DTMF Spd: 50 ms, 100 ms, 200 ms, 300 ms, 500 ms
    - FM Radio: Radio Off, Radio On
    - FM Moni: Off, On
    - Start Pwd: Off, On
    - AutoRPT A: Off, Positive, Negative
    - AutoRPT B: Off, Positive, Negative
    - WX Alarm: Off, On
    - SMS format: M-SMS, H-SMS, DMR STD, IP Connect
    - CTC Ste: Silent, 120Degree, 180Degree, 240Degree, 55.2Hz, OFF
    - DCS Ste: 134.4Hz, 55.2Hz
    - No-Sig Ste: OFF, 55.2Hz, 259.2Hz
    - Time Zone: UTC+08:00, UTC+08:30, UTC+09:00, UTC+10:00, UTC+11:00, UTC+12:00, UTC+13:00, UTC-12:00, UTC-11:00, UTC-10:00, UTC-09:00, UTC-08:00 … (32)
    - Time DISP: Off, On
    - Date Time
    - TxPreamble: 300ms, 360ms, 480ms
    - Header RPT: 2, 3, 4, 5, 6, 7, 8, 9, 10
- Chan Set
  - New Chan → Chan Num (text entry)
  - Delete Chan: popup "Can't Del VFO Chan!"
  - CH Type: A-Analog, D-Digital, A+D TX A, D+A TX D
  - Tx Power: Low, Middle, High, Turbo
  - Offset: popup "Offset +0000Hz"
  - Band width: Narrow
  - Rx Freq (text entry)
  - Tx Freq (text entry)
  - TalkAround: Off, On
  - Name: popup "Vfo Chan not Support!"
  - Tx Allow: Always, CH Free, dif.CC, Same CC
  - TX Prohibit: Off, On
  - Radio ID
    - My Radio → Radio ID 1
    - Master Id
    - Add ID → Radio ID
  - RX CC: CC:1, CC:2, CC:3, CC:4, CC:5, CC:6, CC:7, CC:8, CC:9, CC:10, CC:11, CC:12 … (16)
  - TX CC: CC:1, CC:2, CC:3, CC:4, CC:5, CC:6, CC:7, CC:8, CC:9, CC:10, CC:11, CC:12 … (16)
  - R&T CC: CC:0, CC:1, CC:2, CC:3, CC:4, CC:5, CC:6, CC:7, CC:8, CC:9, CC:10, CC:11 … (16)
  - Time Slot: TS 1, TS 2
  - Digi Encrypt: Encrypt Off, Encrypt 1, Encrypt 2, Encrypt 3, Encrypt 4, Encrypt 5, Encrypt 6, Encrypt 7, Encrypt 8, Encrypt 9, Encrypt 10, Encrypt 11 … (33)
  - Encry Type: Normal, Enhance
  - Rx Group
    - Group List 1 → Edit: Edit Name, Add Group, Remove, Delete, Select
    - Add Group → Edit
  - Work Alone: Off, On
  - CH Ranging: Off, On
  - APRS Rec: Off, On
  - SMS Forbid: Off, On
  - Ack Forbid: Off, On
  - Tx Idle: Off, On
  - DMR Mode: Simplex, Repeater, DCDM Dual, DCDM TS
  - Slot Suit: Off, On
  - CRCignore: Off, On
- Device Info

### 7. Record

- On/Off: On, Off
- List: popup "Record List Empty!"
- Delete All: popup "Record List Empty!"

### 8. GPS

- On/Off: GPS Off, GPS On
- GPS Mode: GPS, BDS, GPS+BDS, GLONASS, GPS+GLON, BDS+GLON, ALL
- GPS Roam: Off, On
- GPS Info: popup "GPS Not Turn On"

### 9. Satellite

- Location: popup "No GPS Position"

### 10. Digi Moni

- On/Off: Off, Single Slot, Double Slot
- DigiMoni Cc: Any Cc, Same Cc
- DigiMoni Id: Any Id, Same Id
- Slot Hold: Off, On

### 11. BT

- On/Off: BT Off, BT On
- Pairing: popup "BT Not Open!"
- BT Names → 14 (text entry)
- Pair-Names: popup "BT Not Open!"
- Hold Time: 10S, 11S, 12S, 13S, 14S, 15S, 16S, 17S, 18S, 19S, 20S, 21S … (34)
- RX Delay: 30ms, 1S, 1.5S, 2S, 2.5S, 3S, 3.5S, 4S, 4.5S, 5S, 5.5S
- BT+int spk: Off, On
- Mic Gain: Level 1, Level 2, Level 3, Level 4, Level 5
- Spk Gain: Level 1, Level 2, Level 3, Level 4, Level 5
- NR BEL: 1.00, 1.36, 1.55, 1.65, 1.85, 2.00
- NR AFL: 0.00, 0.10, 0.36, 0.55, 0.65, 1.00
- Pin Code (text entry)
- PTT Pair
  - PTT HOLD → PTT Pair: Connect, Disconnect

### 12. APRS

- Type: None, Sel A Aprs, Sel D Aprs
- Ana APRS
  - PTT Upload: Off, Tx Start, Tx End
  - TX Power: Low, Middle, High, Turbo
  - Band Width: Narrow, Wide
  - TX Freq
    - Freq 1 → TX Freq
    - Freq 2 → TX Freq
    - Freq 3 → TX Freq
  - Signal Path → 49 (text entry)
  - TX Text → 43 (text entry)
- Digi APRS
  - PTT Upload: Off, On
  - Report CH: VFO A
  - Upload Slot: Chan Slot, Slot 1, Slot 2
  - Upload ID
    - Edit ID → Input ID (text entry)
    - Edit Type: Private, Group
- APRS Info
  - Delete All: popup "APRS Info Empty!"
- Intervals: Off, 30S, 60S, 90S, 120S, 150S, 180S, 210S, 240S, 270S, 300S, 330S … (121)
- Beacon: GPS Beacon, FIX 1, FIX 2, FIX 3, FIX 4, FIX 5, FIX 6, FIX 7, FIX 8

