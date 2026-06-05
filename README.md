# race-tracker

Battery-powered IMU acquisition system. An ESP32 node wakes on a timer, streams high-rate accelerometer + gyroscope samples over MQTT, and goes back to sleep.

```
┌─────────────────────┐
│   race-tracker-mcu  │  ESP32 + LSM6DSOX — real hardware
│  (or simulator)     │
└──────────┬──────────┘
           │  binary MQTT  (rt/<guid>/cmd · status · data)
           ▼
┌─────────────────────┐       ┌─────────────────────┐
│  mqtt (Mosquitto)   │ ◄───  │ race-tracker-tester │  data viewer · :5000
│   broker @ :1883    │       └─────────────────────┘
└─────────────────────┘
           ▲
           │
┌─────────────────────┐
│     MQTTX web       │  ad-hoc debugger · :8080
└─────────────────────┘
```

---

## Components

| Path | Role |
|---|---|
| `components/race-tracker-mcu/` | ESP32 firmware — IMU sampling, MQTT, deep sleep |
| `components/simulator/` | Software-only fake device — runs multiple simulated nodes over MQTT |
| `components/mqtt/` | Mosquitto broker + MQTTX web UI + Flask data-viewer |
| `components/Tiltfile` | Orchestrates everything with [Tilt](https://tilt.dev) |

---

## Running the stack

```sh
cd components/
tilt up
```

Brings up the broker (`:1883`), MQTTX web (`:8080`), and the data viewer (`:5000`).  
The simulator starts automatically with the devices configured in `components/simulator/config.yaml`.

Tilt UI at http://localhost:10350 has buttons on the `race-tracker-simulator` resource to manage devices and overall config without editing YAML by hand.

---

## MCU (real hardware)

**Hardware:** Adafruit ESP32 Feather V2 · Adafruit LSM6DSOX (STEMMA QT) · LP-552035 LiPo 350 mAh

```sh
# one-time setup
arduino-cli core install esp32:esp32@3.3.8
cp components/race-tracker-mcu/src/secrets.h.example components/race-tracker-mcu/src/secrets.h
# edit secrets.h with your WiFi + broker credentials

# compile + flash
arduino-cli compile --profile profile-race-tracker --upload

# logs
arduino-cli monitor -p /dev/ttyACM0 -c baudrate=115200
```

---

## Wire format

See [`components/race-tracker-mcu/PROTOCOL.md`](./components/race-tracker-mcu/PROTOCOL.md) for the full MQTT topic layout, binary message structs, and device state machine.
