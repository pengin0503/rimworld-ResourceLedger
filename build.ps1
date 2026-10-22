param(
 [Parameter(Mandatory=$true)][string]$GameDir,
 [Parameter(Mandatory=$true)][string]$HarmonyPath,
 [switch]$RunTests
)
$ErrorActionPreference='Stop'
if(!(Test-Path -LiteralPath (Join-Path $GameDir 'Assembly-CSharp.dll'))){throw 'GameDir must contain the RimWorld 1.7 Assembly-CSharp.dll'}
if(!(Test-Path -LiteralPath $HarmonyPath)){throw 'HarmonyPath must point to the 1.7 0Harmony.dll'}
$env:RIMWORLD_GAME_DIR=(Resolve-Path -LiteralPath $GameDir).Path
$env:RIMWORLD_HARMONY=(Resolve-Path -LiteralPath $HarmonyPath).Path
$env:DOTNET_CLI_TELEMETRY_OPTOUT='1'
$env:DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE='true'
Push-Location $PSScriptRoot
try{
 dotnet build ResourceLedger.csproj -c Release -p:NuGetAudit=false
 if($LASTEXITCODE -ne 0){throw 'Release build failed'}
 if($RunTests){
  dotnet run --project tests/ResourceLedger.Tests.csproj -c Release -p:NuGetAudit=false -- --contracts --benchmark
  if($LASTEXITCODE -ne 0){throw 'Tests failed'}
 }
 python tools/verify.py
 if($LASTEXITCODE -ne 0){throw 'Artifact verification failed'}
 python tools/package.py
 if($LASTEXITCODE -ne 0){throw 'Packaging failed'}
}finally{Pop-Location}

