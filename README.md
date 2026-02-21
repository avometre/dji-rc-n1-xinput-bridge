# dji-rc-n1-xinput-bridge

Open-source Windows 10/11 bridge application: reads DJI RC-N1 (RC231) controller data (typically over USB VCOM/serial) and exposes it as a virtual Xbox 360 controller (XInput) via ViGEm.

## English

### What It Is
`rcbridge` ingests raw serial data from RC-N1, decodes it (diagnostic stub for now), applies normalization and filtering, then emits XInput reports to a virtual Xbox 360 controller.

Core pipeline:
`SerialFrameSource -> DjiDecoder (diagnostic stub) -> AxisMapper/Filters -> XInputSink`

MVP status:
- COM port listing
- Raw frame capture to binary file
- Runtime bridge command with config-driven mapping/filtering
- Diagnostic command for COM + ViGEmBus checks

### Requirements
- Windows 10/11
- .NET 8 SDK (for build/test)
- ViGEmBus driver (runtime prerequisite)
- DJI USB/VCOM driver may be required
  - DJI Assistant 2 commonly installs this driver
  - Close DJI Assistant 2 after driver install (it can keep COM port busy)
- Runtime usage does not require admin rights after drivers are installed.

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
dotnet run --project src/RcBridge.App -- capture --port auto --baud 115200 --out captures/session.bin --seconds 20
```

4. Run bridge:
```bash
dotnet run --project src/RcBridge.App -- run --port auto --baud 115200 --config config.json
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

### Troubleshooting
- COM port not showing:
  - check cable/driver in Device Manager
  - ensure DJI Assistant 2 is closed
- ViGEm not installed:
  - install ViGEmBus and run `diagnose` again
- No input:
  - run `capture` first to verify incoming bytes
  - enable `decoder.hexDumpFrames` for frame logs
- Game does not detect controller:
  - restart game
  - check Steam Input/remapping conflicts

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
- Windows 10/11
- .NET 8 SDK
- ViGEmBus sürücüsü
- Gerekirse DJI USB/VCOM sürücüsü

### Hızlı Başlangıç
```bash
dotnet run --project src/RcBridge.App -- list-ports
dotnet run --project src/RcBridge.App -- capture --port auto --baud 115200 --out captures/session.bin --seconds 20
dotnet run --project src/RcBridge.App -- run --port auto --baud 115200 --config config.json
```

### Sorun Giderme
Ayrıntılı belge:
- `docs/guide-tr.md`
- `docs/troubleshooting.md`
- `docs/protocol-notes.md`

### Yasal Uyarı
Bu proje DJI ile bağlantılı değildir.
Sürücü kurulumu ve kullanımı kullanıcı sorumluluğundadır.
