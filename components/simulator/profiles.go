package main

import (
	"fmt"
	"math"
)

const gravityMps2 = 9.81

// Profile generates a contiguous block of samples for a run. startIdx is the
// global sample index of the first record returned, count is how many records
// to produce, and odrHz is the run's sampling rate.
type Profile interface {
	SampleBatch(startIdx uint32, count int, odrHz float64) []SampleRecord
}

type ProfileFactory func(params map[string]any) (Profile, error)

var profileRegistry = map[string]ProfileFactory{
	"stationary": newStationary,
	"circle":     newCircle,
	"straight":   newStraight,
	"track":      newTrack,
}

func NewProfile(name string, params map[string]any) (Profile, error) {
	f, ok := profileRegistry[name]
	if !ok {
		return nil, fmt.Errorf("unknown profile %q", name)
	}
	return f(params)
}

// getFloat coerces a YAML-decoded value (int or float64) to float64.
func getFloat(params map[string]any, key string, def float64) float64 {
	if params == nil {
		return def
	}
	v, ok := params[key]
	if !ok {
		return def
	}
	switch n := v.(type) {
	case float64:
		return n
	case float32:
		return float64(n)
	case int:
		return float64(n)
	case int64:
		return float64(n)
	case uint64:
		return float64(n)
	}
	return def
}

func getInt(params map[string]any, key string, def int) int {
	return int(getFloat(params, key, float64(def)))
}

// ---- stationary ---------------------------------------------------------

type stationary struct{}

func newStationary(map[string]any) (Profile, error) { return &stationary{}, nil }

func (p *stationary) SampleBatch(_ uint32, count int, _ float64) []SampleRecord {
	out := make([]SampleRecord, count)
	for i := range out {
		out[i].Az = gravityMps2
	}
	return out
}

// ---- circle -------------------------------------------------------------

type circle struct {
	radiusM  float64
	speedMps float64
}

func newCircle(params map[string]any) (Profile, error) {
	c := &circle{
		radiusM:  getFloat(params, "radius_m", 5),
		speedMps: getFloat(params, "speed_mps", 8),
	}
	if c.radiusM <= 0 {
		return nil, fmt.Errorf("circle.radius_m must be > 0")
	}
	return c, nil
}

func (p *circle) SampleBatch(_ uint32, count int, _ float64) []SampleRecord {
	ax := float32(p.speedMps * p.speedMps / p.radiusM) // centripetal
	gz := float32(p.speedMps / p.radiusM)              // yaw rate
	out := make([]SampleRecord, count)
	for i := range out {
		out[i] = SampleRecord{Ax: ax, Az: gravityMps2, Gz: gz}
	}
	return out
}

// ---- straight -----------------------------------------------------------

type straight struct {
	cruiseAccel float64
	bumpPeriod  float64
	bumpAccel   float64
	bumpWidth   float64
}

func newStraight(params map[string]any) (Profile, error) {
	s := &straight{
		cruiseAccel: getFloat(params, "cruise_accel_mps2", 0.2),
		bumpPeriod:  getFloat(params, "bump_period_s", 5),
		bumpAccel:   getFloat(params, "bump_accel_mps2", 3),
		bumpWidth:   getFloat(params, "bump_width_s", 0.1),
	}
	if s.bumpPeriod <= 0 {
		s.bumpPeriod = 1
	}
	return s, nil
}

func (p *straight) SampleBatch(startIdx uint32, count int, odrHz float64) []SampleRecord {
	out := make([]SampleRecord, count)
	if odrHz <= 0 {
		odrHz = 1
	}
	for i := 0; i < count; i++ {
		t := float64(startIdx+uint32(i)) / odrHz
		phase := math.Mod(t, p.bumpPeriod)
		ax := p.cruiseAccel
		if phase < p.bumpWidth {
			ax += p.bumpAccel
		}
		out[i] = SampleRecord{Ax: float32(ax), Az: gravityMps2}
	}
	return out
}

// ---- track --------------------------------------------------------------
//
// A lap is four equal-time sectors: straight, curve, straight, curve. Speed
// is derived per sector so the math stays stateless / reproducible from
// (startIdx, odrHz).

type track struct {
	lapTime          float64
	lapCount         int
	straightFraction float64 // 0..1, share of lap time spent on the two straights combined
	radiusM          float64
}

func newTrack(params map[string]any) (Profile, error) {
	t := &track{
		lapTime:          getFloat(params, "lap_time_s", 30),
		lapCount:         getInt(params, "lap_count", 5),
		straightFraction: getFloat(params, "straight_fraction", 0.4),
		radiusM:          getFloat(params, "radius_m", 8),
	}
	if t.lapTime <= 0 || t.radiusM <= 0 {
		return nil, fmt.Errorf("track.lap_time_s and track.radius_m must be > 0")
	}
	if t.straightFraction <= 0 || t.straightFraction >= 1 {
		return nil, fmt.Errorf("track.straight_fraction must be in (0,1)")
	}
	return t, nil
}

func (p *track) SampleBatch(startIdx uint32, count int, odrHz float64) []SampleRecord {
	if odrHz <= 0 {
		odrHz = 1
	}
	totalRun := p.lapTime * float64(p.lapCount)

	// straight time per straight section, curve time per curve section
	straightSec := p.straightFraction * p.lapTime / 2
	curveSec := (1 - p.straightFraction) * p.lapTime / 2
	curveCircumference := math.Pi * p.radiusM // half-circle per curve sector
	curveSpeed := curveCircumference / curveSec
	curveYaw := math.Pi / curveSec // π rad over the sector

	out := make([]SampleRecord, count)
	for i := 0; i < count; i++ {
		gIdx := startIdx + uint32(i)
		t := float64(gIdx) / odrHz
		if t >= totalRun {
			// past the configured laps — stationary tail
			out[i] = SampleRecord{Az: gravityMps2}
			continue
		}
		lt := math.Mod(t, p.lapTime)
		switch {
		case lt < straightSec:
			out[i] = SampleRecord{Ax: 0.3, Az: gravityMps2}
		case lt < straightSec+curveSec:
			ax := curveSpeed * curveSpeed / p.radiusM
			out[i] = SampleRecord{Ax: float32(ax), Az: gravityMps2, Gz: float32(curveYaw)}
		case lt < 2*straightSec+curveSec:
			out[i] = SampleRecord{Ax: 0.3, Az: gravityMps2}
		default:
			ax := curveSpeed * curveSpeed / p.radiusM
			out[i] = SampleRecord{Ax: float32(ax), Az: gravityMps2, Gz: float32(-curveYaw)}
		}
	}
	return out
}
