package main

import (
	"fmt"
	"os"

	"gopkg.in/yaml.v3"
)

type Config struct {
	Broker              BrokerConfig   `yaml:"broker"`
	DefaultSleepSeconds float64        `yaml:"default_sleep_seconds"`
	Devices             []DeviceConfig `yaml:"devices"`
}

type BrokerConfig struct {
	Host             string `yaml:"host"`
	Port             int    `yaml:"port"`
	ConnectTimeoutMs int    `yaml:"connect_timeout_ms"`
}

type DeviceConfig struct {
	Guid         string        `yaml:"guid"`
	BatteryMv    uint16        `yaml:"battery_mv"`
	SleepSeconds float64       `yaml:"sleep_seconds"` // overrides Config.DefaultSleepSeconds
	Profile      ProfileConfig `yaml:"profile"`
	// InjectErrors lists firmware error-code names to permanently assert in every status publish.
	// Useful for testing how the front-end displays named faults.
	// Valid names mirror ErrorCodeValue_t in src/config.h (e.g. "IMU_READ_ERROR").
	InjectErrors []string `yaml:"inject_errors"`
}

type ProfileConfig struct {
	Name   string         `yaml:"name"`
	Params map[string]any `yaml:"params"`
}

func LoadConfig(path string) (*Config, error) {
	raw, err := os.ReadFile(path)
	if err != nil {
		return nil, fmt.Errorf("read config %s: %w", path, err)
	}
	var c Config
	if err := yaml.Unmarshal(raw, &c); err != nil {
		return nil, fmt.Errorf("parse config %s: %w", path, err)
	}
	if c.Broker.Host == "" {
		c.Broker.Host = "mosquitto"
	}
	if c.Broker.Port == 0 {
		c.Broker.Port = 1883
	}
	if c.Broker.ConnectTimeoutMs == 0 {
		c.Broker.ConnectTimeoutMs = 5000
	}
	if c.DefaultSleepSeconds <= 0 {
		c.DefaultSleepSeconds = float64(SleepBetweenMs) / 1000
	}
	if len(c.Devices) == 0 {
		return nil, fmt.Errorf("config: no devices declared")
	}
	seen := make(map[string]bool, len(c.Devices))
	for i, d := range c.Devices {
		if _, err := ParseGuid(d.Guid); err != nil {
			return nil, fmt.Errorf("devices[%d]: %w", i, err)
		}
		if seen[d.Guid] {
			return nil, fmt.Errorf("devices[%d]: duplicate guid %s", i, d.Guid)
		}
		seen[d.Guid] = true
		if _, err := NewProfile(d.Profile.Name, d.Profile.Params); err != nil {
			return nil, fmt.Errorf("devices[%d] (%s): %w", i, d.Guid, err)
		}
		for _, name := range d.InjectErrors {
			if _, ok := errNameToCode[name]; !ok {
				return nil, fmt.Errorf("devices[%d] (%s): unknown inject_errors name %q", i, d.Guid, name)
			}
		}
		if d.BatteryMv == 0 {
			c.Devices[i].BatteryMv = 4100
		}
		if d.SleepSeconds <= 0 {
			c.Devices[i].SleepSeconds = c.DefaultSleepSeconds
		}
	}
	return &c, nil
}
