# Protocol Notes (Work in Progress)

This project intentionally starts with a diagnostic decoder stub and does not claim full RC-N1 protocol compatibility yet.

## Current decoder behavior
- Consumes raw serial frames from `System.IO.Ports`.
- Attempts a length-prefixed frame extraction (`sync + payloadLength + payload`) with configurable sync byte.
- Attempts packed 11-bit channel decode from extracted payloads (experimental).
- Optionally logs per-frame hex dump.
- Falls back to diagnostic byte-map (`byte -> [-1..1]`) when protocol decode is not available.
- Supports offline replay from capture files (`replay --capture ... --mode dry-run`).
- Supports offline statistical inspection (`inspect --capture ...`) for frame-size, byte-frequency, and correlation hints.

This is enough to validate end-to-end bridge behavior and tune filtering/mapping while reverse engineering proceeds.

## Capture format (`capture` command)
Current default (`v2`, metadata-enabled):
- file header:
  - ASCII magic: `RCB2` (4 bytes)
  - metadata length: `int32` little-endian
  - metadata JSON (UTF-8), fields include:
    - `formatVersion`
    - `createdUtc`
    - `port`
    - `baudRate`
    - `note`
    - `tool`
- followed by frame records:
  - `int64` UTC ticks (little-endian)
  - `int32` payload length (little-endian)
  - payload bytes

Legacy compatibility (`v1`):
- no file header
- only frame records (`int64 ticks + int32 length + payload`)
- reader remains backward-compatible with v1 captures.

## What we still need for full protocol decode
- Stable frame boundary detection.
- Channel packing format (bit-level mapping).
- Button/switch layout and scaling calibration.
- Failsafe/invalid frame handling strategy.

## How to contribute captures
1. Run:
```bash
rcbridge capture --port auto --baud 115200 --out captures/my-session.bin --seconds 20 --note "yaw sweep"
```
2. Include context in issue/PR:
- RC-N1 hardware revision (if known)
- OS version
- Port + baud used
- Which stick/switch motion happened at which approximate second
3. Attach capture file and optional log file from `logs/`.

## Reverse engineering guidance
- Try deterministic movement patterns (single-axis sweeps, trigger-only actions).
- Keep each session short and labeled.
- Compare multiple captures for repeated byte offsets and ranges.
