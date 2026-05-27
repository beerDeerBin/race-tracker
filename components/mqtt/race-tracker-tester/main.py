import struct
import threading
import paho.mqtt.client as mqtt
from flask import Flask, render_template, jsonify, request

BROKER_HOST = "mosquitto"
BROKER_PORT = 1883

SAMPLE_FMT     = "<6f"
SAMPLE_SIZE    = struct.calcsize(SAMPLE_FMT)
BATCH_HDR_FMT  = "<8HII"  # Guid_t (8×uint16) + startOffset + count
BATCH_HDR_SIZE = struct.calcsize(BATCH_HDR_FMT)
STATUS_FMT     = "<IHBBII"  # uptimeMs, batteryMv, batteryPct, status, sampledCount, totalSamples
STATUS_SIZE    = struct.calcsize(STATUS_FMT)

CMD_CONNECT    = 0x01
CMD_START_RUN  = 0x02
CMD_DISCONNECT = 0x03
CMD_RESET      = 0x04

app = Flask(__name__)

_lock        = threading.Lock()
_mqtt_client = None

current_guid  = None
last_run      = {"runId": None, "records": []}
last_status   = {"uptimeMs": 0, "state": "idle", "batteryMv": 0, "batteryPct": 0, "sampledCount": 0, "totalSamples": 0}
keepalive_log = []
known_guids   = []   # ordered list, most-recently-seen first

MAX_KEEPALIVE_LOG = 50


def _guid_words_to_str(words) -> str:
    return f"{words[0]:04X}{words[1]:04X}-{words[2]:04X}-{words[3]:04X}-{words[4]:04X}-{words[5]:04X}{words[6]:04X}{words[7]:04X}"


def _parse_guid(guid_str: str):
    h = guid_str.replace("-", "")
    return [int(h[i:i+4], 16) for i in range(0, 32, 4)]


def decode_data(payload: bytes) -> None:
    if len(payload) < BATCH_HDR_SIZE:
        return
    unpacked     = struct.unpack_from(BATCH_HDR_FMT, payload)
    run_id       = _guid_words_to_str(unpacked[:8])
    start_offset = unpacked[8]
    count        = unpacked[9]
    expected = BATCH_HDR_SIZE + count * SAMPLE_SIZE
    if len(payload) < expected:
        return
    records = []
    for i in range(count):
        ax, ay, az, gx, gy, gz = struct.unpack_from(SAMPLE_FMT, payload, BATCH_HDR_SIZE + i * SAMPLE_SIZE)
        records.append({"ax": ax, "ay": ay, "az": az, "gx": gx, "gy": gy, "gz": gz})
    with _lock:
        if last_run["runId"] != run_id:
            last_run["runId"]    = run_id
            last_run["records"]  = []
        needed = start_offset + count
        if len(last_run["records"]) < needed:
            last_run["records"].extend([None] * (needed - len(last_run["records"])))
        for i, r in enumerate(records):
            last_run["records"][start_offset + i] = r


_STATE_MAP = {0: "idle", 1: "connected", 2: "acquiring"}


def decode_status(payload: bytes) -> None:
    if len(payload) < STATUS_SIZE:
        return
    uptime_ms, battery_mv, battery_pct, status, sampled_count, total_samples = struct.unpack_from(STATUS_FMT, payload)
    state = _STATE_MAP.get(status, f"unknown({status})")
    entry = {"uptimeMs": uptime_ms, "state": state, "batteryMv": battery_mv, "batteryPct": battery_pct,
             "sampledCount": sampled_count, "totalSamples": total_samples}
    with _lock:
        last_status.update(entry)
        keepalive_log.append(entry)
        if len(keepalive_log) > MAX_KEEPALIVE_LOG:
            keepalive_log.pop(0)


def _register_guid(guid: str):
    with _lock:
        if guid in known_guids:
            known_guids.remove(guid)
        known_guids.insert(0, guid)


def on_connect(client, userdata, flags, rc, properties=None):
    if rc == 0:
        print("[tester] connected to broker")
        _resubscribe(client)
    else:
        print(f"[tester] connection failed: rc={rc}")


def _resubscribe(client):
    with _lock:
        guid = current_guid
    if guid:
        # rt/<guid>/# covers status — no need for the wildcard sub alongside it
        client.unsubscribe("rt/+/status")
        client.subscribe(f"rt/{guid}/#")
        print(f"[tester] subscribed to rt/{guid}/#")
    else:
        # no active device — use wildcard for discovery only
        client.subscribe("rt/+/status")
        print("[tester] subscribed to rt/+/status (discovery)")


def on_message(client, userdata, msg):
    parts = msg.topic.split("/")
    if len(parts) < 3:
        return
    guid = parts[1]
    kind = parts[2]
    _register_guid(guid)
    with _lock:
        active = current_guid
    if guid != active:
        return
    if kind == "data":
        decode_data(bytes(msg.payload))
    elif kind == "status":
        decode_status(bytes(msg.payload))


def mqtt_thread():
    global _mqtt_client
    client = mqtt.Client(mqtt.CallbackAPIVersion.VERSION2)
    client.on_connect = on_connect
    client.on_message = on_message
    _mqtt_client = client
    print(f"[tester] connecting to {BROKER_HOST}:{BROKER_PORT} ...")
    client.connect(BROKER_HOST, BROKER_PORT)
    client.loop_forever()


# --- REST API ---

@app.route("/")
def index():
    return render_template("index.html")


@app.route("/api/data")
def api_data():
    with _lock:
        return jsonify({
            "guid":       current_guid,
            "run":        last_run,
            "status":     last_status,
            "keepalives": list(keepalive_log),
        })


@app.route("/api/guids")
def api_guids():
    with _lock:
        return jsonify({"guids": list(known_guids)})


@app.route("/api/disconnect-guid", methods=["POST"])
def api_disconnect_guid():
    global current_guid, last_run, last_status, keepalive_log
    with _lock:
        current_guid  = None
        last_run      = {"runId": None, "records": []}
        last_status   = {"uptimeMs": 0, "state": "idle", "batteryMv": 0, "batteryPct": 0, "sampledCount": 0, "totalSamples": 0}
        keepalive_log = []
    if _mqtt_client:
        _mqtt_client.unsubscribe("rt/#")
        _resubscribe(_mqtt_client)
    return jsonify({"ok": True})


@app.route("/api/reset", methods=["POST"])
def api_reset():
    global last_run, last_status, keepalive_log
    with _lock:
        last_run      = {"runId": None, "records": []}
        last_status   = {"uptimeMs": 0, "state": "idle", "batteryMv": 0, "batteryPct": 0, "sampledCount": 0, "totalSamples": 0}
        keepalive_log = []
    return jsonify({"ok": True})


@app.route("/api/set-guid", methods=["POST"])
def api_set_guid():
    global current_guid, last_run, last_status, keepalive_log
    guid = request.json.get("guid", "").strip()
    if not guid:
        return jsonify({"error": "guid required"}), 400
    _register_guid(guid)
    with _lock:
        current_guid  = guid
        last_run      = {"runId": None, "records": []}
        last_status   = {"uptimeMs": 0, "state": "idle", "batteryMv": 0, "batteryPct": 0, "sampledCount": 0, "totalSamples": 0}
        keepalive_log = []
    if _mqtt_client:
        _resubscribe(_mqtt_client)
    return jsonify({"ok": True, "guid": guid})


@app.route("/api/send-cmd", methods=["POST"])
def api_send_cmd():
    with _lock:
        guid = current_guid
    if not guid:
        return jsonify({"error": "no guid set"}), 400

    body = request.json
    cmd  = body.get("cmd")
    topic = f"rt/{guid}/cmd"

    if cmd == "connect":
        payload = struct.pack("B", CMD_CONNECT)
    elif cmd == "disconnect":
        payload = struct.pack("B", CMD_DISCONNECT)
    elif cmd == "reset":
        payload = struct.pack("B", CMD_RESET)
    elif cmd == "start_run":
        guid_words  = _parse_guid(body["runId"])
        num_samples = int(body["numSamples"])
        odr         = int(body["odr"])
        accel_range = int(body["accelRange"])
        gyro_range  = int(body["gyroRange"])
        payload = struct.pack("B", CMD_START_RUN) + struct.pack("<8HIBBBB", *guid_words, num_samples, odr, accel_range, gyro_range, 0)
    else:
        return jsonify({"error": "unknown cmd"}), 400

    if _mqtt_client:
        _mqtt_client.publish(topic, payload, qos=1)
        print(f"[tester] published {cmd} to {topic}")
        return jsonify({"ok": True})
    return jsonify({"error": "broker not connected"}), 503


if __name__ == "__main__":
    t = threading.Thread(target=mqtt_thread, daemon=True)
    t.start()
    app.run(host="0.0.0.0", port=5000, use_reloader=False)
