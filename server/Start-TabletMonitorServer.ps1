[CmdletBinding()]
param(
    [int]$Porta = 5000,
    [int]$Largura = 1920,
    [int]$Altura = 1200,
    [int]$FPS = 30,
    [int]$BitrateKbps = 12000,
    [string]$CaminhoFFmpeg = "D:\TabletMonitor\tools\ffmpeg\bin\ffmpeg.exe"
)

$ErrorActionPreference = "Stop"

$Projeto = Join-Path `
    $PSScriptRoot `
    "TabletMonitor.Server\TabletMonitor.Server.csproj"

$Dotnet = Get-Command "dotnet.exe" -ErrorAction SilentlyContinue

if ($null -eq $Dotnet) {
    Write-Host ".NET SDK não localizado." -ForegroundColor Red
    Write-Host "É necessário o .NET 10 SDK para executar o servidor." -ForegroundColor Yellow
    return
}

if (-not (Test-Path -LiteralPath $CaminhoFFmpeg)) {
    $FFmpegNoPath = Get-Command "ffmpeg.exe" -ErrorAction SilentlyContinue

    if ($null -ne $FFmpegNoPath) {
        $CaminhoFFmpeg = $FFmpegNoPath.Source
    }
}

if (-not (Test-Path -LiteralPath $CaminhoFFmpeg)) {
    Write-Host "FFmpeg não localizado." -ForegroundColor Red
    Write-Host "Caminho verificado: $CaminhoFFmpeg" -ForegroundColor Yellow
    return
}

Set-Location -LiteralPath $PSScriptRoot

& $Dotnet.Source run `
    --project $Projeto `
    --configuration Release `
    -- `
    --port $Porta `
    --width $Largura `
    --height $Altura `
    --fps $FPS `
    --bitrate $BitrateKbps `
    --ffmpeg $CaminhoFFmpeg

