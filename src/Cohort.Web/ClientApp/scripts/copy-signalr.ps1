$ErrorActionPreference = 'Stop'

$root = Resolve-Path (Join-Path $PSScriptRoot '..')
$src = Join-Path $root 'node_modules\@microsoft\signalr\dist\browser\signalr.min.js'
$dstDir = Join-Path $root 'src\assets\vendor'
$dst = Join-Path $dstDir 'signalr.min.js'

if (-not (Test-Path $src)) {
    throw "SignalR client not found at: $src. Run 'npm install' first."
}

New-Item -ItemType Directory -Force -Path $dstDir | Out-Null
Copy-Item -Force -Path $src -Destination $dst

Write-Host "Copied SignalR client to $dst"
