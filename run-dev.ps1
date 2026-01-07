[CmdletBinding()]
param(
    [switch]$SameWindow,
    [switch]$BuildClient,
    [switch]$BuildDotnet,
    [switch]$SkipNpmInstall,
    [switch]$NoKillPorts
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path

$webProj = Join-Path $repoRoot 'src\Cohort.Web\Cohort.Web.csproj'
$idpProj = Join-Path $repoRoot 'src\Cohort.Idp\Cohort.Idp.csproj'
$clientDir = Join-Path $repoRoot 'src\Cohort.Web\ClientApp'

if (-not (Test-Path $webProj)) { throw "Web project not found: $webProj" }
if (-not (Test-Path $idpProj)) { throw "IdP project not found: $idpProj" }
if ($BuildClient -and -not (Test-Path (Join-Path $clientDir 'package.json'))) { throw "ClientApp not found: $clientDir" }

function Get-ListeningProcess([int]$Port) {
    try {
        $conn = Get-NetTCPConnection -State Listen -LocalPort $Port -ErrorAction Stop | Select-Object -First 1
        if (-not $conn) { return $null }

        $proc = Get-Process -Id $conn.OwningProcess -ErrorAction SilentlyContinue
        [pscustomobject]@{
            Port = $Port
            Pid  = $conn.OwningProcess
            Name = if ($proc) { $proc.ProcessName } else { "<unknown>" }
        }
    }
    catch {
        return $null
    }
}

$portsToCheck = @(5000, 5001, 5002, 5003)
$busy = @()
foreach ($p in $portsToCheck) {
    $hit = Get-ListeningProcess -Port $p
    if ($hit) { $busy += $hit }
}

if ($busy.Count -gt 0) {
    Write-Host "One or more expected dev ports are already in use:" -ForegroundColor Red
    $busy | Format-Table -AutoSize | Out-String | Write-Host

    if (-not $NoKillPorts) {
        Write-Host "Auto-killing busy port processes..." -ForegroundColor Yellow
        foreach ($b in $busy) {
            try {
                Stop-Process -Id $b.Pid -Force -ErrorAction Stop
                Write-Host "Killed PID $($b.Pid) on port $($b.Port) ($($b.Name))." -ForegroundColor DarkGray
            }
            catch {
                Write-Host "Failed to kill PID $($b.Pid) on port $($b.Port): $_" -ForegroundColor Red
            }
        }
    }
    else {
        Write-Host "Stop the existing process (or use default auto-kill) and re-run." -ForegroundColor Yellow
        Write-Host "Tip: remove -NoKillPorts flag to auto-kill on next run." -ForegroundColor DarkGray
        throw "Ports already in use."
    }
}

Write-Host "Starting Cohort.Idp (https://localhost:5001) and Cohort.Web (https://localhost:5003)..." -ForegroundColor Cyan

if ($BuildClient) {
    Write-Host "Building Angular client (npm ci + build:dotnet)..." -ForegroundColor Cyan
    Push-Location $clientDir
    try {
        if (-not $SkipNpmInstall) {
            npm ci
        }
        npm run build:dotnet
    }
    finally {
        Pop-Location
    }
}
else {
    Write-Host "Skipping client build (use -BuildClient to rebuild)..." -ForegroundColor DarkGray
}

if ($BuildDotnet) {
    Write-Host "Building .NET projects (UseAppHost=false to avoid file lock issues on Windows)..." -ForegroundColor Cyan
    dotnet build $idpProj --nologo -p:UseAppHost=false | Out-Null
    dotnet build $webProj --nologo -p:UseAppHost=false | Out-Null
}
else {
    Write-Host "Skipping .NET build (use -BuildDotnet to rebuild)..." -ForegroundColor DarkGray
}

if ($SameWindow) {
    Write-Host "Running both in this window (Ctrl+C to stop each)." -ForegroundColor Yellow

    $idpJob = Start-Job -ScriptBlock {
        param($repoRoot, $idpProj)
        Set-Location $repoRoot
        dotnet run --no-build --launch-profile https --project $idpProj -p:UseAppHost=false
    } -ArgumentList $repoRoot, $idpProj

    $webJob = Start-Job -ScriptBlock {
        param($repoRoot, $webProj)
        Set-Location $repoRoot
        dotnet run --no-build --launch-profile https --project $webProj -p:UseAppHost=false
    } -ArgumentList $repoRoot, $webProj

    Write-Host "Jobs started: IdP=$($idpJob.Id), Web=$($webJob.Id)" -ForegroundColor Green
    Write-Host "Tip: Receive-Job -Id <id> -Keep" -ForegroundColor DarkGray
    return
}

$dotnetIdp = "dotnet run --no-build --launch-profile https --project `"$idpProj`" -p:UseAppHost=false"
$dotnetWeb = "dotnet run --no-build --launch-profile https --project `"$webProj`" -p:UseAppHost=false"

Start-Process powershell -WorkingDirectory $repoRoot -ArgumentList '-NoExit', '-Command', $dotnetIdp | Out-Null
Start-Process powershell -WorkingDirectory $repoRoot -ArgumentList '-NoExit', '-Command', $dotnetWeb | Out-Null

Write-Host "Opened two PowerShell windows." -ForegroundColor Green
