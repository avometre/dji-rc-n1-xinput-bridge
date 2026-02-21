# dji-rc-n1-xinput-bridge

Open-source RC-N1 bridge application: reads DJI RC-N1 (RC231) controller data (typically over USB VCOM/serial). On Windows it exposes a virtual Xbox 360 controller (XInput) via ViGEm; on Linux it can expose a virtual gamepad via `uinput`, and on all platforms it supports dry-run diagnostics.

## English

### What It Is
`rcbridge` ingests raw serial data from RC-N1, attempts frame extraction + packed channel decode (experimental), applies optional checksum validation, applies normalization/filtering, then emits XInput reports to a virtual Xbox 360 controller.

Core pipeline:
`SerialFrameSource -> DjiDecoder (framing attempt + diagnostic fallback) -> AxisMapper/Filters -> XInputSink`

MVP status:
- COM port listing
- Raw frame capture to binary file
- Runtime bridge command with config-driven mapping/filtering
- Diagnostic command for COM + ViGEmBus checks

### Requirements
- .NET 8 SDK (build/test on Windows/Linux)
- Runtime output modes:
  - `--mode xinput`: Windows 10/11 + ViGEmBus
  - `--mode linux-uinput`: Linux + `/dev/uinput` access
  - `--mode dry-run`: Windows/Linux/macOS (no virtual gamepad output)
  - `--mode auto`: Windows->`xinput`, Linux->`linux-uinput` (or fallback `dry-run`)
- DJI USB/VCOM driver may be required (especially on Windows)
  - DJI Assistant 2 commonly installs this driver
  - Close DJI Assistant 2 after driver install (it can keep COM port busy)
- Runtime usage does not require admin rights after drivers are installed (except driver installation itself).

### Quickstart
1. Build and test:
```bash
git clone https://github.com/avometre/dji-rc-n1-xinput-bridge.git
cd dji-rc-n1-xinput-bridge
dotnet restore RcBridge.sln
dotnet build RcBridge.sln -c Debug
dotnet test RcBridge.sln -c Debug
```

2. List ports:
```bash
dotnet run --project src/RcBridge.App -- list-ports
```

3. Capture frames:
```bash
dotnet run --project src/RcBridge.App -- capture --port auto --baud 115200 --out captures/session.bin --seconds 20 --note "yaw sweep"
```

4. Run bridge:
```bash
dotnet run --project src/RcBridge.App -- run --port auto --baud 115200 --config config.json --mode auto
```

Linux/macOS pipeline test (no ViGEm):
```bash
dotnet run --project src/RcBridge.App -- run --port auto --baud 115200 --config config.json --mode dry-run
```

Linux virtual gamepad output:
```bash
sudo modprobe uinput
dotnet run --project src/RcBridge.App -- run --port auto --baud 115200 --config config.json --mode linux-uinput
```

`--port auto` tries to detect DJI VCOM port by friendly name.
If multiple candidates exist or no DJI match is found, use explicit `--port COMx`.

5. Diagnose environment:
```bash
dotnet run --project src/RcBridge.App -- diagnose
```

6. Replay a capture without hardware:
```bash
dotnet run --project src/RcBridge.App -- replay --capture captures/session.bin --config config.json --mode dry-run
```

7. Inspect a capture for reverse-engineering hints:
```bash
dotnet run --project src/RcBridge.App -- inspect --capture captures/session.bin
```

8. Decode-preview a capture to get channel activity + button/switch candidates:
```bash
dotnet run --project src/RcBridge.App -- inspect --capture captures/session.bin --decode-preview --config config.json
```

### Troubleshooting
- COM port not showing:
  - check cable/driver in Device Manager
  - ensure DJI Assistant 2 is closed
- ViGEm not installed:
  - install ViGEmBus and run `diagnose` again
- No input:
  - run `capture` first to verify incoming bytes
  - enable `decoder.hexDumpFrames` for frame logs
  - if protocol decode is unstable, keep `decoder.checksumMode` as `none` until captures confirm checksum model
- Game does not detect controller:
  - restart game
  - check Steam Input/remapping conflicts
- Linux/macOS user:
  - `--mode xinput` is not available
  - on Linux prefer `--mode linux-uinput` (requires `/dev/uinput` write access)
  - otherwise use `capture`, `inspect`, `replay --mode dry-run`, `run --mode dry-run`

Detailed docs:
- `docs/troubleshooting.md`
- `docs/protocol-notes.md`

### Safety / Legal
This project is not affiliated with DJI.
Users install third-party drivers at their own risk.
No DJI proprietary binaries are distributed in this repository.

---

## Türkçe

### Proje Özeti
`rcbridge`, RC-N1 kumandasından seri veriyi alır, normalize/filter uygular ve ViGEm üzerinden sanal Xbox 360 gamepad olarak oyuna verir.

### Gereksinimler
- .NET 8 SDK (Windows/Linux geliştirme ve test)
- Çalışma modları:
  - `--mode xinput`: Windows 10/11 + ViGEmBus
  - `--mode linux-uinput`: Linux + `/dev/uinput`
  - `--mode dry-run`: Windows/Linux/macOS (sanal gamepad üretmez)
  - `--mode auto`: Windows'ta `xinput`, Linux'ta `linux-uinput` (mümkün değilse `dry-run`)
- Gerekirse DJI USB/VCOM sürücüsü

### Hızlı Başlangıç
```bash
dotnet run --project src/RcBridge.App -- list-ports
dotnet run --project src/RcBridge.App -- capture --port auto --baud 115200 --out captures/session.bin --seconds 20 --note "roll sweep"
dotnet run --project src/RcBridge.App -- run --port auto --baud 115200 --config config.json --mode auto
dotnet run --project src/RcBridge.App -- run --port auto --baud 115200 --config config.json --mode linux-uinput
dotnet run --project src/RcBridge.App -- run --port auto --baud 115200 --config config.json --mode dry-run
```

### Sorun Giderme
Ayrıntılı belge:
- `docs/guide-tr.md`
- `docs/troubleshooting.md`
- `docs/protocol-notes.md`

### Yasal Uyarı
Bu proje DJI ile bağlantılı değildir.
Sürücü kurulumu ve kullanımı kullanıcı sorumluluğundadır.
