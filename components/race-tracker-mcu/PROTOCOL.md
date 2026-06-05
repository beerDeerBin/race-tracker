# MCU Protocol

This document describes how the race-tracker MCU behaves on the wire — enough to build a simulator, a custom front-end, or to debug from `mosquitto_sub` / `mosquitto_pub`.

This file focuses on **what flows over MQTT, in what order, and exactly which bytes**.

---

## 1. Connection model

The device is an MQTT 3.1.1 client with a **persistent connection**. It stays powered and online: it brings up WiFi → connects to the broker → subscribes to its own command topic at QoS 1 → and then remains connected, polling for commands and publishing a status keepalive every ~5 s. It reconnects automatically if WiFi or the broker drops. The device does **not** deep sleep; to save some energy it runs at a fixed 80 MHz (set once at boot — switching CPU frequency at runtime desyncs the live WiFi connection) and uses WiFi modem sleep when not acquiring.

Because the connection is always up, **commands can be sent at any time** — the device picks them up on its next `MQTT_PollCommand()` (within ~50 ms). Two integration options:

1. Publish to `rt/<uuid>/cmd` with QoS 1 + retained flag — robust across a transient device reconnect.
2. Publish to `rt/<uuid>/cmd` with QoS 1 (non-retained) — fine for a device known to be online.

The retained flag is the more robust integration path.

### Client identity

The device GUID is a `uint16_t[8]` set on first boot and stored in EEPROM. It is formatted as a standard UUID string for all visible identifiers:

```
GUID layout in EEPROM:
data[0]   data[1]   data[2]   data[3]   data[4]   data[5]   data[6]   data[7]
└─uuid[0:4]┴uuid[5:8]┴uuid[9:12]┴uuid[13:16]┴uuid[17:20]┴────uuid[21:32]──────┘

UUID format: XXXXXXXX-XXXX-XXXX-XXXX-XXXXXXXXXXXX
                ↑       ↑    ↑    ↑    ↑
              [0][1]   [2]  [3]  [4]  [5][6][7]
```

Examples:

| Identifier | Format |
|---|---|
| MQTT client ID | `rt-<uuid>` |
| Command topic | `rt/<uuid>/cmd` |
| Status topic | `rt/<uuid>/status` |
| Data topic | `rt/<uuid>/data` |

To discover the device's topics, watch the broker's connection log or subscribe to `rt/+/status` and extract the GUID from the topic path.

---

## 2. State machine

```
                        ┌───────────────────┐
                        │       BOOT        │
                        └────────┬──────────┘
                                 │  always → IDLE, uptime = 0
                                 ▼
              ┌──────────────────────────────────────┐
              │               IDLE                   │
              │  • persistent connection             │
              │  • publishes keepalive every ~5 s    │
              └──────┬───────────────────────────────┘
                     │ CONNECT cmd
                     ▼
              ┌──────────────────────────────────────┐
              │            CONNECTED                 │
              │  • persistent connection             │
              │  • publishes keepalive every ~5 s    │
              └──────┬──────────────┬────────────────┘
                     │ START_RUN    │ DISCONNECT cmd
                     │ (validated)  ▼
                     │       back to IDLE
                     ▼
              ┌──────────────────────────────────────┐
              │            ACQUIRING                 │
              │  • IMU FIFO drained continuously     │
              │  • ~10 evenly-spaced health msgs     │
              │  • batches published when done       │
              │  • transitions to CONNECTED on exit  │
              └──────────────────────────────────────┘

  RESET command (any IDLE/CONNECTED moment): uptime → 0, error code → 0, state → IDLE
```

State is held in RAM only — it is **not** persisted. A reset (power cycle, watchdog, re-flash) always boots into `IDLE` with `uptime = 0`. Only the device GUID survives in EEPROM.

The state determines which commands are accepted — see the **Valid in states** column of the command table in §3.

---

## 3. Wire format conventions

- **Endianness:** little-endian (ESP32 native). All multi-byte integers and floats are serialized as the raw bytes of the C struct.
- **Packing:** all message structs are written / read with `memcpy` against `__attribute__((packed))`-equivalent layouts. No implicit padding past what's shown in the byte tables below.
- **No JSON, no length prefix, no framing.** The MQTT payload length itself is the framing: the device parses the first byte as a command type and treats the rest as the payload for that command.

---

## 4. Commands (FE → device, on `rt/<uuid>/cmd`)

Every command is a single byte optionally followed by a packed payload. The device reads `payload[0]` as the command type.

| Byte | Constant | Payload size | Valid in states |
|------|----------|--------------|------------------|
| `0x01` | `MQTT_CMD_CONNECT` | 0 | IDLE |
| `0x02` | `MQTT_CMD_START_RUN` | 24 (`MqttCmdStartRun_t`) | CONNECTED |
| `0x03` | `MQTT_CMD_DISCONNECT` | 0 | CONNECTED |
| `0x04` | `MQTT_CMD_RESET` | 0 | IDLE, CONNECTED |

A command sent in the wrong state is silently ignored and discarded (there is no serial console). The device stays connected and continues its normal loop.

### 4.1 `CONNECT` (0x01)

```
┌──────────┐
│ 0x01 (1) │
└──────────┘
```

Transitions `IDLE → CONNECTED`. The device publishes one status update with `state = CONNECTED`.

### 4.2 `START_RUN` (0x02)

Total payload: **25 bytes** (1 command byte + 24 of `MqttCmdStartRun_t`).

```
offset │ size │ field        │ type     │ notes
───────┼──────┼──────────────┼──────────┼──────────────────────────────────────
  0    │  1   │ cmd          │ uint8    │ 0x02
  1    │ 16   │ runId        │ uint16[8]│ caller-chosen run UUID (echoed back)
 17    │  4   │ numSamples   │ uint32   │ total samples to collect (LE)
 21    │  1   │ odr          │ uint8    │ ImuManagerOdr_t — see table below
 22    │  1   │ accelRange   │ uint8    │ ImuManagerAccelRange_t
 23    │  1   │ gyroRange    │ uint8    │ ImuManagerGyroRange_t
 24    │  1   │ reserved     │ uint8    │ pad, must be 0
```

#### ODR (`odr`)

| Value | Hz |
|-------|------|
| `0x01` | 12.5 |
| `0x02` | 26 |
| `0x03` | 52 |
| `0x04` | 104 (default) |
| `0x05` | 208 |
| `0x06` | 417 |
| `0x07` | 833 |

#### Accelerometer range (`accelRange`)

| Value | ± g |
|-------|------|
| `0x00` | 2 |
| `0x02` | 4 (default) |
| `0x03` | 8 |
| `0x01` | 16 |

#### Gyroscope range (`gyroRange`)

| Value | ± dps |
|-------|------|
| `0x01` | 125 |
| `0x00` | 250 |
| `0x02` | 500 (default) |
| `0x04` | 1000 |
| `0x06` | 2000 |

#### Pre-run validation

The device rejects the run (stays in `CONNECTED`) if any of:

- `PWR_GetState() == PWR_STATE_CRITICAL_BATTERY` (battery < 3100 mV)
- `DAMGR_Count() > 0` (ring buffer is non-empty — a previous run did not fully publish)

There is no MQTT-level NACK; the rejection is visible only in the next status keepalive (state stays at `CONNECTED`). A critical battery is additionally flagged in the status `errorCode` (`PWR_BATTERY_CRITICAL_ERROR`).

### 4.3 `DISCONNECT` (0x03)

```
┌──────────┐
│ 0x03 (1) │
└──────────┘
```

Transitions `CONNECTED → IDLE`. One status update published.

### 4.4 `RESET` (0x04)

```
┌──────────┐
│ 0x04 (1) │
└──────────┘
```

- Resets the uptime baseline (`uptime → 0`)
- Clears the accumulated `errorCode` (sticky faults → 0)
- Sets `sysState = SYS_STATE_IDLE`
- Publishes a status update with `state=IDLE`, `uptime=0`

Useful for "factory" recovery — the GUID is **not** reset. Hardware re-flash + EEPROM wipe are the only way to change the GUID.

---

## 5. Outbound: status (device → FE, on `rt/<uuid>/status`)

A packed `MqttStatus_t`, always **24 bytes**:

```
offset │ size │ field         │ type   │ notes
───────┼──────┼───────────────┼────────┼─────────────────────────────────────
  0    │  4   │ uptimeMs      │ uint32 │ ms since last fresh boot / reset
  4    │  2   │ batteryMv     │ uint16 │ battery voltage; 65535 = unknown
  6    │  1   │ batteryPct    │ uint8  │ 0–100; 255 = unknown
  7    │  1   │ status        │ uint8  │ SystemState_t (see below)
  8    │  4   │ sampledCount  │ uint32 │ samples collected so far in this run
 12    │  4   │ totalSamples  │ uint32 │ samples requested for this run
 16    │  8   │ errorCode     │ uint64 │ ErrorCodeValue_t bitmask (see below)
```

### When status is published

| Trigger | Frequency |
|---|---|
| Right after (re)connecting to the broker | once per connection |
| On every state transition (`IDLE↔CONNECTED`, `RESET`, end of run) | once |
| Idle / connected keepalive | every ~5 s |
| During `ACQUIRING` | ~10 evenly-spaced updates across the run |

During `ACQUIRING` the status message doubles as the progress signal: `sampledCount` / `totalSamples` rise across the run (the device publishes ~10 evenly-spaced updates), and the data itself arrives in batches at the end (§6).

### Field semantics

- **`uptimeMs`** — ms since the last fresh boot or `RESET` (`millis() − baseline`). Wraps every ~49.7 days.
- **`batteryMv`** — averaged `analogReadMilliVolts(A13) × 2` (Adafruit Feather V2 BAT pin uses an internal 200 K + 200 K divider on ADC1_CH7 / GPIO 35). Set to `65535` if the ADC fails.
- **`batteryPct`** — linear approximation: `clamp((mv − 3000) / 1200 × 100, 0, 100)`. Set to `255` if `batteryMv` is unknown.
- **`status`** — `SystemState_t`:
  - `0` — `SYS_STATE_IDLE`
  - `1` — `SYS_STATE_CONNECTED`
  - `2` — `SYS_STATE_ACQUIRING`
- **`sampledCount` / `totalSamples`** — non-zero only during and at the end of `ACQUIRING`. Both are `0` outside of a run.
- **`errorCode`** — little-endian `uint64` bitmask of `ErrorCodeValue_t` flags (`src/config.h`). Two categories of bits:
  - **Accumulated faults** — OR'd in by `MAIN_Report()` as operations fail (init, connect, publish, IMU read, etc). These accumulate from call sites *since the last successful publish*, then are cleared (`gFaultCode = 0`) immediately after each publish. They do **not** persist indefinitely; a transient failure appears in exactly one status message.
  - **Live condition flags** — recomputed fresh on every publish regardless of `gFaultCode`. Currently only `PWR_BATTERY_CRITICAL_ERROR` (`1 << 42`): set whenever `PWR_GetState() == PWR_STATE_CRITICAL_BATTERY`.
  - `RESET` zeroes the in-RAM accumulator and triggers an immediate publish, so the next received message will have `errorCode = 0` (unless a live condition is active).
  - See §5.1 for the full list of named codes.

### 5.1 Error code table

| Bit | Constant | Subsystem | Notes |
|-----|----------|-----------|-------|
| 0 | `EEPROM_PARAMETER_ERROR` | EEPROM | |
| 1 | `EEPROM_INIT_ERROR` | EEPROM | |
| 2 | `EEPROM_WRITE_ERROR` | EEPROM | |
| 8 | `WIFI_INIT_ERROR` | WiFi | |
| 9 | `WIFI_CONNECT_ERROR` | WiFi | |
| 10 | `WIFI_SHUTDOWN_ERROR` | WiFi | |
| 11 | `WIFI_WAKEUP_ERROR` | WiFi | |
| 12 | `WIFI_SLEEP_ERROR` | WiFi | |
| 16 | `DAMGR_INIT_ERROR` | Data Manager | |
| 17 | `DAMGR_ALLOC_ERROR` | Data Manager | |
| 18 | `DAMGR_OVERFLOW_ERROR` | Data Manager | |
| 24 | `MQTT_CONNECT_ERROR` | MQTT | |
| 25 | `MQTT_PUBLISH_ERROR` | MQTT | |
| 26 | `MQTT_SUBSCRIBE_ERROR` | MQTT | |
| 32 | `IMU_INIT_ERROR` | IMU | |
| 33 | `IMU_CONFIG_ERROR` | IMU | |
| 34 | `IMU_READ_ERROR` | IMU | |
| 35 | `IMU_FIFO_ERROR` | IMU | |
| 40 | `PWR_INIT_ERROR` | Power | |
| 41 | `PWR_ADC_ERROR` | Power | |
| 42 | `PWR_BATTERY_CRITICAL_ERROR` | Power | Live flag (battery < 3100 mV) |

---

## 6. Outbound: data batch (device → FE, on `rt/<uuid>/data`)

Published at the end of a run, one MQTT message per batch of up to **32 samples**.

### Payload layout

```
┌─────────────────────────────────┬─────────────────────────────────────────┐
│ MqttBatchHeader_t (24 bytes)    │ SampleRecord_t[count]  (count × 24 B)   │
└─────────────────────────────────┴─────────────────────────────────────────┘

total size = 24 + count × 24   (max 24 + 32×24 = 792 bytes)
```

### Batch header (24 bytes)

```
offset │ size │ field        │ type     │ notes
───────┼──────┼──────────────┼──────────┼──────────────────────────────
  0    │ 16   │ runId        │ uint16[8]│ echo of the START_RUN runId
 16    │  4   │ startOffset  │ uint32   │ index of first sample in run
 20    │  4   │ count        │ uint32   │ # of SampleRecord_t to follow
```

`startOffset` is 0 for the first batch and increments by `MQTT_MODULE_BATCH_MAX` (32) each batch — **even if the final batch contains fewer than 32 samples**, the next `startOffset` is still incremented by 32. Use `count` to know how many samples are actually present.

### Sample record (24 bytes)

```
offset │ size │ field │ type  │ unit
───────┼──────┼───────┼───────┼────────
  0    │  4   │  ax   │ float │ m/s²
  4    │  4   │  ay   │ float │ m/s²
  8    │  4   │  az   │ float │ m/s²
 12    │  4   │  gx   │ float │ rad/s
 16    │  4   │  gy   │ float │ rad/s
 20    │  4   │  gz   │ float │ rad/s
```

No timestamp — the ODR set in `START_RUN` is the only time reference. The first sample is at `t = 0`; sample `n` is at `t = n / odr_hz`.

### Streaming order

The device publishes batches sequentially with `MQTT_PublishBatch()` while `DAMGR_Count() > 0`. Batches are guaranteed to arrive in order from a single producer at QoS 0. A run with `numSamples = N` will produce `ceil(N / 32)` batches.

---

## 7. Session lifecycle (the happy path)

```
       device                                broker / FE
         │ ─── WIFI connect ─────────────────►   │
         │ ─── MQTT connect ─────────────────►   │
         │ ─── SUBSCRIBE rt/.../cmd (QoS 1) ──►  │
         │ ─── PUBLISH rt/.../status (24B) ───►  │  state=IDLE
         │     (keepalive every ~5s)             │
         │  ◄── PUBLISH rt/.../cmd  [0x01] ────  │  CONNECT
         │ ─── PUBLISH rt/.../status (24B) ───►  │  state=CONNECTED
         │                                       │
         │  ◄── PUBLISH rt/.../cmd  [0x02 ..] ─  │  START_RUN
         │ ─── PUBLISH status (state=ACQUIRING)► │  sampled≈N/10
         │ ─── PUBLISH status (sampled≈2N/10) ─► │
         │   ...                                 │
         │ ─── PUBLISH status (sampled=N) ────►  │  ~10 updates total
         │ ─── PUBLISH data (batch 1) ──────►    │  offset=0,  count=32
         │ ─── PUBLISH data (batch 2) ──────►    │  offset=32, count=32
         │   ...                                 │
         │ ─── PUBLISH status (state=CONNECTED)► │
         │     (connection stays up)             │
```

The connection is never torn down between commands — there is no sleep cycle.

---

## 8. Building a simulator

For testing without real hardware, the minimum a fake device needs to do is:

1. **Pick a UUID** and build the three topics — pick something stable like `00000000-0000-0000-0000-000000000001` so the FE always finds you.
2. **Subscribe to** `rt/<uuid>/cmd` (QoS 1) and **stay connected** (no sleep cycle).
3. **Publish to** `rt/<uuid>/status` every ~5 s with a 24-byte `MqttStatus_t` (include `errorCode`).
4. **On `0x01`**: flip `status` to 1, publish.
5. **On `0x02`**: parse `numSamples`, then publish ~10 evenly-spaced 24-byte status updates with `state=2` and rising `sampledCount`, then publish `ceil(numSamples / 32)` data batches with synthetic floats, then return `status` to 1.
6. **On `0x03`**: flip `status` to 0, publish.
7. **On `0x04`**: zero `uptimeMs` and `errorCode`, flip `status` to 0, publish.

A minimal Python sketch:

```python
import paho.mqtt.client as mqtt
import struct, time

UUID = "00000000-0000-0000-0000-000000000001"
CMD, STATUS, DATA = (f"rt/{UUID}/{leaf}" for leaf in ("cmd", "status", "data"))

state, uptime_ms, error_code = 0, 0, 0   # IDLE

def status_bytes(sampled=0, total=0):
    # uint32 uptimeMs, uint16 mv, uint8 pct, uint8 state, uint32 sampled, uint32 total, uint64 errorCode
    return struct.pack("<IHBBIIQ", uptime_ms, 4100, 91, state, sampled, total, error_code)

def on_msg(client, _, msg):
    global state, uptime_ms, error_code
    cmd = msg.payload[0]
    if cmd == 0x01:       # CONNECT
        state = 1
    elif cmd == 0x02:     # START_RUN
        run_id = msg.payload[1:17]
        num_samples, odr, *_ = struct.unpack("<IBBBB", msg.payload[17:25])
        state = 2
        for k in range(1, 11):                    # 10 evenly-spaced status updates = progress
            sampled = num_samples * k // 10
            client.publish(STATUS, status_bytes(sampled, num_samples))
            time.sleep(num_samples / max(odr, 1) / 10)
        for off in range(0, num_samples, 32):     # synthetic data batches
            n = min(32, num_samples - off)
            hdr = run_id + struct.pack("<II", off, n)
            body = b"".join(struct.pack("<6f", 0,0,9.81, 0,0,0) for _ in range(n))
            client.publish(DATA, hdr + body)
        state = 1
    elif cmd == 0x03:     # DISCONNECT
        state = 0
    elif cmd == 0x04:     # RESET
        state, uptime_ms, error_code = 0, 0, 0
    client.publish(STATUS, status_bytes())

client = mqtt.Client(client_id=f"rt-{UUID}")
client.on_message = on_msg
client.connect("localhost", 1883)
client.subscribe(CMD, qos=1)
while True:                                        # persistent connection
    client.publish(STATUS, status_bytes())
    uptime_ms += 10_000
    client.loop(timeout=10)
```

---

## 9. Debugging from the command line

```sh
# watch everything from a known device
mosquitto_sub -h 192.168.x.x -t 'rt/<uuid>/#' -F '%t %x'

# send CONNECT (retained, so the device picks it up next wake)
mosquitto_pub -h 192.168.x.x -t 'rt/<uuid>/cmd' -r -m '$(printf "\x01")'

# send RESET
mosquitto_pub -h 192.168.x.x -t 'rt/<uuid>/cmd' -r -m '$(printf "\x04")'

# send START_RUN: 0x02, runId=…, numSamples=8330 (LE), odr=104Hz(0x04),
# accelRange=4G(0x02), gyroRange=500dps(0x02), reserved=0
mosquitto_pub -h 192.168.x.x -t 'rt/<uuid>/cmd' -r \
  -f <(printf '\x02\x01\x00\x02\x00\x03\x00\x04\x00\x05\x00\x06\x00\x07\x00\x08\x00\x8A\x20\x00\x00\x04\x02\x02\x00')
```

Decode a status message with `xxd` or Python:

```sh
mosquitto_sub -h 192.168.x.x -t 'rt/<uuid>/status' -F '%x' | while read hex; do
  python3 -c "
import struct, sys
b = bytes.fromhex('$hex')
u, mv, pct, st, sc, tc, err = struct.unpack('<IHBBIIQ', b)
print(f'state={st} uptime={u}ms batt={mv}mV ({pct}%)  sampled={sc}/{tc}  err=0x{err:016x}')
"
done
```

---

## 10. Constants quick reference

| Source | Constant | Value |
|---|---|---|
| `mqtt/messages.h` | `MQTT_CMD_NONE` | `0x00` |
| | `MQTT_CMD_CONNECT` | `0x01` |
| | `MQTT_CMD_START_RUN` | `0x02` |
| | `MQTT_CMD_DISCONNECT` | `0x03` |
| | `MQTT_CMD_RESET` | `0x04` |
| | `MQTT_TOPIC_ROOT` | `"rt"` |
| | `MQTT_TOPIC_DATA` | `"data"` |
| | `MQTT_TOPIC_STATUS` | `"status"` |
| | `MQTT_TOPIC_CMD` | `"cmd"` |
| `mqtt/mqtt_mod.h` | `MQTT_MODULE_BATCH_MAX` | `32` |
| | `MQTT_MODULE_BUFFER_SIZE` | `256` |
| `race-tracker-mcu.ino` | `MAIN_STATUS_INTERVAL_MS` | `10000` |
| | `MAIN_PROGRESS_MARKS` | `10` (status updates per run) |
| | `MAIN_LOOP_DELAY_MS` | `50` |
| `power/pwr_mod.h` | `PWR_VBAT_CRITICAL_MV` | `3100` |
| | `PWR_VBAT_LOW_MV` | `3400` |
| | `PWR_CPU_FREQ_IDLE_MHZ` | `80` (fixed; set once at boot) |

Struct sizes:

| Struct | Bytes |
|---|---|
| `Guid_t` | 16 |
| `SampleRecord_t` | 24 |
| `MqttCmdStartRun_t` | 24 |
| `MqttStatus_t` | 24 |
| `MqttBatchHeader_t` | 24 |
