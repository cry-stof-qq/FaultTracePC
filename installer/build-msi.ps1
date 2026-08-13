<#
    FaultTracePC — construction du paquet MSI (déployable par GPO)

    PRÉREQUIS (une seule fois) :
        dotnet tool install --global wix --version 6.0.2

    Pourquoi épingler la 6.0.2 ? C'est la version dont la compatibilité avec le
    schéma utilisé ici (namespace v4) est éprouvée. WiX 7 existe depuis avril 2026 ;
    si tu veux l'essayer : build-msi.ps1 -WixVersion 7.0.0 (et vérifie le résultat).

    UTILISATION :
        cd $env:USERPROFILE\Documents\FaultTracePC
        powershell -ExecutionPolicy Bypass -File installer\build-msi.ps1

    Le script publie d'abord les trois exécutables (build\publish.ps1) puis
    fabrique dist\FaultTracePC-<version>.msi.

    DÉPLOIEMENT PAR GPO :
        1. Copier le MSI dans un partage lisible par « Ordinateurs du domaine »
           (droits Lecture pour « Ordinateurs du domaine », pas seulement les utilisateurs).
        2. GPO → Configuration ordinateur → Stratégies → Paramètres du logiciel
           → Installation de logiciel → Nouveau → Package → chemin UNC du MSI
           → « Attribué ».
        3. L'installation se fait au démarrage suivant des machines.
    Installation silencieuse manuelle :  msiexec /i FaultTracePC-0.9.0.msi /qn
    Désinstallation silencieuse :        msiexec /x FaultTracePC-0.9.0.msi /qn
#>
[CmdletBinding()]
param(
    [string]$WixVersion = '6.0.2',
    [string]$Version = '0.9.0',
    [switch]$SkipPublish
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$publishDir = Join-Path $root 'dist\FaultTracePC'
$msiPath = Join-Path $root "dist\FaultTracePC-$Version.msi"

# --- 1. Outil WiX ---------------------------------------------------------
# L'élément <Files> (récolte de dossier) n'existe qu'à partir de WiX v5 : une v4
# déjà installée doit être mise à niveau, sinon la construction échoue.
$installed = $null
if (Get-Command wix -ErrorAction SilentlyContinue) {
    try { $installed = [version](((& wix --version) 2>$null) -replace '[^\d.].*$', '') } catch { }
}
if (-not $installed -or $installed -lt [version]'5.0.0') {
    Write-Host "Outil WiX absent ou trop ancien — installation de la version $WixVersion…" -ForegroundColor Yellow
    dotnet tool update --global wix --version $WixVersion
    if ($LASTEXITCODE -ne 0) { throw "Échec de l'installation de l'outil WiX. Ouvre un nouveau terminal et réessaie." }
    $env:PATH = "$env:USERPROFILE\.dotnet\tools;$env:PATH"
}

# --- 2. Publication autonome ---------------------------------------------
if (-not $SkipPublish) {
    Write-Host 'Publication des exécutables…' -ForegroundColor Cyan
    & (Join-Path $root 'build\publish.ps1')
}
if (-not (Test-Path (Join-Path $publishDir 'FaultTracePC.exe'))) {
    throw "Dossier de publication introuvable ou incomplet : $publishDir"
}

# --- 3. Construction du MSI ----------------------------------------------
Write-Host 'Construction du paquet MSI…' -ForegroundColor Cyan
New-Item -ItemType Directory -Force -Path (Split-Path $msiPath) | Out-Null

wix build (Join-Path $PSScriptRoot 'FaultTracePC.wxs') `
    -d "SourceDir=$publishDir" `
    -d "AssetsDir=$(Join-Path $root 'assets')" `
    -d "Version=$Version" `
    -arch x64 `
    -out $msiPath

if ($LASTEXITCODE -ne 0) { throw 'Échec de la construction du MSI.' }

$size = [math]::Round((Get-Item $msiPath).Length / 1MB, 1)
Write-Host "MSI construit : $msiPath ($size Mo)" -ForegroundColor Green
Write-Host ''
Write-Host 'Test local  : msiexec /i "' -NoNewline; Write-Host $msiPath -NoNewline; Write-Host '" /qn'
Write-Host 'Journalisé  : msiexec /i "' -NoNewline; Write-Host $msiPath -NoNewline; Write-Host '" /l*v install.log'
Write-Host 'Suppression : msiexec /x "' -NoNewline; Write-Host $msiPath -NoNewline; Write-Host '" /qn'
