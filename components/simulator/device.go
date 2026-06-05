package main

import (
	"context"
	"fmt"
	"log/slog"
	"sync"
	"time"

	mqtt "github.com/eclipse/paho.mqtt.golang"
)

type Device struct {
	cfg     DeviceConfig
	guid    GuidWords
	profile Profile

	brokerURL      string
	connectTimeout time.Duration
	sleepDuration  time.Duration

	log *slog.Logger

	// runtime state — only touched from the device goroutine.
	state       SystemState
	startedAt   time.Time
	client      mqtt.Client
	clientID    string
	cmdTopic    string
	statusTopic string
	dataTopic   string
	cmdCh       chan []byte

	// injectedErrors is a static bitmask OR'd into every published errorCode,
	// computed once from cfg.InjectErrors at construction time.
	injectedErrors uint64

	// run state, set on START_RUN, cleared at end.
	runActive   bool
	runId       GuidWords
	runOdrHz    float64
	runTotal    uint32
	runSampled  uint32
	runAccelRng uint8
	runGyroRng  uint8
}

func NewDevice(cfg DeviceConfig, broker BrokerConfig) (*Device, error) {
	g, err := ParseGuid(cfg.Guid)
	if err != nil {
		return nil, err
	}
	prof, err := NewProfile(cfg.Profile.Name, cfg.Profile.Params)
	if err != nil {
		return nil, err
	}
	var injected uint64
	for _, name := range cfg.InjectErrors {
		injected |= errNameToCode[name]
	}
	clientID := fmt.Sprintf("rt-%s", g.String())
	d := &Device{
		cfg:            cfg,
		guid:           g,
		profile:        prof,
		injectedErrors: injected,
		brokerURL:      fmt.Sprintf("tcp://%s:%d", broker.Host, broker.Port),
		connectTimeout: time.Duration(broker.ConnectTimeoutMs) * time.Millisecond,
		sleepDuration:  time.Duration(cfg.SleepSeconds * float64(time.Second)),
		log: slog.With(
			slog.String("guid", g.String()),
			slog.String("profile", cfg.Profile.Name),
		),
		state:       StateIdle,
		startedAt:   time.Now(),
		clientID:    clientID,
		cmdTopic:    fmt.Sprintf("rt/%s/cmd", g.String()),
		statusTopic: fmt.Sprintf("rt/%s/status", g.String()),
		dataTopic:   fmt.Sprintf("rt/%s/data", g.String()),
		cmdCh:       make(chan []byte, 4),
	}
	return d, nil
}

func (d *Device) Run(ctx context.Context, wg *sync.WaitGroup, startupDelay time.Duration) {
	defer wg.Done()
	d.log.Info("device starting", slog.Duration("startup_delay", startupDelay))
	if startupDelay > 0 {
		if !sleepCtx(ctx, startupDelay) {
			return
		}
	}
	statusTick := time.NewTicker(StatusIntervalMs * time.Millisecond)
	defer statusTick.Stop()

	for ctx.Err() == nil {
		if !d.ensureConnected(ctx) {
			// connect failed — back off, then retry. Connection is persistent.
			if !sleepCtx(ctx, d.connectTimeout) {
				break
			}
			continue
		}
		select {
		case <-ctx.Done():
		case payload := <-d.cmdCh:
			d.handleCommand(payload)
			if d.state == StateAcquiring {
				d.runAcquire(ctx)
			}
		case <-statusTick.C:
			d.publishStatus()
		}
	}
	d.disconnect()
	d.log.Info("device stopped")
}

// ensureConnected keeps the persistent MQTT connection up, publishing a status
// on each fresh (re)connect. Returns false only if a connect attempt fails.
func (d *Device) ensureConnected(ctx context.Context) bool {
	if d.client != nil && d.client.IsConnected() {
		return true
	}
	if !d.connect(ctx) {
		return false
	}
	d.publishStatus()
	return true
}

// runAcquire drives a single START_RUN to completion over the already-up
// connection: ~10 evenly-spaced status updates (sampledCount rising = progress),
// then data batches, then back to CONNECTED. No sleep / disconnect — stays live.
func (d *Device) runAcquire(ctx context.Context) {
	d.log.Info("run started",
		slog.Uint64("num_samples", uint64(d.runTotal)),
		slog.Float64("odr_hz", d.runOdrHz),
	)

	odr := d.runOdrHz
	if odr <= 0 {
		odr = 1
	}
	// Total run duration split into ProgressMarks evenly-spaced steps.
	stepDelay := time.Duration(float64(d.runTotal) / odr / float64(ProgressMarks) * float64(time.Second))

	for k := 1; k <= ProgressMarks; k++ {
		if !sleepCtx(ctx, stepDelay) {
			return
		}
		d.runSampled = uint32(uint64(d.runTotal) * uint64(k) / uint64(ProgressMarks))
		d.publishStatus() // status doubles as progress: sampledCount rises across the run
	}

	// Emit data batches. startOffset increments by BatchMax each batch,
	// even when the final batch is short (PROTOCOL.md §6).
	for off := uint32(0); off < d.runTotal; off += BatchMax {
		n := d.runTotal - off
		if n > BatchMax {
			n = BatchMax
		}
		recs := d.profile.SampleBatch(off, int(n), d.runOdrHz)
		hdr := MqttBatchHeader{RunId: d.runId, StartOffset: off, Count: n}
		d.publish(d.dataTopic, EncodeBatch(hdr, recs), 0)
	}
	d.log.Info("run complete", slog.Uint64("samples", uint64(d.runTotal)))

	d.runActive = false
	d.runSampled = 0
	d.runTotal = 0
	d.state = StateConnected
	d.publishStatus()
}

func (d *Device) handleCommand(payload []byte) {
	if len(payload) == 0 {
		return
	}
	op := payload[0]
	switch op {
	case CmdConnect:
		if d.state != StateIdle {
			d.log.Warn("command ignored", slog.String("cmd", "CONNECT"), slog.String("state", d.state.String()))
			return
		}
		d.state = StateConnected
		d.log.Info("CONNECT → CONNECTED")
		d.publishStatus()

	case CmdDisconnect:
		if d.state != StateConnected {
			d.log.Warn("command ignored", slog.String("cmd", "DISCONNECT"), slog.String("state", d.state.String()))
			return
		}
		d.state = StateIdle
		d.log.Info("DISCONNECT → IDLE")
		d.publishStatus()

	case CmdReset:
		if d.state == StateAcquiring {
			d.log.Warn("command ignored", slog.String("cmd", "RESET"), slog.String("state", d.state.String()))
			return
		}
		d.startedAt = time.Now()
		d.state = StateIdle
		d.runActive = false
		d.runSampled = 0
		d.runTotal = 0
		d.log.Info("RESET → IDLE (uptime=0)")
		d.publishStatus()

	case CmdStartRun:
		if d.state != StateConnected {
			d.log.Warn("command ignored", slog.String("cmd", "START_RUN"), slog.String("state", d.state.String()))
			return
		}
		req, err := DecodeStartRun(payload[1:])
		if err != nil {
			d.log.Warn("START_RUN decode failed", slog.String("err", err.Error()))
			return
		}
		d.runId = req.RunId
		d.runTotal = req.NumSamples
		d.runOdrHz = OdrHz(req.Odr)
		d.runAccelRng = req.AccelRange
		d.runGyroRng = req.GyroRange
		d.runSampled = 0
		d.runActive = true
		d.state = StateAcquiring
		d.log.Info("START_RUN → ACQUIRING",
			slog.String("run_id", req.RunId.String()),
			slog.Uint64("num_samples", uint64(req.NumSamples)),
			slog.Float64("odr_hz", d.runOdrHz),
		)

	default:
		d.log.Warn("unknown command opcode", slog.Int("opcode", int(op)))
	}
}

// ---- MQTT plumbing ------------------------------------------------------

func (d *Device) connect(ctx context.Context) bool {
	if d.client != nil && d.client.IsConnected() {
		return true
	}
	opts := mqtt.NewClientOptions().
		AddBroker(d.brokerURL).
		SetClientID(d.clientID).
		SetCleanSession(true).
		SetAutoReconnect(false).
		SetConnectTimeout(d.connectTimeout).
		SetOrderMatters(false)
	d.client = mqtt.NewClient(opts)
	t := d.client.Connect()
	if !t.WaitTimeout(d.connectTimeout) || t.Error() != nil {
		d.log.Warn("mqtt connect failed", slog.Any("err", t.Error()))
		d.client = nil
		return false
	}
	st := d.client.Subscribe(d.cmdTopic, 1, func(_ mqtt.Client, m mqtt.Message) {
		buf := make([]byte, len(m.Payload()))
		copy(buf, m.Payload())
		select {
		case d.cmdCh <- buf:
		default:
			d.log.Warn("cmd channel full, dropping payload")
		}
	})
	if !st.WaitTimeout(d.connectTimeout) || st.Error() != nil {
		d.log.Warn("mqtt subscribe failed", slog.Any("err", st.Error()))
		d.client.Disconnect(0)
		d.client = nil
		return false
	}
	return true
}

func (d *Device) disconnect() {
	if d.client != nil && d.client.IsConnected() {
		d.client.Disconnect(100)
	}
	d.client = nil
}

func (d *Device) publish(topic string, payload []byte, qos byte) {
	if d.client == nil || !d.client.IsConnected() {
		return
	}
	t := d.client.Publish(topic, qos, false, payload)
	t.WaitTimeout(2 * time.Second)
	if err := t.Error(); err != nil {
		d.log.Warn("mqtt publish failed", slog.String("topic", topic), slog.Any("err", err))
	}
}

func (d *Device) publishStatus() {
	mv := d.cfg.BatteryMv
	pct := uint8(255)
	if mv != 65535 {
		f := (float64(mv) - 3000) / 1200 * 100
		if f < 0 {
			f = 0
		}
		if f > 100 {
			f = 100
		}
		pct = uint8(f)
	}
	var errorCode uint64
	if mv != 65535 && mv < VbatCriticalMv {
		errorCode |= ErrBatteryCritical
	}
	errorCode |= d.injectedErrors
	s := MqttStatus{
		UptimeMs:     uint32(time.Since(d.startedAt).Milliseconds()),
		BatteryMv:    mv,
		BatteryPct:   pct,
		Status:       uint8(d.state),
		SampledCount: d.runSampled,
		TotalSamples: d.runTotal,
		ErrorCode:    errorCode,
	}
	d.publish(d.statusTopic, EncodeStatus(s), 0)
}

// sleepCtx blocks for d or until ctx cancels. Returns false if ctx canceled.
func sleepCtx(ctx context.Context, d time.Duration) bool {
	if d <= 0 {
		return ctx.Err() == nil
	}
	t := time.NewTimer(d)
	defer t.Stop()
	select {
	case <-t.C:
		return true
	case <-ctx.Done():
		return false
	}
}
