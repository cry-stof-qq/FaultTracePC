<#
    FaultTracePC — publication autonome (aucun prérequis sur la machine cible)

    Les trois exécutables (application, service de surveillance, CLI) sont publiés
    dans UN SEUL dossier : ils partagent alors les fichiers du runtime .NET au lieu
    de les dupliquer trois fois (~120 Mo au total au lieu de ~250 Mo).

    Utilisation :
        cd $env:USERPROFILE\Documents\FaultTracePC
        powershell -ExecutionPolicy Bypass -File build\publish.ps1
        # puis, pour l'archive portable :
        powershell -ExecutionPolicy Bypass -File build\publish.ps1 -Zip
#>
[CmdletBinding()]
param(
    [string]$Configuration = 'Release',
    [string]$Runtime = 'win-x64',
    [switch]$Zip,
    # Le nom de l'archive porte la version : sans elle, deux téléchargements
    # successifs deviennent indiscernables dans un dossier Téléchargements.
    [string]$Version = '1.3.0',
    [switch]$FrameworkDependent   # nécessite le runtime .NET Desktop sur la cible, mais ~15 Mo
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$out = Join-Path $root 'dist\FaultTracePC'

Write-Host "FaultTracePC — publication ($Configuration / $Runtime)" -ForegroundColor Cyan
if (Test-Path $out) { Remove-Item $out -Recurse -Force }
New-Item -ItemType Directory -Force -Path $out | Out-Null

$selfContained = (-not $FrameworkDependent).ToString().ToLower()

foreach ($proj in @('FaultTracePC.App', 'FaultTracePC.Monitor', 'FaultTracePC.Cli')) {
    Write-Host "  → $proj" -ForegroundColor Gray
    $log = dotnet publish (Join-Path $root "src\$proj") `
        -c $Configuration -r $Runtime `
        --self-contained $selfContained `
        -p:PublishSingleFile=false `
        -o $out 2>&1
    if ($LASTEXITCODE -ne 0) {
        $log | Write-Host        # sans cela, l'erreur de compilation resterait invisible
        throw "Échec de la publication de $proj"
    }
}

$size = [math]::Round((Get-ChildItem $out -Recurse | Measure-Object Length -Sum).Sum / 1MB, 1)
Write-Host "Publication terminée : $out ($size Mo)" -ForegroundColor Green
Write-Host "  FaultTracePC.exe          — application (interface graphique)"
Write-Host "  FaultTracePC.Cli.exe      — diagnostic en ligne de commande"
Write-Host "  FaultTracePC.Monitor.exe  — service de surveillance (installé par l'application ou le MSI)"

if ($Zip) {
    $zipPath = Join-Path $root "dist\FaultTracePC-$Version-portable.zip"
    if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
    Compress-Archive -Path "$out\*" -DestinationPath $zipPath
    Write-Host "Archive portable : $zipPath" -ForegroundColor Green
}
