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
- GitHub templates, CI workflow, and release workflow with checksums.
