# Compile CustomFancyZone dans le dossier app\ (un seul CustomFancyZone.exe).
# Usage : .\build.ps1          compile
#         .\build.ps1 -Run     compile puis lance l'outil
param([switch]$Run)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$app = Join-Path $root 'app'
$exe = Join-Path $app 'CustomFancyZone.exe'

# Ferme proprement l'instance en cours (elle rétablit le menu natif de Windows en partant).
if (Get-Process CustomFancyZone -ErrorAction SilentlyContinue) {
    if (Test-Path $exe) { & $exe --quit | Out-Null }
    Wait-Process CustomFancyZone -Timeout 5 -ErrorAction SilentlyContinue
    Get-Process CustomFancyZone -ErrorAction SilentlyContinue | Stop-Process -Force
}

dotnet publish (Join-Path $root 'src\CustomFancyZone.csproj') -c Release -o $app --nologo
if ($LASTEXITCODE -ne 0) { throw "La compilation a échoué." }

# layouts.json n'est jamais écrasé : l'exe le crée au premier lancement s'il n'existe pas.
Write-Host "OK : $exe"
if ($Run) { Start-Process $exe }
