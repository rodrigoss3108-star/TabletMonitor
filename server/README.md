# Servidor Windows

O servidor captura o desktop do Windows com FFmpeg, codifica em H.264 e envia
os quadros ao aplicativo Android pelo protocolo TabletMonitor v1.

## Estado atual

- Um tablet conectado por vez.
- Captura do desktop completo.
- H.264 com `libx264`, preset ultrafast e modo de baixa latência.
- Resolução padrão de 1920 × 1200.
- 30 FPS e bitrate de 12 Mbps.
- Reconexão permitida após o tablet desconectar.

Esta versão transmite o desktop existente. A criação de um monitor realmente
estendido será implementada posteriormente com um Indirect Display Driver.

## Requisitos

- Windows 10 ou 11.
- .NET 10 SDK.
- FFmpeg com suporte a `gdigrab` e `libx264`.
- Porta TCP 5000 liberada na rede privada.

## Execução

No PowerShell:

```powershell
Set-Location "D:\github\TabletMonitor\server"

.\Start-TabletMonitorServer.ps1 `
    -CaminhoFFmpeg "D:\TabletMonitor\tools\ffmpeg\bin\ffmpeg.exe"
```

No aplicativo Android, informe o IP do computador e a porta `5000`.

## Parâmetros

```powershell
Set-Location "D:\github\TabletMonitor\server"

.\Start-TabletMonitorServer.ps1 `
    -Porta 5000 `
    -Largura 1920 `
    -Altura 1200 `
    -FPS 30 `
    -BitrateKbps 12000 `
    -CaminhoFFmpeg "D:\TabletMonitor\tools\ffmpeg\bin\ffmpeg.exe"
```

