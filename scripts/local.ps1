param([switch]$Stop, [switch]$ForwardSupervisor)
$ErrorActionPreference = 'Stop'
$env:PYTHONUTF8 = '1'
$utf8 = New-Object System.Text.UTF8Encoding($false)
[Console]::OutputEncoding = $utf8
[Console]::InputEncoding = $utf8
$OutputEncoding = $utf8
$root = Split-Path $PSScriptRoot
Set-Location $root
$local = Join-Path $root '.local'
New-Item -ItemType Directory -Force $local | Out-Null
function Run-Native([string]$Executable, [string[]]$Arguments) {
    & $Executable @Arguments
    if ($LASTEXITCODE -ne 0) { throw "Ошибка выполнения $Executable (код $LASTEXITCODE)." }
}
function Native-Output([string]$Executable, [string[]]$Arguments) {
    $ErrorActionPreference = 'Continue'
    $output = & $Executable @Arguments 2>$null
    $script:LASTEXITCODE = $LASTEXITCODE
    return $output
}
function Wait-Kubernetes([int]$TimeoutSeconds = 300, [int]$PollSeconds = 3) {
    $contexts = @(Native-Output 'kubectl' @('--context','docker-desktop','config','get-contexts','-o','name'))
    if ($LASTEXITCODE -ne 0 -or $contexts -notcontains 'docker-desktop') { throw 'Контекст docker-desktop отсутствует. Создайте Kubernetes cluster в Docker Desktop: Kubernetes > Create cluster > kubeadm.' }
    Write-Host 'Ожидание запуска Kubernetes Docker Desktop...'
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    do {
        $node = Native-Output 'kubectl' @('--context','docker-desktop','get','nodes','-o','name','--request-timeout=10s')
        if ($LASTEXITCODE -eq 0 -and $node) { return $node }
        if ((Get-Date) -ge $deadline) { break }
        Start-Sleep -Seconds $PollSeconds
    } while ((Get-Date) -lt $deadline)
    throw 'Kubernetes Docker Desktop не запустился за 5 минут. Проверьте состояние cluster kubeadm в Docker Desktop, память WSL и журнал Docker Desktop; затем повторите start-local.cmd.'
}
function Kube([string[]]$Arguments) { Run-Native 'kubectl' (@('--context','docker-desktop') + $Arguments) }
function Owned-Process($Entry, [string]$Name) {
    if (-not $Entry) { return $null }
    $process = Get-CimInstance Win32_Process -Filter "ProcessId=$($Entry.pid)" -ErrorAction SilentlyContinue
    if ($process -and $process.Name -eq $Name -and $process.CommandLine -and $process.CommandLine.Contains($Entry.marker)) {
        $running = Get-Process -Id $Entry.pid -ErrorAction SilentlyContinue
        if ($running -and $running.StartTime.ToUniversalTime().Ticks.ToString() -eq [string]$Entry.started) { return $running }
    }
    return $null
}
function Read-State([string]$Path) {
    if (Test-Path $Path) { try { $value = Get-Content $Path -Raw | ConvertFrom-Json; return $value } catch { return $null } }
    return $null
}
function Controller-Image([string]$BuildsPath) {
    $builds = Get-Content $BuildsPath -Raw | ConvertFrom-Json
    $controllers = @($builds | Where-Object { $_.name -eq 'controller' })
    if ($controllers.Count -ne 1 -or [string]::IsNullOrWhiteSpace([string]$controllers[0].image)) {
        throw 'builds.json должен содержать ровно один controller с непустым image. Повторите render-local.py.'
    }
    return [string]$controllers[0].image
}
function Save-State([string]$Path, $Value) {
    ConvertTo-Json -InputObject $Value -Depth 15 | Set-Content ($Path + '.tmp') -Encoding UTF8
    Move-Item -LiteralPath ($Path + '.tmp') -Destination $Path -Force
}
function Process-State($Process, [string]$Marker) {
    return @{pid=$Process.Id;started=$Process.StartTime.ToUniversalTime().Ticks.ToString();marker=$Marker}
}
function Listening([int]$Port) {
    return @(Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue).Count -gt 0
}
function Stop-Forwards {
    $supervisorPath = Join-Path $local 'forward-supervisor.json'
    $supervisor = Owned-Process (Read-State $supervisorPath) 'powershell.exe'
    if ($supervisor) { Stop-Process -Id $supervisor.Id -Force -ErrorAction SilentlyContinue }
    Remove-Item -LiteralPath $supervisorPath -Force -ErrorAction SilentlyContinue
    $path = Join-Path $local 'forwards.json'
    if (Test-Path $path) {
        foreach ($entry in (Get-Content $path -Raw | ConvertFrom-Json)) {
            $process = Owned-Process $entry 'kubectl.exe'
            if ($process) {
                Stop-Process -Id $entry.pid -Force -ErrorAction SilentlyContinue
            }
        }
        Remove-Item -LiteralPath $path
    }
}
function Show-Diagnostics([string]$JobName) {
    & kubectl --context docker-desktop -n collaby get pods -o wide
    & kubectl --context docker-desktop -n collaby-system get pods -l "job-name=$JobName" -o wide
    & kubectl --context docker-desktop -n collaby get events --field-selector type=Warning --sort-by=.lastTimestamp
    Write-Host 'OOMKilled: увеличьте память Docker/WSL. ImagePullBackOff: проверьте интернет. Загрузка AI возобновляется из постоянного хранилища при повторном запуске.'
    Write-Host "Журнал Job: kubectl --context docker-desktop -n collaby-system logs job/$JobName"
}
$dockerBins = @("$env:ProgramFiles\Docker\Docker\resources\bin", "$env:LOCALAPPDATA\Programs\DockerDesktop\resources\bin")
foreach ($bin in $dockerBins) { if (Test-Path $bin) { $env:PATH = "$bin;$env:PATH" } }
if ($ForwardSupervisor) {
    $entries = @{}
    $path = Join-Path $local 'forwards.json'
    try {
        while ($true) {
            $ports = Read-State (Join-Path $local 'rendered/ports.json')
            foreach ($property in $ports.PSObject.Properties) {
                $name = $property.Name
                if (Owned-Process $entries[$name] 'kubectl.exe') { continue }
                if (Listening $property.Value.local) { continue }
                $mapping = "$($property.Value.local):$($property.Value.remote)"
                $marker = "service/$($property.Value.service) $mapping"
                $process = Start-Process kubectl -WindowStyle Hidden -PassThru -ArgumentList @('--context','docker-desktop','-n',$property.Value.namespace,'port-forward',"service/$($property.Value.service)",$mapping,'--address=127.0.0.1') -RedirectStandardOutput (Join-Path $local "$name-forward.log") -RedirectStandardError (Join-Path $local "$name-forward-error.log")
                $entries[$name] = Process-State $process $marker
                Save-State $path @($entries.Values)
            }
            Start-Sleep -Seconds 3
        }
    } finally {
        foreach ($entry in $entries.Values) { $process = Owned-Process $entry 'kubectl.exe'; if ($process) { Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue } }
    }
    exit
}
$mutex = New-Object Threading.Mutex($false, 'Local\CollabyLauncher')
try { $locked = $mutex.WaitOne(0) } catch [Threading.AbandonedMutexException] { $locked = $true }
if (-not $locked) { $mutex.Dispose(); Write-Host 'Запуск Collaby уже выполняется.'; exit 0 }
try {
    $dockerBins = @("$env:ProgramFiles\Docker\Docker\resources\bin", "$env:LOCALAPPDATA\Programs\DockerDesktop\resources\bin")
    foreach ($bin in $dockerBins) { if (Test-Path $bin) { $env:PATH = "$bin;$env:PATH" } }
    if ($Stop) {
        Stop-Forwards
        foreach ($namespace in @('collaby-system','collaby','portainer')) {
            Kube @('-n',$namespace,'delete','job','-l','app.kubernetes.io/part-of=collaby','--ignore-not-found=true')
            $workloads = Native-Output 'kubectl' @('--context','docker-desktop','-n',$namespace,'get','deployment,statefulset','-l','app.kubernetes.io/part-of=collaby','-o','name')
            if ($LASTEXITCODE -ne 0) { throw "Не удалось проверить workloads в $namespace. Проверьте Kubernetes Docker Desktop." }
            if ($workloads) { Kube (@('-n',$namespace,'scale','--replicas=0') + @($workloads)) }
            Kube @('-n',$namespace,'delete','daemonset','-l','app.kubernetes.io/part-of=collaby','--ignore-not-found=true')
        }
        Write-Host 'Collaby остановлен. Постоянные данные сохранены.'
        exit 0
    }
    if (-not (Get-Command docker -ErrorAction SilentlyContinue)) { throw 'Установите Docker Desktop и выполните prepare-machine.cmd. Подробности: README.md.' }
    $desktop = @("$env:ProgramFiles\Docker\Docker\Docker Desktop.exe", "$env:LOCALAPPDATA\Programs\DockerDesktop\Docker Desktop.exe") | Where-Object { Test-Path $_ } | Select-Object -First 1
    if ($desktop -and -not (Get-Process 'Docker Desktop' -ErrorAction SilentlyContinue)) { Start-Process -FilePath $desktop -WindowStyle Hidden }
    Write-Host 'Ожидание Docker Desktop и Linux engine...'
    $deadline = (Get-Date).AddMinutes(5)
    do {
        $contexts = @(Native-Output 'docker' @('context','ls','--format','{{.Name}}'))
        $dockerContext = if ($contexts -contains 'desktop-linux') { 'desktop-linux' } else { 'default' }
        $os = Native-Output 'docker' @('--context',$dockerContext,'info','--format','{{.OSType}}')
        if ($LASTEXITCODE -eq 0 -and $os -eq 'linux') { break }
        Start-Sleep -Seconds 3
    } while ((Get-Date) -lt $deadline)
    if ($os -ne 'linux') { throw 'Linux engine Docker Desktop не готов. Проверьте WSL 2 и журнал Docker Desktop.' }
    if (-not (Get-Command kubectl -ErrorAction SilentlyContinue)) { throw 'kubectl отсутствует. Восстановите установку Docker Desktop.' }
    $node = Wait-Kubernetes
    Kube @('wait','--for=condition=Ready','node','--all','--timeout=180s')
    $nodes = & kubectl --context docker-desktop get nodes -o json | ConvertFrom-Json
    if ($nodes.items.Count -ne 1 -or $nodes.items[0].metadata.name -ne 'docker-desktop') { throw 'Ожидался одноузловой Kubernetes kubeadm docker-desktop.' }
    $capacity = [long]($nodes.items[0].status.capacity.memory -replace 'Ki$','') / 1MB
    if ($capacity -lt 10) { throw ('Kubernetes доступно только {0:N1} GiB памяти. Выделите WSL/Docker не менее 12 GiB и повторите запуск.' -f $capacity) }
    $priorJobs = Native-Output 'kubectl' @('--context','docker-desktop','-n','collaby-system','get','jobs','-l','app.kubernetes.io/part-of=collaby','-o','json')
    if ($LASTEXITCODE -eq 0) {
        $pending = @(($priorJobs | ConvertFrom-Json).items | Where-Object { -not $_.status.succeeded -and -not $_.status.failed -and -not ($_.status.conditions | Where-Object { $_.type -eq 'Failed' -and $_.status -eq 'True' }) })
        if ($pending.Count -gt 0) { throw "Ansible Job $($pending[0].metadata.name) ещё выполняется. Дождитесь завершения или выполните stop-local.cmd перед новым применением." }
    }
    Run-Native 'python' @('-m','pip','install','--quiet','-r','scripts/requirements-local.txt')
    Run-Native 'python' @('scripts/render-local.py')
    $rendered = Join-Path $local 'rendered'
    $ports = Get-Content (Join-Path $rendered 'ports.json') -Raw | ConvertFrom-Json
    $supervisorPath = Join-Path $local 'forward-supervisor.json'
    $existingSupervisor = Owned-Process (Read-State $supervisorPath) 'powershell.exe'
    if (-not $existingSupervisor) { Stop-Forwards }
    $ownedForwards = @(Read-State (Join-Path $local 'forwards.json'))
    foreach ($property in $ports.PSObject.Properties) {
        if ((Listening $property.Value.local) -and -not ($ownedForwards | Where-Object { $_ -and $_.marker.EndsWith(" $($property.Value.local):$($property.Value.remote)") -and (Owned-Process $_ 'kubectl.exe') })) { throw "Порт $($property.Value.local) занят. Освободите его и повторите запуск." }
    }
    $builds = Get-Content (Join-Path $rendered 'builds.json') -Raw | ConvertFrom-Json
    $image = Controller-Image (Join-Path $rendered 'builds.json')
    foreach ($build in $builds) {
        Native-Output 'docker' @('--context',$dockerContext,'image','inspect',$build.image) | Out-Null
        if ($LASTEXITCODE -eq 0) { continue }
        Write-Host "Сборка $($build.name)..."
        $buildArguments = @('--context',$dockerContext,'build','--platform','linux/amd64','--load','-t',$build.image,'-f',(Join-Path $build.context $build.dockerfile))
        if ($build.name -eq 'frontend') { $buildArguments += @('--build-arg', ('VITE_BUILD_ID=' + $build.image), '--build-arg', 'VITE_REF_NAME=main') }
        Run-Native 'docker' ($buildArguments + @($build.context))
    }
    Kube @('apply','-f','kubernetes/bootstrap.yml')
    $cm = & kubectl --context docker-desktop -n collaby-system create configmap local-configuration --from-file="$rendered\config.json" --from-file="$rendered\storage.json" --from-file="$rendered\topics.json" --from-file="$rendered\apps.json" --from-file="$rendered\panels.json" --dry-run=client -o json
    if ($LASTEXITCODE -ne 0) { throw 'Не удалось подготовить ConfigMap.' }
    $cm | & kubectl --context docker-desktop apply -f - | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Не удалось сохранить конфигурацию.' }
    $secret = & kubectl --context docker-desktop -n collaby-system create secret generic local-credentials --from-file="$rendered\secrets.json" --from-file="$rendered\local.json" --dry-run=client -o json
    if ($LASTEXITCODE -ne 0) { throw 'Не удалось подготовить Secret.' }
    $secret | & kubectl --context docker-desktop apply -f - | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Не удалось сохранить секреты.' }
    $jobName = 'local-apply-' + [DateTimeOffset]::UtcNow.ToUnixTimeSeconds() + '-' + ([Guid]::NewGuid().ToString('N').Substring(0,6))
    $job = @{apiVersion='batch/v1';kind='Job';metadata=@{name=$jobName;namespace='collaby-system';labels=@{'app.kubernetes.io/part-of'='collaby'}};spec=@{backoffLimit=0;activeDeadlineSeconds=5400;ttlSecondsAfterFinished=86400;template=@{spec=@{serviceAccountName='controller';restartPolicy='Never';containers=@(@{name='ansible';image=$image;imagePullPolicy='Never';resources=@{requests=@{memory='256Mi';cpu='100m'};limits=@{memory='768Mi'}};volumeMounts=@(@{name='configuration';mountPath='/configuration';readOnly=$true},@{name='credentials';mountPath='/credentials';readOnly=$true})});volumes=@(@{name='configuration';configMap=@{name='local-configuration'}},@{name='credentials';secret=@{secretName='local-credentials'}})}}}}
    $job | ConvertTo-Json -Depth 30 | & kubectl --context docker-desktop apply -f - | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Не удалось создать Ansible Job.' }
    Write-Host "Ansible Job: $jobName. Первая загрузка AI может занять несколько минут."
    $deadline = (Get-Date).AddMinutes(90)
    do {
        $state = & kubectl --context docker-desktop -n collaby-system get job $jobName -o json | ConvertFrom-Json
        if ($state.status.succeeded -gt 0) { break }
        if ($state.status.failed -gt 0) {
            Show-Diagnostics $jobName
            throw 'Ansible Job завершился с ошибкой. Проверьте Failed/OOMKilled/ImagePullBackOff и свободную память.'
        }
        Start-Sleep -Seconds 5
    } while ((Get-Date) -lt $deadline)
    if (-not $state.status.succeeded) { Show-Diagnostics $jobName; throw 'Истекло время ожидания Ansible Job. Проверьте состояние Job перед повторным запуском.' }
    if (-not $existingSupervisor) {
        $process = Start-Process powershell.exe -WindowStyle Hidden -PassThru -ArgumentList @('-NoProfile','-ExecutionPolicy','Bypass','-File',('"' + $PSCommandPath + '"'),'-ForwardSupervisor') -RedirectStandardOutput (Join-Path $local 'forward-supervisor.log') -RedirectStandardError (Join-Path $local 'forward-supervisor-error.log')
        Save-State $supervisorPath (Process-State $process '-ForwardSupervisor')
    }
    $deadline=(Get-Date).AddSeconds(90)
    do {
        $missing = @($ports.PSObject.Properties | Where-Object { -not (Listening $_.Value.local) })
        try { $ready=Invoke-WebRequest http://localhost:8080/healthz -UseBasicParsing -TimeoutSec 3 } catch { $ready=$null }
        if ($ready -and $missing.Count -eq 0) { break }
        Start-Sleep -Seconds 2
    } while ((Get-Date) -lt $deadline)
    if (-not $ready -or $missing.Count -gt 0) { throw 'Локальные прокси не готовы. Проверьте .local/*forward-error.log.' }
    Start-Process 'http://localhost:8080'
    Write-Host 'Collaby: http://localhost:8080. Пароли: .local/rendered/local.json. Почта: http://localhost:8025.'
} catch {
    Write-Host $_.Exception.Message -ForegroundColor Red
    exit 1
} finally { $mutex.ReleaseMutex();$mutex.Dispose() }
