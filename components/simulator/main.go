package main

import (
	"context"
	"log/slog"
	"os"
	"os/signal"
	"sync"
	"syscall"
	"time"
)

func main() {
	logger := slog.New(slog.NewJSONHandler(os.Stdout, &slog.HandlerOptions{Level: slog.LevelInfo}))
	slog.SetDefault(logger)

	cfgPath := os.Getenv("CONFIG_PATH")
	if cfgPath == "" {
		cfgPath = "/app/config.yaml"
	}
	if _, err := os.Stat(cfgPath); os.IsNotExist(err) {
		fallback := "/app/config.example.yaml"
		if _, ferr := os.Stat(fallback); ferr == nil {
			slog.Warn("config not found, using bundled example", slog.String("requested", cfgPath), slog.String("fallback", fallback))
			cfgPath = fallback
		}
	}

	cfg, err := LoadConfig(cfgPath)
	if err != nil {
		slog.Error("config load failed", slog.String("err", err.Error()))
		os.Exit(1)
	}
	slog.Info("config loaded",
		slog.String("path", cfgPath),
		slog.String("broker", cfg.Broker.Host),
		slog.Int("port", cfg.Broker.Port),
		slog.Int("devices", len(cfg.Devices)),
	)

	ctx, cancel := context.WithCancel(context.Background())
	defer cancel()
	sigCh := make(chan os.Signal, 1)
	signal.Notify(sigCh, syscall.SIGINT, syscall.SIGTERM)
	go func() {
		s := <-sigCh
		slog.Info("signal received, shutting down", slog.String("sig", s.String()))
		cancel()
	}()

	var wg sync.WaitGroup
	// Spread initial wake-ups across the configured default sleep so N
	// devices don't connect-storm the broker.
	jitterStep := time.Duration(cfg.DefaultSleepSeconds*float64(time.Second)) / time.Duration(max(len(cfg.Devices), 1))
	for i, dc := range cfg.Devices {
		dev, err := NewDevice(dc, cfg.Broker)
		if err != nil {
			slog.Error("device init failed", slog.Int("index", i), slog.String("err", err.Error()))
			continue
		}
		wg.Add(1)
		go dev.Run(ctx, &wg, time.Duration(i)*jitterStep)
	}
	wg.Wait()
	slog.Info("all devices stopped")
}
