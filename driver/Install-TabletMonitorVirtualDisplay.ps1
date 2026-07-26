#Requires -RunAsAdministrator
[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"

$Diretorio = $PSScriptRoot
$Certificado = Join-Path $Diretorio "TabletMonitor-Test.cer"
$Inf = Join-Path $Diretorio "TabletMonitor.VirtualDisplay.inf"
$Aplicativo = Join-Path $Diretorio "TabletMonitor.VirtualDisplay.App.exe"

if (-not (Test-Path -LiteralPath $Certificado)) {
    throw "Certificado de teste não encontrado: $Certificado"
}

if (-not (Test-Path -LiteralPath $Inf)) {
    throw "Arquivo INF não encontrado: $Inf"
}

if (-not (Test-Path -LiteralPath $Aplicativo)) {
    throw "Aplicativo do monitor virtual não encontrado: $Aplicativo"
}

Write-Host "INSTALANDO CERTIFICADO DE TESTE" -ForegroundColor Cyan

Import-Certificate `
    -FilePath $Certificado `
    -CertStoreLocation "Cert:\LocalMachine\Root" |
    Out-Null

Import-Certificate `
    -FilePath $Certificado `
    -CertStoreLocation "Cert:\LocalMachine\TrustedPublisher" |
    Out-Null

Write-Host "INSTALANDO DRIVER" -ForegroundColor Cyan

& pnputil.exe `
    /add-driver `
    $Inf `
    /install

if ($LASTEXITCODE -ne 0) {
    throw "O pnputil falhou com o código $LASTEXITCODE."
}

$ProcessoExistente = Get-Process `
    -Name "TabletMonitor.VirtualDisplay.App" `
    -ErrorAction SilentlyContinue |
    Select-Object -First 1

if ($null -ne $ProcessoExistente) {
    Write-Host "O monitor virtual já está conectado." -ForegroundColor Yellow
    return
}

Write-Host "CONECTANDO MONITOR VIRTUAL" -ForegroundColor Cyan

$Processo = Start-Process `
    -FilePath $Aplicativo `
    -WorkingDirectory $Diretorio `
    -PassThru

Start-Sleep -Seconds 3

Write-Host "MONITOR VIRTUAL INICIADO" -ForegroundColor Green

[PSCustomObject]@{
    ProcessoId = $Processo.Id
    Aplicativo = $Aplicativo
} | Format-List
