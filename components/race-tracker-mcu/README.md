# ESP32 Feather V2 — Power-Optimized Sampling Node

**Hardware:** Adafruit ESP32 Feather V2 + Adafruit LSM6DSOX (STEMMA QT)
**Battery target:** 300 mAh / 3.7 V LiPo
**Goal:** weeks of standby, on-demand bursts of high-rate IMU sampling

---

## State machine

```
                          ┌──────────────────────────┐
                          │           BOOT           │
                          └─────────────┬────────────┘
                                        ▼
   ┌───────────────────────────────────────────────────────────┐
   │                       IDLE  (default)                     │
   │  • CPU @ 80 MHz                                           │
   │  • WiFi connected, modem-sleep (~15-20 mA peak, ~1-2 avg) │
   │  • LSM6DSOX powered down (~3 µA)                          │
   │  • Light-sleep between keep-alives (~0.8 mA)              │
   │  • Keep-alive every N seconds (default 10)                │
   │     → {"state":"idle","uptime_s":..., "vbat_mv":..., ...} │
   └──────────────┬───────────────────────────────▲────────────┘
                  │ {"cmd":"start", ... }         │ duration_s elapsed
                  │                               │ OR {"cmd":"stop"}
                  ▼                               │
   ┌──────────────────────────────────────────────┴────────────┐
   │                      ACQUIRING                            │
   │  • CPU @ 240 MHz                                          │
   │  • LSM6DSOX active @ commanded ODR                        │
   │  • Samples → PSRAM ring buffer                            │
   │  • Batch MQTT publish every 30 s (or when ring fills)     │
   │  • Auto-exits to IDLE after duration_s                    │
   └───────────────────────────────────────────────────────────┘
```

---

## MQTT command surface

All commands are JSON published to `sensor/cmd`.

### Start acquisition
```json
{
  "cmd": "start",
  "odr": 104,           // Hz: 12, 26, 52, 104, 208, 416, 833, 1660, 3330, 6660
  "range_g": 4,         // accel: 2, 4, 8, 16
  "range_dps": 500,     // gyro: 125, 250, 500, 1000, 2000
  "duration_s": 30      // 1..600
}
```

### Stop acquisition (return to IDLE immediately)
```json
{"cmd":"stop"}
```

### Change keep-alive interval
```json
{"cmd":"keepalive_interval","seconds":30}
```

### Other
```json
{"cmd":"reset"}          // soft reboot
{"cmd":"factory_reset"}  // wipe EEPROM + reboot
```

---

## Published topics

| Topic | When | Payload shape |
|---|---|---|
| `sensor/keepalive` | every N s in IDLE | `{"state":"idle","uptime_s":...,"vbat_mv":...,"batt_pct":...,"rssi":...}` |
| `sensor/status` | on state transitions | `{"state":"acquiring","odr":...,...}` etc. |
| `sensor/data` | batched, every 30 s while ACQUIRING | `{"samples":[{"ts":...,"ax":...,"ay":...,...},...]}` |

---

## Power budget (300 mAh LiPo, usable ~270 mAh down to 3.2 V)

| Phase | Avg current | Runtime if continuous |
|---|---|---|
| IDLE keep-alive every 10 s | **~1-3 mA** | **~3-10 days** |
| ACQUIRING @ 104 Hz | ~80-120 mA | ~2-3 hours |
| ACQUIRING @ 416 Hz | ~100-140 mA | ~2 hours |
| Deep sleep (critical battery fallback) | ~70-100 µA | ~100-150 days |

Concrete example duty cycle: **1 min ACQUIRING + 59 min IDLE per hour** ≈ 4 mA avg → **~2 months** on one charge.

---

## File structure

```
ESP32_Feather_V2/
├── ESP32_Feather_V2.ino       ← main sketch (setup + loop with light-sleep)
├── config.h                   ← constants, pins, shared typedefs
├── eeprom_mod.h    / .cpp     ← Eeprom    — 1 kB persistent config
├── data_manager.h  / .cpp     ← DataMgr   — 2 MB PSRAM ring buffer
├── wifi_mod.h      / .cpp     ← Wifi      — STA + modem-sleep + TX power
├── mqtt_mod.h      / .cpp     ← Mqtt      — PubSubClient + JSON cmd parser
├── imu.h           / .cpp     ← Imu       — LSM6DSOX wrapper, power-down
├── energy.h        / .cpp     ← Energy    — CPU freq, light/deep sleep
├── state_machine.h / .cpp     ← Fsm       — IDLE / ACQUIRING orchestration
└── tasks.h         / .cpp     ← Tasks     — Core 0 sampling + batch flush
```

---

## Required libraries

| Library | Author |
|---|---|
| `PubSubClient` | Nick O'Leary |
| `Adafruit NeoPixel` | Adafruit |
| `Adafruit LSM6DS` | Adafruit (installs `Adafruit_LSM6DSOX.h`) |
| `Adafruit Unified Sensor` | Adafruit (dep of LSM6DS) |
| `Adafruit BusIO` | Adafruit (dep of LSM6DS) |
| `EEPROM` | built-in for ESP32 |

---

## Arduino IDE board settings

- **Board:** Adafruit ESP32 Feather V2
- **Partition Scheme:** Huge APP (3MB No OTA / 1MB SPIFFS)
- **CPU Frequency:** 240MHz (we drop it at runtime via `setCpuFrequencyMhz()`)
- **Upload Speed:** 921600

---

## Where to tune for less current

Most impactful, in order:

1. **Keep-alive interval** — raise `KEEPALIVE_DEFAULT_S` to 30 or 60 s; idle avg drops accordingly. Just send `{"cmd":"keepalive_interval","seconds":60}`.
2. **WiFi TX power** — `WIFI_TX_POWER_DBM` in `config.h` is already 11 dBm; can go to `WIFI_POWER_8_5dBm` or lower if RSSI permits.
3. **CPU idle freq** — `CPU_FREQ_IDLE_MHZ` is 80 MHz; 40 MHz is the lowest stable with WiFi but cuts modem-sleep current ~25%.
4. **Acquisition duration** — keep bursts short. The IDLE budget dominates total runtime.
5. **Drop WiFi entirely** for ultra-long standby — change `_enterIdle()` to `Wifi.shutdown()` and use deep-sleep with `Energy.deepSleep(keepalive_interval_us)`. ~100 µA avg, but loses MQTT inbound for the sleep window.
