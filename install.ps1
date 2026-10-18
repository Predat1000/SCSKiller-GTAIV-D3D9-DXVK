[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$BaseCommit = 'f8c8b43282566faaa4f4002ed4e56cf1fdbcfa48'
$ExpectedVersion = '1.3.0.0'
$Root = Split-Path -Parent $MyInvocation.MyCommand.Path
$BuildRoot = Join-Path $Root '.build'
$Source = Join-Path $BuildRoot 'SCSKiller'
$HelperOut = Join-Path $BuildRoot 'helper'
$Install = Join-Path $env:LOCALAPPDATA 'SCSKiller.App\current'
$AppExe = Join-Path $Install 'SCSKiller.exe'
$CoreGui = Join-Path $Install 'SCSKiller.Core.dll'
$CoreCli = Join-Path $Install 'cli\SCSKiller.Core.dll'
$Tools = Join-Path $env:LOCALAPPDATA 'SCSKiller\tools'
$HelperDest = Join-Path $Tools 'GTAIV.exe'
$Patch = Join-Path $Root 'SCSKiller-GTAIV-D3D9-DXVK.patch'
$HelperProject = Join-Path $Root 'helper-source\Gta4D3D9Warm.csproj'

function Fail([string]$Message) {
    Write-Error $Message
    exit 1
}

if (-not (Test-Path $AppExe)) { Fail "SCSKiller installation not found at $Install" }
if (-not (Test-Path $Patch)) { Fail "Patch file not found: $Patch" }
if (-not (Test-Path $HelperProject)) { Fail "Helper project not found: $HelperProject" }

$installedVersion = (Get-Item $AppExe).VersionInfo.FileVersion
if ($installedVersion -ne $ExpectedVersion) {
    Fail "This installer targets SCSKiller $ExpectedVersion only. Installed file version: $installedVersion"
}

if (-not (Get-Command git -ErrorAction SilentlyContinue)) { Fail 'Git is required.' }
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { Fail '.NET SDK is required.' }

$sdks = dotnet --list-sdks
if (-not ($sdks -match '^10\.')) { Fail '.NET 10 SDK is required to build SCSKiller Core.' }
if (-not ($sdks -match '^9\.')) { Fail '.NET 9 SDK is required to build the x86 GTA IV helper.' }

Write-Host 'Preparing clean SCSKiller v1.3.0 source...'
if (Test-Path $Source) { Remove-Item -Recurse -Force $Source }
New-Item -ItemType Directory -Force -Path $BuildRoot | Out-Null
& git clone --quiet https://github.com/BlueHeisenberg/SCSKiller.git $Source
Push-Location $Source
try {
    & git checkout --quiet $BaseCommit
    & git apply $Patch

    Write-Host 'Building patched SCSKiller.Core.dll...'
    & dotnet build 'src\SCSKiller.Core\SCSKiller.Core.csproj' -c Release -p:Version=1.3.0 '-p:VersionSuffix='
    if ($LASTEXITCODE -ne 0) { Fail 'SCSKiller Core build failed.' }
}
finally {
    Pop-Location
}

$BuiltCore = Join-Path $Source 'src\SCSKiller.Core\bin\Release\net10.0-windows\SCSKiller.Core.dll'
if (-not (Test-Path $BuiltCore)) { Fail "Built Core DLL not found: $BuiltCore" }
$assemblyVersion = [Reflection.AssemblyName]::GetAssemblyName($BuiltCore).Version.ToString()
if ($assemblyVersion -ne $ExpectedVersion) { Fail "Unexpected built Core assembly version: $assemblyVersion" }

Write-Host 'Building GTA IV x86 DXVK helper...'
if (Test-Path $HelperOut) { Remove-Item -Recurse -Force $HelperOut }
& dotnet publish $HelperProject -c Release -r win-x86 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o $HelperOut
if ($LASTEXITCODE -ne 0) { Fail 'GTA IV helper build failed.' }
$BuiltHelper = Join-Path $HelperOut 'GTAIV.exe'
if (-not (Test-Path $BuiltHelper)) { Fail "Built helper not found: $BuiltHelper" }

Write-Host 'Stopping SCSKiller...'
Get-Process SCSKiller -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 500

$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
foreach ($target in @($CoreGui, $CoreCli)) {
    if (-not (Test-Path $target)) { Fail "Installed Core DLL not found: $target" }
    $backup = "$target.pre-gta4-$stamp"
    Copy-Item $target $backup -Force
    Copy-Item $BuiltCore $target -Force
    Write-Host "Patched: $target"
    Write-Host "Backup : $backup"
}

New-Item -ItemType Directory -Force -Path $Tools | Out-Null
Copy-Item $BuiltHelper $HelperDest -Force
Write-Host "Helper : $HelperDest"

Start-Process $AppExe
Write-Host ''
Write-Host 'GTA IV support installed.' -ForegroundColor Green
Write-Host 'Refresh SCSKiller, locate Grand Theft Auto IV and compile it normally.'
