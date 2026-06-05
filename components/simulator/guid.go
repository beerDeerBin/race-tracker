package main

import (
	"fmt"
	"strconv"
	"strings"
)

// GuidWords is the wire-level GUID representation: 8 little-endian uint16
// words. Mirrors the layout of `Guid_t` in the firmware and the `<8H>`
// struct in the tester.
type GuidWords [8]uint16

// String renders the GUID as the canonical uppercase UUID format used by the
// tester (`_guid_words_to_str`, race-tracker-tester/main.py:36).
//
// Layout: words 0+1 form the first 8-char group, then words 2, 3, 4 form the
// three 4-char groups, and words 5+6+7 form the trailing 12-char group.
func (g GuidWords) String() string {
	return fmt.Sprintf("%04X%04X-%04X-%04X-%04X-%04X%04X%04X",
		g[0], g[1], g[2], g[3], g[4], g[5], g[6], g[7])
}

func ParseGuid(s string) (GuidWords, error) {
	h := strings.ReplaceAll(s, "-", "")
	if len(h) != 32 {
		return GuidWords{}, fmt.Errorf("guid %q: expected 32 hex chars, got %d", s, len(h))
	}
	var g GuidWords
	for i := 0; i < 8; i++ {
		v, err := strconv.ParseUint(h[i*4:i*4+4], 16, 16)
		if err != nil {
			return GuidWords{}, fmt.Errorf("guid %q: word %d: %w", s, i, err)
		}
		g[i] = uint16(v)
	}
	return g, nil
}
