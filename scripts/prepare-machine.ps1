param([switch]$Elevated)
$ErrorActionPreference = 'Stop'
$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    $process = Start-Process powershell.exe -Verb RunAs -WindowStyle Hidden -PassThru -ArgumentList @('-NoProfile','-ExecutionPolicy','Bypass','-File',('"' + $PSCommandPath + '"'),'-Elevated') -Wait
    exit $process.ExitCode
}
$local = Join-Path (Split-Path $PSScriptRoot) '.local'
New-Item -ItemType Directory -Force $local | Out-Null
$logPath = Join-Path $local 'prepare-machine.log'
Start-Transcript -Path $logPath -Append
try {
    $needsRestart = $false
    foreach ($feature in @('Microsoft-Windows-Subsystem-Linux','VirtualMachinePlatform')) {
        $state = Get-WindowsOptionalFeature -Online -FeatureName $feature
        if ($state.State -eq 'EnablePending') { $needsRestart = $true; continue }
        if ($state.State -ne 'Enabled') {
            $result = Enable-WindowsOptionalFeature -Online -FeatureName $feature -All -NoRestart
            $needsRestart = $needsRestart -or $result.RestartNeeded
        }
    }
    $desktop = Join-Path $env:ProgramFiles 'Docker\Docker\Docker Desktop.exe'
    if (-not (Test-Path $desktop)) {
        if (-not (Get-Command winget.exe -ErrorAction SilentlyContinue)) { throw 'Установите Docker Desktop с https://docs.docker.com/desktop/setup/install/windows-install/, затем повторите подготовку.' }
        & winget.exe install --exact --id Docker.DockerDesktop --source winget --accept-source-agreements --accept-package-agreements --silent
        if ($LASTEXITCODE -ne 0 -and $LASTEXITCODE -ne 3010) { throw 'Установка Docker Desktop не завершилась. См. .local/prepare-machine.log.' }
        $needsRestart = $needsRestart -or ($LASTEXITCODE -eq 3010)
    }
    if ($needsRestart) {
        'Перезагрузите Windows, затем повторите prepare-machine.cmd.' | Set-Content (Join-Path $local 'restart-required.txt')
        Write-Host 'Нужна перезагрузка Windows. Автоматическая перезагрузка не выполняется.'
    } else {
        & wsl.exe --update
        if ($LASTEXITCODE -ne 0) { throw 'Не удалось обновить WSL.' }
        & wsl.exe --set-default-version 2
        if ($LASTEXITCODE -ne 0) { throw 'Не удалось выбрать WSL 2.' }
        Remove-Item -LiteralPath (Join-Path $local 'restart-required.txt') -Force -ErrorAction SilentlyContinue
        Write-Host 'В Docker Desktop выберите Linux containers, включите WSL 2 engine и создайте Kubernetes cluster kubeadm (один узел). Затем запустите start-local.cmd.'
    }
} finally { Stop-Transcript }
