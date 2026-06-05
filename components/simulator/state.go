package main

type SystemState uint8

const (
	StateIdle      SystemState = 0
	StateConnected SystemState = 1
	StateAcquiring SystemState = 2
)

func (s SystemState) String() string {
	switch s {
	case StateIdle:
		return "idle"
	case StateConnected:
		return "connected"
	case StateAcquiring:
		return "acquiring"
	default:
		return "unknown"
	}
}

// Command opcodes (PROTOCOL.md §4 / §10).
const (
	CmdNone       uint8 = 0x00
	CmdConnect    uint8 = 0x01
	CmdStartRun   uint8 = 0x02
	CmdDisconnect uint8 = 0x03
	CmdReset      uint8 = 0x04
)

// MQTT timing / behaviour constants (PROTOCOL.md §10).
const (
	SleepBetweenMs   = 30000 // startup-stagger spacing only (device no longer sleeps)
	StatusIntervalMs = 10000 // idle/connected keepalive cadence
	ProgressMarks    = 10    // evenly-spaced progress updates per run
	BatchMax         = 32
)

// errorCode bits (ErrorCodeValue_t, src/config.h) — full set mirrored here so the
// simulator can surface any named fault via inject_errors in config.yaml.
const (
	// EEPROM
	ErrEepromParameter uint64 = 1 << 0
	ErrEepromInit      uint64 = 1 << 1
	ErrEepromWrite     uint64 = 1 << 2
	// WiFi
	ErrWifiInit     uint64 = 1 << 8
	ErrWifiConnect  uint64 = 1 << 9
	ErrWifiShutdown uint64 = 1 << 10
	ErrWifiWakeup   uint64 = 1 << 11
	ErrWifiSleep    uint64 = 1 << 12
	// Data manager
	ErrDamgrInit     uint64 = 1 << 16
	ErrDamgrAlloc    uint64 = 1 << 17
	ErrDamgrOverflow uint64 = 1 << 18
	// MQTT
	ErrMqttConnect   uint64 = 1 << 24
	ErrMqttPublish   uint64 = 1 << 25
	ErrMqttSubscribe uint64 = 1 << 26
	// IMU
	ErrImuInit   uint64 = 1 << 32
	ErrImuConfig uint64 = 1 << 33
	ErrImuRead   uint64 = 1 << 34
	ErrImuFifo   uint64 = 1 << 35
	// Power
	ErrPwrInit         uint64 = 1 << 40
	ErrPwrAdc          uint64 = 1 << 41
	ErrBatteryCritical uint64 = 1 << 42

	VbatCriticalMv uint16 = 3100
)

// errNameToCode maps the canonical firmware constant names (used in inject_errors config)
// to their bit values so the YAML config can reference errors by name.
var errNameToCode = map[string]uint64{
	"EEPROM_PARAMETER_ERROR":      ErrEepromParameter,
	"EEPROM_INIT_ERROR":           ErrEepromInit,
	"EEPROM_WRITE_ERROR":          ErrEepromWrite,
	"WIFI_INIT_ERROR":             ErrWifiInit,
	"WIFI_CONNECT_ERROR":          ErrWifiConnect,
	"WIFI_SHUTDOWN_ERROR":         ErrWifiShutdown,
	"WIFI_WAKEUP_ERROR":           ErrWifiWakeup,
	"WIFI_SLEEP_ERROR":            ErrWifiSleep,
	"DAMGR_INIT_ERROR":            ErrDamgrInit,
	"DAMGR_ALLOC_ERROR":           ErrDamgrAlloc,
	"DAMGR_OVERFLOW_ERROR":        ErrDamgrOverflow,
	"MQTT_CONNECT_ERROR":          ErrMqttConnect,
	"MQTT_PUBLISH_ERROR":          ErrMqttPublish,
	"MQTT_SUBSCRIBE_ERROR":        ErrMqttSubscribe,
	"IMU_INIT_ERROR":              ErrImuInit,
	"IMU_CONFIG_ERROR":            ErrImuConfig,
	"IMU_READ_ERROR":              ErrImuRead,
	"IMU_FIFO_ERROR":              ErrImuFifo,
	"PWR_INIT_ERROR":              ErrPwrInit,
	"PWR_ADC_ERROR":               ErrPwrAdc,
	"PWR_BATTERY_CRITICAL_ERROR":  ErrBatteryCritical,
}
