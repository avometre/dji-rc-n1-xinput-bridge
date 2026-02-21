# DJI RC-N1 XInput Köprüsü: Türkçe Kurulum ve Kullanım Rehberi

## 1. Amaç
Bu rehber, RC-N1 (RC231) kumandadan gelen veriyi Windows'ta sanal Xbox 360 gamepad olarak kullanmak için adım adım kurulum anlatır.

## 2. Ön Koşullar
- Windows 10/11
- .NET 8 SDK
- ViGEmBus sürücüsü
- DJI USB/VCOM sürücüsü (gerektiğinde)

## 3. Sürücü Kurulumu
1. ViGEmBus kurulumunu tamamlayın.
2. RC-N1 için VCOM sürücüsü yoksa DJI Assistant 2 ile kurun.
3. Kurulum sonrası DJI Assistant 2'yi kapatın.

[EKRAN GÖRÜNTÜSÜ YER TUTUCU - ViGEmBus installer]
[EKRAN GÖRÜNTÜSÜ YER TUTUCU - Device Manager COM port]

## 4. Projeyi Derleme
```bash
git clone https://github.com/avometre/dji-rc-n1-xinput-bridge.git
cd dji-rc-n1-xinput-bridge
dotnet restore RcBridge.sln
dotnet build RcBridge.sln -c Release
dotnet test RcBridge.sln -c Release
```

## 5. COM Port Tespiti
```bash
dotnet run --project src/RcBridge.App -- list-ports
```
Örnek çıktı:
- `COM5`

## 6. Ham Veri Capture
```bash
dotnet run --project src/RcBridge.App -- capture --port COM5 --baud 115200 --out captures/session.bin --seconds 20
```
Bu dosyayı protokol çözümüne katkı için paylaşabilirsiniz.

## 7. Çalıştırma
`config.json` içindeki mapping/filter ayarlarını düzenleyin:
```bash
dotnet run --project src/RcBridge.App -- run --port COM5 --baud 115200 --config config.json
```

## 8. Teşhis
```bash
dotnet run --project src/RcBridge.App -- diagnose
```
Bu komut COM portları ve ViGEmBus durumunu özetler.

## 9. İnce Ayar
- Deadzone: merkez jitter azaltır
- Expo: merkez hassasiyetini artırır/azaltır
- Smoothing: ani sıçramaları yumuşatır
- Invert: eksen yönünü ters çevirir

## 10. Sık Hata Durumları
- Port açılamıyor: başka uygulama portu kullanıyor olabilir.
- Veri yok: kablo/sürücü/baud kontrol edin.
- Oyun görmüyor: oyunu yeniden başlatın, Steam Input ayarlarını kontrol edin.

Detaylı hata çözümü: `docs/troubleshooting.md`
