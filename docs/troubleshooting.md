# Troubleshooting

## COM port not showing
- Verify USB cable and reconnect RC-N1.
- Check Device Manager under "Ports (COM & LPT)".
- Install/reinstall DJI USB/VCOM driver.
- Close DJI Assistant 2 after installation.

## Port exists but cannot open
- Another app is using the port (common: DJI Assistant 2).
- Disconnect/reconnect device.
- Retry with the same baud rate used by capture/run.

## ViGEmBus not available
- Install ViGEmBus.
- Reboot if installer requests it.
- Run `rcbridge diagnose` again.

## No input in games
- Use `capture` to verify incoming serial bytes.
- Enable `decoder.hexDumpFrames` and inspect logs in `logs/`.
- Tune `config.json` mapping and thresholds.

## Game does not detect controller
- Restart the game after launching `rcbridge`.
- Check Steam Input/controller remapping conflicts.
- Confirm ViGEm virtual controller is present in Windows game controller panel.

## High jitter or unstable axes
- Increase per-axis `deadzone` slightly.
- Increase per-axis `smoothing`.
- Lower `updateRateHz` if CPU is constrained.
