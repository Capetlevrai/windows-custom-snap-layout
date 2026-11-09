# Construit les fichiers à distribuer dans dist\ :
#   dist\portable\CustomFancyZone.exe        exe autonome (.NET inclus), rien à installer
#   dist\CustomFancyZone-Setup-<version>.exe  installeur (Inno Setup 6)
# Usage : .\release.ps1
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$dist = Join-Path $root 'dist'
[xml]$proj = Get-Content (Join-Path $root 'src\CustomFancyZone.csproj')
$version = $proj.Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1

if (Test-Path $dist) { Remove-Item $dist -Recurse -Force }

dotnet publish (Join-Path $root 'src\CustomFancyZone.csproj') -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true `
    -p:DebugType=embedded -o (Join-Path $dist 'portable') --nologo
if ($LASTEXITCODE -ne 0) { throw "La compilation a échoué." }

$iscc = @("$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe", "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe") |
    Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) { throw "Inno Setup 6 introuvable (winget install JRSoftware.InnoSetup)." }
& $iscc /Q "/DAppVersion=$version" "/DSourceExe=$(Join-Path $dist 'portable\CustomFancyZone.exe')" (Join-Path $root 'installer\CustomFancyZone.iss')
if ($LASTEXITCODE -ne 0) { throw "La création de l'installeur a échoué." }

Get-ChildItem $dist -Recurse -File -Filter *.exe | ForEach-Object { "{0,-45} {1,8:N1} Mo" -f $_.Name, ($_.Length / 1MB) }
