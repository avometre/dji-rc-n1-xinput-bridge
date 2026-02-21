# Contributing

## Development setup
```bash
dotnet restore RcBridge.sln
dotnet tool restore
dotnet tool run dotnet-format -- RcBridge.sln --check --verbosity minimal
dotnet build RcBridge.sln -c Debug
dotnet test RcBridge.sln -c Debug
```

## Running locally
```bash
dotnet run --project src/RcBridge.App -- list-ports
dotnet run --project src/RcBridge.App -- diagnose
dotnet run --project src/RcBridge.App -- capture --port auto --baud 115200 --out captures/session.bin --seconds 20 --note "test sweep"
dotnet run --project src/RcBridge.App -- inspect --capture captures/session.bin
dotnet run --project src/RcBridge.App -- run --port auto --baud 115200 --config config.json
dotnet run --project src/RcBridge.App -- replay --capture captures/session.bin --config config.json --mode dry-run
```

## Coding conventions
- Keep architecture boundaries clear (`Core` vs IO adapters).
- Prefer small, testable classes.
- Add unit tests for math/mapping/config changes.
- Keep nullable warnings at zero.
- Avoid introducing DJI proprietary binaries.

## Submitting protocol captures
Open an issue and attach:
- capture file (`captures/*.bin`)
- RC-N1 revision (if known)
- Windows version
- movement notes (what was moved and when)

## Pull requests
- Keep PRs focused and small.
- Update docs for user-facing changes.
- Ensure CI passes on Windows.
