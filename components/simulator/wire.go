package main

import (
	"encoding/binary"
	"fmt"
	"math"
)

// All struct sizes per PROTOCOL.md §10.
const (
	StatusSize      = 24
	StartRunSize    = 24
	BatchHeaderSize = 24
	SampleSize      = 24
)

type MqttStatus struct {
	UptimeMs     uint32
	BatteryMv    uint16
	BatteryPct   uint8
	Status       uint8
	SampledCount uint32
	TotalSamples uint32
	ErrorCode    uint64
}

type MqttCmdStartRun struct {
	RunId      GuidWords
	NumSamples uint32
	Odr        uint8
	AccelRange uint8
	GyroRange  uint8
	Reserved   uint8
}

type MqttBatchHeader struct {
	RunId       GuidWords
	StartOffset uint32
	Count       uint32
}

type SampleRecord struct {
	Ax, Ay, Az float32
	Gx, Gy, Gz float32
}

func EncodeStatus(s MqttStatus) []byte {
	b := make([]byte, StatusSize)
	binary.LittleEndian.PutUint32(b[0:4], s.UptimeMs)
	binary.LittleEndian.PutUint16(b[4:6], s.BatteryMv)
	b[6] = s.BatteryPct
	b[7] = s.Status
	binary.LittleEndian.PutUint32(b[8:12], s.SampledCount)
	binary.LittleEndian.PutUint32(b[12:16], s.TotalSamples)
	binary.LittleEndian.PutUint64(b[16:24], s.ErrorCode)
	return b
}

func encodeGuid(b []byte, g GuidWords) {
	for i := 0; i < 8; i++ {
		binary.LittleEndian.PutUint16(b[i*2:i*2+2], g[i])
	}
}

func EncodeBatch(hdr MqttBatchHeader, recs []SampleRecord) []byte {
	out := make([]byte, BatchHeaderSize+len(recs)*SampleSize)
	encodeGuid(out[0:16], hdr.RunId)
	binary.LittleEndian.PutUint32(out[16:20], hdr.StartOffset)
	binary.LittleEndian.PutUint32(out[20:24], hdr.Count)
	for i, r := range recs {
		off := BatchHeaderSize + i*SampleSize
		binary.LittleEndian.PutUint32(out[off+0:off+4], math.Float32bits(r.Ax))
		binary.LittleEndian.PutUint32(out[off+4:off+8], math.Float32bits(r.Ay))
		binary.LittleEndian.PutUint32(out[off+8:off+12], math.Float32bits(r.Az))
		binary.LittleEndian.PutUint32(out[off+12:off+16], math.Float32bits(r.Gx))
		binary.LittleEndian.PutUint32(out[off+16:off+20], math.Float32bits(r.Gy))
		binary.LittleEndian.PutUint32(out[off+20:off+24], math.Float32bits(r.Gz))
	}
	return out
}

// DecodeStartRun parses the 24-byte payload that follows the 0x02 command
// byte. Caller is responsible for stripping the leading opcode.
func DecodeStartRun(p []byte) (MqttCmdStartRun, error) {
	var r MqttCmdStartRun
	if len(p) < StartRunSize {
		return r, fmt.Errorf("start_run: payload too short (%d < %d)", len(p), StartRunSize)
	}
	for i := 0; i < 8; i++ {
		r.RunId[i] = binary.LittleEndian.Uint16(p[i*2 : i*2+2])
	}
	r.NumSamples = binary.LittleEndian.Uint32(p[16:20])
	r.Odr = p[20]
	r.AccelRange = p[21]
	r.GyroRange = p[22]
	r.Reserved = p[23]
	return r, nil
}

// OdrHz converts the wire-level ODR code (PROTOCOL.md §4.2) to its Hz value.
// Unknown codes fall back to the 104 Hz default the firmware ships with.
func OdrHz(code uint8) float64 {
	switch code {
	case 0x01:
		return 12.5
	case 0x02:
		return 26
	case 0x03:
		return 52
	case 0x04:
		return 104
	case 0x05:
		return 208
	case 0x06:
		return 417
	case 0x07:
		return 833
	default:
		return 104
	}
}
