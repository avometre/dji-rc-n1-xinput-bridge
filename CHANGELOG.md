# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]
### Added
- Initial repository scaffold with .NET 8 solution and clean architecture projects.
- CLI commands: `list-ports`, `capture`, `run`, `diagnose`.
- Diagnostic DJI decoder stub and raw capture writer.
- ViGEm XInput output sink with graceful diagnostics.
- Typed config loading/validation and axis/button mapping pipeline.
- Unit tests for deadzone/expo/mapping/config validation.
- Capture replay and inspect commands for hardwareless protocol analysis.
- Capture format v2 with metadata header (`port`, `baudRate`, `note`) and v1 reader compatibility.
- Experimental protocol decoder path: length-prefixed frame extraction + packed 11-bit channel decode with diagnostic fallback.
- Optional protocol checksum strategy (`none` / `xor8-tail`) with configurable header inclusion and protocol-reject reason logging.
- Capture decode-preview inspection with per-channel activity stats and button/switch candidate hints.
- `run` command mode selection: `--mode xinput|dry-run` (cross-platform dry-run support for Linux/macOS).
- CI expanded to Windows + Linux matrix with CLI smoke checks (`list-ports`, `diagnose`).
- Linux virtual gamepad output mode (`--mode linux-uinput`) backed by `/dev/uinput` with auto mode selection.
- Auto port resolution improved with Linux serial heuristics (`/dev/ttyACM*`, `/dev/ttyUSB*`).
- GitHub templates, CI workflow, and release workflow with checksums.
