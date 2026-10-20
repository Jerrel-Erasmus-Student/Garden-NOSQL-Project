param([switch]$Seed)
$ErrorActionPreference = 'Stop'
$projectRoot = $PSScriptRoot
$mongoProcess = $null
$oldConnection = $env:Mongo__ConnectionString
$oldDatabase = $env:Mongo__Database
try {
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { throw 'Install the .NET SDK before running this script.' }
    $mongoCommand = Get-Command mongod -ErrorAction SilentlyContinue
    $mongoPath = if ($mongoCommand) { $mongoCommand.Source } else {
        Get-ChildItem -LiteralPath 'C:\Program Files\MongoDB\Server' -Filter mongod.exe -Recurse -ErrorAction SilentlyContinue |
            Sort-Object FullName -Descending | Select-Object -First 1 -ExpandProperty FullName
    }
    if (-not $mongoPath) { throw 'MongoDB Server was not found. Install MongoDB Community Server or follow the manual/Atlas steps in README.md.' }
    $dataPath = Join-Path $projectRoot '.local-data'
    New-Item -ItemType Directory -Path $dataPath -Force | Out-Null
    $arguments = @('--dbpath', ('"' + $dataPath + '"'), '--port', '27184', '--bind_ip', '127.0.0.1', '--logpath', ('"' + (Join-Path $dataPath 'mongo.log') + '"'))
    $mongoProcess = Start-Process -FilePath $mongoPath -ArgumentList $arguments -WindowStyle Hidden -PassThru
    $ready = $false
    for ($attempt = 0; $attempt -lt 30; $attempt++) {
        Start-Sleep -Milliseconds 500
        if ($mongoProcess.HasExited) { throw 'The demo MongoDB process exited. Check .local-data/mongo.log; port 27184 may already be in use.' }
        $socket = [System.Net.Sockets.TcpClient]::new()
        try { $socket.Connect('127.0.0.1', 27184); $ready = $true; break } catch { } finally { $socket.Dispose() }
    }
    if (-not $ready) { throw 'MongoDB did not become ready. See .local-data/mongo.log.' }
    $env:Mongo__ConnectionString = 'mongodb://127.0.0.1:27184'
    $env:Mongo__Database = 'GardenGroupProjectData'
    & dotnet restore (Join-Path $projectRoot 'NoSQL_Project.sln')
    if ($LASTEXITCODE -ne 0) { throw 'Dependency restore failed.' }
    if ($Seed) {
        $env:Import__Directory = Join-Path $projectRoot 'data'
        & dotnet run --project (Join-Path $projectRoot 'NoSQL_Project') -- --import-data --reset-imported-passwords
        if ($LASTEXITCODE -ne 0) { throw 'Demo setup failed. Seeding requires empty collections. On later runs, omit -Seed.' }
    }
    Write-Host 'Open http://localhost:5188. Press Ctrl+C here to stop the application.'
    & dotnet run --project (Join-Path $projectRoot 'NoSQL_Project')
} finally {
    if ($mongoProcess -and -not $mongoProcess.HasExited) { Stop-Process -Id $mongoProcess.Id }
    $env:Mongo__ConnectionString = $oldConnection
    $env:Mongo__Database = $oldDatabase
}
