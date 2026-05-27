import struct
import threading
import paho.mqtt.client as mqtt
from flask import Flask, render_template, jsonify

BROKER_HOST = "mosquitto"
BROKER_PORT = 1883

SAMPLE_FMT     = "<6f"
SAMPLE_SIZE    = struct.calcsize(SAMPLE_FMT)
BATCH_HDR_FMT  = "<3I"
BATCH_HDR_SIZE = struct.calcsize(BATCH_HDR_FMT)
STATUS_FMT     = "<IB"
STATUS_SIZE    = struct.calcsize(STATUS_FMT)

app = Flask(__name__)

_lock       = threading.Lock()
last_run    = {"runId": None, "records": []}
last_status = {"uptimeMs": 0, "state": "idle"}


def decode_data(payload: bytes) -> None:
    if len(payload) < BATCH_HDR_SIZE:
        print(f"[data] too short ({len(payload)} bytes)")
        return

    run_id, start_offset, count = struct.unpack_from(BATCH_HDR_FMT, payload)
    expected = BATCH_HDR_SIZE + count * SAMPLE_SIZE
    if len(payload) < expected:
        print(f"[data] truncated: expected {expected} bytes, got {len(payload)}")
        return

    records = []
    for i in range(count):
        ax, ay, az, gx, gy, gz = struct.unpack_from(SAMPLE_FMT, payload, BATCH_HDR_SIZE + i * SAMPLE_SIZE)
        records.append({"ax": ax, "ay": ay, "az": az, "gx": gx, "gy": gy, "gz": gz})
        print(f"  [{start_offset + i:5d}]  ax={ax:8.3f}  ay={ay:8.3f}  az={az:8.3f}    gx={gx:8.3f}  gy={gy:8.3f}  gz={gz:8.3f}")

    print(f"[data] runId={run_id}  offset={start_offset}  count={count}")

    with _lock:
        if last_run["runId"] != run_id:
            last_run["runId"] = run_id
            last_run["records"] = []
        needed = start_offset + count
        if len(last_run["records"]) < needed:
            last_run["records"].extend([None] * (needed - len(last_run["records"])))
        for i, r in enumerate(records):
            last_run["records"][start_offset + i] = r


def decode_status(payload: bytes) -> None:
    if len(payload) < STATUS_SIZE:
        print(f"[status] too short ({len(payload)} bytes)")
        return
    uptime_ms, status = struct.unpack_from(STATUS_FMT, payload)
    state = "running" if status else "idle"
    print(f"[status] uptime={uptime_ms} ms  state={state}")
    with _lock:
        last_status["uptimeMs"] = uptime_ms
        last_status["state"] = state


def on_connect(client, userdata, flags, rc, properties=None):
    if rc == 0:
        print("[decoder] connected — subscribing to rt/#")
        client.subscribe("rt/#")
    else:
        print(f"[decoder] connection failed: rc={rc}")


def on_message(client, userdata, msg):
    parts = msg.topic.split("/")
    if len(parts) < 3:
        return
    kind = parts[2]
    if kind == "data":
        decode_data(bytes(msg.payload))
    elif kind == "status":
        decode_status(bytes(msg.payload))


def mqtt_thread():
    client = mqtt.Client(mqtt.CallbackAPIVersion.VERSION2)
    client.on_connect = on_connect
    client.on_message = on_message
    print(f"[decoder] connecting to {BROKER_HOST}:{BROKER_PORT} ...")
    client.connect(BROKER_HOST, BROKER_PORT)
    client.loop_forever()


@app.route("/")
def index():
    return render_template("index.html")


@app.route("/api/data")
def api_data():
    with _lock:
        return jsonify({"run": last_run, "status": last_status})


if __name__ == "__main__":
    t = threading.Thread(target=mqtt_thread, daemon=True)
    t.start()
    app.run(host="0.0.0.0", port=5000, use_reloader=False)
