# TabletMonitor Virtual Display

Driver UMDF/IddCx que cria um monitor virtual separado no Windows 11.

## Primeira versão

- Um monitor virtual.
- Modo preferido: 1920 x 1200 a 60 Hz.
- Modos alternativos: 1920 x 1080 e 1280 x 800.
- O monitor permanece conectado enquanto `TabletMonitor.VirtualDisplay.App.exe` estiver aberto.
- O processamento de quadros segue o laço de swap-chain do exemplo oficial. A transmissão continua sendo feita pelo servidor TabletMonitor.

## Compilação

A automação `.github/workflows/driver-ci.yml` usa o WDK NuGet oficial e publica o artefato `TabletMonitor-VirtualDisplay-x64`.

## Instalação de teste

O pacote gerado inclui um certificado público de teste. Execute `Install-TabletMonitorVirtualDisplay.ps1` como administrador. Dependendo da política de assinatura do Windows, o modo de teste poderá ser necessário. Não altere Secure Boot nem a política de inicialização sem revisar essa etapa.

## Origem

Código derivado do exemplo oficial [IndirectDisplay](https://github.com/microsoft/Windows-driver-samples/tree/main/video/IndirectDisplay), distribuído pela Microsoft sob MS-PL. A licença aplicável ao código derivado está em `MS-PL.txt`.
