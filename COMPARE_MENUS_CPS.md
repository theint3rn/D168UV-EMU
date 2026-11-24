# D168UV radio menus vs stock CPS vs cfw UI

Sources:
* **Radio**: stock V1.07 menu crawl in the emulator (`MENUS.md`, `out/crawl/tree.md`; 2026-11-22). Values listed in
  the radio's order; an option list opens on its current value, so the first value shown is the factory default of the
  emulator's codeplug (K6RRR-based NAND image).
* **CPS**: stock D168UV CPS V1.07 map `D890UV_src/cfw_cps/docs/STOCK_CPS_MAP_D168.md` and `schema/stock_d168uv.json`
  (CSV key = Optional Setting column, GUI label in brackets; ".rdt only" = in the codeplug file, not in the CSV export).
* **cfw**: D168 custom firmware UI (`D168UV/cfw`, shared `D890UV_src/cfw/app/ui.c`): main screen (channel / VFO, keypad
  frequency entry), menu *Mode, Channel settings, Step, RX polarity, De-emphasis comp, Test tone, Info, Save now*, keys
  PF1 = mode cycle, PF2 = VFO/MEM, long `*` = key lock, knob = channel.

Legend: **=** maps to the CPS key; **?** = probable match (label / value range agree, byte not checked);
**—** = no CPS field found.

## 1. Settings > Radio Set

### Voice Func
| Radio item | Values (radio) | CPS |
|---|---|---|
| Beep Tone | Beep Off, Beep On | = `Beep` (Key Tone) |
| Digi Idle | Off, Type 1-3 | = `SqOnVoice` (Digi Idle Channel Tone) |
| Ana Idle | Off, On | = `AnaSqOnVoice` (Ana Idle Channel Tone) |
| StartSound | Off, On | = `Boot_Sound` (Startup Sound) |
| Talk Permit | Off, Digi, Ana, Both | = `TalkTips` (Call Tone) ? |
| D-Rst Tone | Off, On | = `OverVoice` (Digi Call ResetTone) |
| Vol Level | Level 1-8, Indoor | = `MaxVol` (Maximum Volume) |
| Ear Vol | Level 1-8, Indoor | = `EarMaxVol` (Max Headphone Volume) |
| Mic sound | Normal, Mic Enhance | = `EnSoundEffect` (Enhanced Sound Quality) ? |
| SMS-Prompt | Off, Ring | = `MsgRing` (SMS Alert) |
| Call Ring | Off, Ring | = `CallRing` (Call Alert) |
| Digi Mic | Level 1-5, Auto | = `MicLevel` (Digit Mic Gain) |
| Ana Mic | Level 1-5, Auto | = `AnaMic` (Ana Mic Gain) |
| Time Mute | Off, On | = `FixTimeMute` (Mute timing) |
| TX NR | Off, On | = `TxNr` |
| Ana RX NR | Off, On | = `RxNr` |
| Rx AGC | Off, On | = `Drc` (DRC Dynamic Range Control (Rx AGC)) |

CPS-only in this group: `VolNoteEn` (Key Sound Adjustable).

### DISP Func
| Radio item | Values | CPS |
|---|---|---|
| Back Light | Light 1-5 | = `Lightness` (Brightness) |
| Night Mode | Off, On | = `NightMode`; start / end time `NlightStart*`/`NlightEnd*` are CPS only |
| Light Time | 5S ... 45Min, 1 Hour, Always | = `AutoBKLightTime` |
| Ch.Name | CH, FREQ | = `FreqDis` (Display mode) ? |
| Language | English, 简体中文 | = `Language` (CSV only, no GUI label) |
| Exit Time | 5S ... 60S | = `MenuLowPower` (Menu Exit Time) |
| Start up | Picture, Character, Customer's | = `StartDspSet` (Power-on Interface) |
| Background | Default, Cust's 1, Cust's 2 | = `bkpic` (Standby BK Picture) |
| FontColor | White, Black, Orange, Red, Yellow, Green, Cyan, Blue | = `WorkCharDisColour` (Standby Char Color) |
| CH Color A / B | Orange, Red, Yellow, Green, Cyan, Blue, White | = `ChanNameColour` / `ChanNameColourB` |
| Zone CLR.A / B | same 7 colours | = `ZoneNameColourA` / `ZoneNameColourB` |
| Main Ch | Chan A, Chan B | = `MainState` (Main Channel Set) |
| Sub Ch off | Off, On | = `SubMode` (Sub-Channel Mode) ? |

CPS-only display settings (no radio menu): `TxDimWait`, `RxDimWait`, `DateDisFormat`, `LastCallDis`, `CallModeDisKind`,
`CallSignColour`, `ChanNumDisKind`, `MenuDisWorkContact`, `GpsTextUsed`, `DiviDisEn`, `LastHeardChanSet`, `ChanDisCtr`,
`ZoneBarsEn`, night-mode times, Display Time Slot / Color Code (.rdt only).

### Key Func
| Radio item | Values | CPS |
|---|---|---|
| Key Lock | Manual, Auto | = `KeyLock` |
| PF1 Short, PF2 Short | ~55 functions (list below) | Key Function tab, **.rdt only** (not in CSV) |
| PF1 Long1, PF2 Long1, PF1 Long2, PF2 Long2 | OFF + the same functions | Key Function tab, .rdt only |

Factory values seen: PF1 Short = VFO/MR, PF2 Short = Monitor, PF1 Long1 = Main CH, PF2 Long1 / PF1 Long2 / PF2 Long2 = OFF.
Function list (radio order): OFF, Volt, Tx Power, TalkAround, Reverse, Encryption, Call, VOX, VFO/MR, Sub Ptt, Scan,
FM Radio, Alarm, Rec Switch, Record, SMS, Dial, GPS Info, Monitor, Main CH, HotKey 1-6, Work Alone, Scan skip, Digi Moni,
Sub Ch off, Prior Zone, VFO Scan, Mic sound, LastCall, ChType, Ranging, Roaming, CH Ranging, Max VOL, Slot Set, Aprs Type,
Zone Select, Roam Set, APRS Set, Time Mute, CDT Set, TBST Send, BT On/Off, Gps On/Off, ..., GPS Roam, WX ALarm, Sq Level,
CH Set, Predicting. CPS-only: `PfLongTime` (Long Key 2 Time), Knob / Keyboard / Side / Forced key locks (.rdt only).

### Other Func
| Radio item | Values | CPS |
|---|---|---|
| PowerOff | Off, 10/30/60/120Min | = `AutoPowerOff` (Auto Shutdown); `ApoKind` CPS only |
| TX Timer | Off, 30S ... 240S | = `TOT` |
| TOT Predict | Off, On | = `TotPreEn` |
| TxPow Agc | Off, On | = `TxAgcCon` |
| Freq Step | 2.5K, 5K, 6.25K, 8.33K, 10K, 12.5K, 20K, 25K, 30K, 50K | = `Step` |
| Sq Level | (level editor, current VFO) | = `SQL1` / `SQL2` |
| Power Save | Save Off, Save 1:1, Save 2:1 | = `PowerSave` |
| TBST Sel | 1000, 1450, 1750, 2100 Hz | = `TBST` |
| VOX | VOX OFF, VOX 1-3 | = `VOX` (VOX Level) |
| Vox Delay | Delay 2.0S, 2.1S, 2.2S, ... (0.1 s steps) | = `VOX_Delay` |
| Scan Mod | SCM TO, SCM CO, SCM SE | = `ScanType` (VFO Scan Type) ? |
| DTMF Spd | 50, 100, 200, 300, 500 ms | — (D168 `DTMFEncode` has only ID / Encode columns; no speed key found) |
| FM Radio | Radio Off, Radio On | = `FM_En` (FM Function) |
| FM Moni | | = `FmMon` (FM Monitor) |
| Start Pwd | Off, On | = `Password` (Power-on Password); the password text `Password_Char` is CPS only |
| AutoRPT A / B | Off, Positive, Negative | = `AutoRepeater` / `AutoRepeaterB` |
| WX Alarm | Off, On | = `WxAlarmSign` (CSV only) |
| SMS format | M-SMS, H-SMS, DMR STD, IP Connect | = `SmsFormat` |
| CTC Ste | Silent, 120/180/240 Degree, 55.2Hz, OFF | = `STE_Type` |
| DCS Ste | 134.4Hz, 55.2Hz | = `DcsSte` |
| No-Sig Ste | OFF, 55.2Hz, 259.2Hz | = `STE_Freq` (STE When No Signal) |
| Time Zone | UTC-12:00 ... UTC+13:00 (32 zones, UTC+08:00 default) | = `TmZone` |
| Time DISP | Off, On | = `DateDisKind` (Time Display) |
| Date Time > Time Set / GPS Check / Format | clock editor; GPS time check; yy/mm/dd, dd/mm/yy | Time Set, GPS Check: — (RTC); Format = `DateDisFormat` |
| TxPreamble | 300ms, 360ms, ... | = `PreWave` (TX Preamble Duration) |
| Header RPT | 2-10 | = `Preamble` (Voice Header Repetitions) |

CPS-only: `TxAgcCon`... (see above), `SteTime`, `ContactOutSetEn`, `AnaHoldTime`, `CurTalkPathHold`, `PriZoneA/B`
(radio: Zone > Option > As Prior), `MonType`, `FailAlarm`, `IdleWait`, `ManDownWait` (man-down), `StartChUse` and
startup channels, `StartResetEn`, `Start_Char*` texts.

### Chan Set (current channel / VFO; Channel table columns)
| Radio item | Values | CPS (Channel column) |
|---|---|---|
| New Chan / Delete Chan | channel number entry; "Can't Del VFO Chan!" on a VFO | Channel rows |
| CH Type | A-Analog, D-Digital, A+D TX A, D+A TX D | Channel Type |
| Tx Power | Low, Middle, High, Turbo | Transmit Power |
| Offset | +0000Hz (editor) | Transmit Frequency (offset is derived) |
| Band width | Narrow (Wide on analog) | Band Width |
| Rx Freq / Tx Freq | frequency editors | Receive / Transmit Frequency |
| TalkAround | Off, On | Talk Around(Simplex) |
| Name | "Vfo Chan not Support!" on a VFO | Channel Name |
| Tx Allow | Always, CH Free, dif.CC, Same CC | Busy Lock/TX Permit |
| TX Prohibit | Off, On | PTT Prohibit |
| Radio ID (My Radio, Master Id, Add ID: Edit Id / Edit Name / Save) | | Radio ID (column) + `RadioIDList` table; Master ID dialog (.rdt only) |
| RX CC / TX CC / R&T CC | CC:0-15 | RX Color Code / `txcc` (TxCc); R&T sets both |
| Time Slot | TS 1, TS 2 | Slot |
| Digi Encrypt | Encrypt Off, Encrypt 1-32 | Digital Encryption |
| Encry Type | Normal, Enhance | AES Digital Encryption / ARC4 ? (Encryption Type is .rdt only in the CPS map) |
| Rx Group (Group List 1: Edit Name, Add Group, Remove, Delete) | | Receive Group List + `ReceiveGroupCallList` table |
| Work Alone | Off, On | Work Alone |
| CH Ranging | Off, On | Ranging |
| APRS Rec | Off, On | APRS RX |
| SMS Forbid | Off, On | SMS Confirmation ? |
| Ack Forbid | Off, On | DataACK Disable |
| Tx Idle | Off, On | Idle TX |
| DMR Mode | Simplex, Repeater, DCDM Dual, DCDM TS | DMR MODE (Through Mode) |
| Slot Suit | Off, On | Slot Suit |
| CRCignore | Off, On | dmr_crc_ignore |

The digital VFO A was the current channel, so analog-only items (CTCSS/DCS, squelch mode, optional signal, scrambler)
were not shown; the CPS Channel table has them (CTCSS/DCS Decode/Encode, Squelch Mode, Optional Signal, DTMF/2Tone/5Tone ID,
PTT ID, Custom CTCSS, 2TONE Decode, Compand, ...).
Channel columns with no radio item seen: Contact/Talk Group (set from the TG list), Scan List, Call Confirmation,
Correct Frequency, Exclude channel from roaming, Auto Scan, APRS PTT modes / report type / channel, Send Talker Alias,
R5Tone BOT/EOT, DisturEn / DisturFreq.

## 2. Other main-menu settings
| Radio | Values | CPS |
|---|---|---|
| Talk Group > Talker Alias > Tx Set > Tx Alias | Off, On | Talk Alias dialog (.rdt only) |
| Talk Group > Talker Alias > Tx Set > Tx Form | ISO 8bit, ISO 7bit, Unicode | Talk Alias dialog (.rdt only) |
| Talk Group > Talker Alias > Rx Dis | OFF, Contact, Alias | Talk Alias dialog (.rdt only) |
| Talk Group > TG List / New Cont / Manual Dial | contacts | `TalkGroups`, `DigitalContactList` tables |
| SMS > Text (5 preset texts) | | `PrefabricatedSMS` |
| Zone > Option: Select Zone, As Prior, Edit Name, Edit Chan, Zone Hide, Save, Delete | | `Zone` table; As Prior = `PriZoneA/B`; Zone Hide: — (no CSV column) |
| Scan > On/Off, Cdt Scan | | `ScanList`; Cdt Scan: — |
| Roaming > Roam Zone / channels | Rx, Tx, CC, Slot, Name | `RoamingZone`, `RoamingChannel` |
| Roaming > Auto Roam > On/Off, Fixed Time (1-121 min), Start Roam (Fixed Time, Out of Range) | | `TimeRoamOn`, `WanderPerod`, `FixRomanStartOp` |
| Roaming > Rept Check > On/Off, Intervals (10-50 s) | | `BsModeCheck`, `TimeBsCheck`; `BsCheckTimes` CPS only |
| Roaming > OutRange > Note Kind (Off, Sound, Beep), Note Times (3-10) | | `OutRepNote`, `OutNoteTimes` |
| Roaming > Effect Wait | OFF, 1-30 Sec | `WanderEffectWait` |
| Record > On/Off | On, Off | `Record_En`; `RecordDelay`, `WtRecordNote` CPS only |
| GPS > On/Off | GPS Off, GPS On | — (no CSV key found; `StartTestGps`?) |
| GPS > GPS Mode | GPS, BDS, GPS+BDS, GLONASS, GPS+GLON, BDS+GLON, ALL | = `GpsMode` (CSV only) ? |
| GPS > GPS Roam | Off, On | = `Gps` (GPS Roaming) |
| Satellite > Location: GPS Beacon, FIX 1-8 (Edit Name, View&Select) | | `SateLocalKind`, `SateFixPointName1-8` |
| Digi Moni > On/Off | Off, Single Slot, Double Slot | = `DigiMoni` |
| Digi Moni > DigiMoni Cc / Id | Any/Same Cc, Any/Same Id | = `DigiMoniCc` / `DigiMoniId` |
| Digi Moni > Slot Hold | Off, On | = `Scan_Pri_Current` (Monitor Slot Hold; the CPS map found no .rdt byte for it) |
| BT > On/Off | BT Off, BT On | = `BlueToothOn` (CSV only) |
| BT > Hold Time | OFF, 1-30S, 60S, 120S, infinite | = `BhtHoldTime` ? |
| BT > RX Delay | 30ms, 1S-5.5S | = `BhtHoldDelay` ? |
| BT > BT+int spk | Off, On | = `SpkInBlueTooth` ? |
| BT > Mic Gain / Spk Gain | Level 1-5 | = `BhtMicGain` / `BhtSpkGain` |
| BT > PTT Pair > PTT HOLD | Connect, Disconnect | = `BhtPttHold` ? |
| BT > BT Names, Pin Code, NR BEL, NR AFL, Pairing, Pair-Names | | — (module settings, sent as AT commands) |
| APRS > Type | None, Sel A Aprs, Sel D Aprs | `APRS` table / channel `APRS Report Type` ? |
| APRS > Ana APRS > PTT Upload, TX Power, Band Width, TX Freq (Freq 1-8), Signal Path, TX Text | | `APRS` columns Transmission Frequency0-7, Transmit Power, APRS Signal Path, Enter Your Sending Text; PTT mode per channel (`Analog APRS PTT Mode`) |
| APRS > Digi APRS > PTT Upload, Report CH, Upload Slot, Upload ID (ID, Private/Group) | | channel `Digital APRS PTT Mode` / `Digital APRS Report Channel`, `APRS` `APRS TG` / `Call Type` |
| APRS > Intervals | Off, 30S ... 3600S | = `APRS Auto TX Intervals[s]` |
| APRS > Beacon | GPS Beacon, FIX 1-8 | = `Fixed Location Beacon` + `LatiDegree1-4` ... |

## 3. Findings

**Radio settings without a CPS field** (not in the CSV schema; some may sit in .rdt bytes the map did not identify):
GPS On/Off, BT Names / Pin Code / NR BEL / NR AFL (module-side), Zone Hide, Cdt Scan, DTMF Spd (D168 CSV has no DTMF speed),
Date Time (RTC). PF key assignments and Talker Alias are in the .rdt but not in the CSV (already listed in the CPS map).

**CPS keys with no radio menu**: most of the Display tab (call display, colours of call sign, channel number, last
caller, TX/RX backlight delays, date format, zone bars), power-on texts / password char / startup channels, auto-repeater
frequency ranges and offsets, VFO scan ranges, repeater check reconnections, satellite TX power / squelch / AOS limit,
`ManDownWait` and the Alarm Setting dialog (man-down / emergency), QDC1200, 2-Tone / 5-Tone / DTMF tables, encryption keys,
hot keys (only selectable as PF functions), GPS roaming table, `ContactOutSetEn`, `AnaHoldTime`, `CurTalkPathHold`, `SteTime`,
`MonType`, `FailAlarm`, `IdleWait`, `MsgOacsuSet`, `MeasurePeriod`, `PttSleepTime`, `DmrRasEn`, `VcallRpheader`.

**Labels the CPS map could not confirm, now named from the radio menu**: `Language`, `WxAlarmSign` (WX Alarm),
`GpsMode` (GPS Mode), `BlueToothOn` (BT On/Off), `BhtMicGain` / `BhtSpkGain` (BT Mic / Spk Gain), `BhtHoldTime`,
`BhtHoldDelay`, `SpkInBlueTooth`, `BhtPttHold`. The D168 CPS has no Bluetooth page although the radio has a full BT menu
and module (Bt168.cs answers it in the emulator).

**Stock features the cfw UI lacks** (cfw is a multi-mode digital test firmware; its UI has 8 menu entries): contacts /
talk groups browsing and manual dial, talker alias, SMS, call log, zones and zone editing, scan / CDT scan, roaming and
repeater check, call recording, GPS (receiver, info, roaming), satellite tracking / fixed locations, digital monitor,
Bluetooth, APRS (analog and DMR), FM broadcast radio, PF key assignment (cfw: fixed PF1 mode / PF2 VFO), all audio settings
(tones, volume limits, mic gains, NR, AGC), display settings (backlight, colours, language, night mode), power save / auto
power off / TOT / VOX / STE / TBST / auto repeater, key lock modes (cfw: long `*` only), date / time / time zone.
cfw has instead: digital modes beyond DMR (P25, NXDN, D-STAR, YSF, ...), per-channel mode editing, RX polarity /
de-emphasis compensation, test tone and an info page.

## 4. Schema change made (cfw_cps)
`tools/make_stock_schema.py`: D168 schema gets an informational `radio_menu` map (the ten CSV-only keys above -> radio
menu path; "?" where only the value range agrees). `schema/stock_d168uv.json` and `web/schema/stock_d168uv.json` were
regenerated: identical apart from the new key. `tools/make_stock_map_doc.py` shows the radio name in the "More (CSV only)"
table of `docs/STOCK_CPS_MAP_D168.md`. GUI labels are unchanged (still only GUI-confirmed ones). `make test`: 80 passed.
Not changed (listed above instead): radio settings with no CSV key (GPS On/Off, DTMF Spd, Zone Hide, BT module settings),
because they need .rdt byte work in the stock CPS.
