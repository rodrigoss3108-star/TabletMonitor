[CmdletBinding()]
param(
    [int]$Porta = 5000,
    [int]$Largura = 1920,
    [int]$Altura = 1200,
    [int]$FPS = 30,
    [int]$BitrateKbps = 12000,
    [int]$Tela = 0,
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

Add-Type -AssemblyName System.Windows.Forms

$Telas = @(
    [System.Windows.Forms.Screen]::AllScreens |
        Sort-Object DeviceName
)

if ($Telas.Count -eq 0) {
    Write-Host "Nenhuma tela foi localizada pelo Windows." -ForegroundColor Red
    return
}

$TelaAlvo = $null

if ($Tela -gt 0 -and $Tela -le $Telas.Count) {
    $TelaAlvo = $Telas[$Tela - 1]
}

if ($Tela -gt $Telas.Count) {
    Write-Host "A tela $Tela não existe." -ForegroundColor Red

    $Telas |
        Select-Object DeviceName, Primary, Bounds |
        Format-Table -AutoSize

    return
}

if ($null -eq $TelaAlvo) {
    $TelaAlvo = $Telas |
        Where-Object {
            -not $_.Primary
        } |
        Select-Object -First 1
}

if ($null -eq $TelaAlvo) {
    $TelaAlvo = $Telas |
        Where-Object {
            $_.Primary
        } |
        Select-Object -First 1
}

Write-Host "`nTELAS ENCONTRADAS" -ForegroundColor Cyan

$TabelaTelas = for ($Indice = 0; $Indice -lt $Telas.Count; $Indice++) {
    [PSCustomObject]@{
        Numero      = $Indice + 1
        Dispositivo = $Telas[$Indice].DeviceName
        Principal   = $Telas[$Indice].Primary
        PosicaoX    = $Telas[$Indice].Bounds.X
        PosicaoY    = $Telas[$Indice].Bounds.Y
        Largura     = $Telas[$Indice].Bounds.Width
        Altura      = $Telas[$Indice].Bounds.Height
    }
}

$TabelaTelas |
    Format-Table -AutoSize

$CapturaX = $TelaAlvo.Bounds.X
$CapturaY = $TelaAlvo.Bounds.Y
$CapturaLargura = $TelaAlvo.Bounds.Width
$CapturaAltura = $TelaAlvo.Bounds.Height

Write-Host "`nTELA TRANSMITIDA AO TABLET" -ForegroundColor Green

[PSCustomObject]@{
    Dispositivo = $TelaAlvo.DeviceName
    Principal   = $TelaAlvo.Primary
    PosicaoX    = $CapturaX
    PosicaoY    = $CapturaY
    Largura     = $CapturaLargura
    Altura      = $CapturaAltura
} | Format-List

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
    --capture-x $CapturaX `
    --capture-y $CapturaY `
    --capture-width $CapturaLargura `
    --capture-height $CapturaAltura `
    --ffmpeg $CaminhoFFmpeg
