$ErrorActionPreference = 'Stop'
$env:PYTHONUTF8 = '1'
$utf8 = New-Object System.Text.UTF8Encoding($false)
[Console]::OutputEncoding = $utf8
[Console]::InputEncoding = $utf8
$OutputEncoding = $utf8
foreach ($name in @('local.ps1','prepare-machine.ps1')) {
    $errors = $null
    $tokens = $null
    $ast = [System.Management.Automation.Language.Parser]::ParseFile((Join-Path $PSScriptRoot $name), [ref]$tokens, [ref]$errors)
    if ($errors.Count -gt 0) { throw ($errors | Out-String) }
    if ($name -eq 'local.ps1') { $launcher = $ast }
}
# Извлекаем только функции; Kubernetes и Docker при проверке не запускаются.
foreach ($function in $launcher.FindAll({param($node) $node -is [System.Management.Automation.Language.FunctionDefinitionAst]}, $false)) {
    Invoke-Expression $function.Extent.Text
}
function Get-CimInstance { param($ClassName,$Filter,$ErrorAction) return $script:fakeCim }
function Get-Process { param($Id,$ErrorAction) return $script:fakeProcess }
$stamp = [datetime]'2026-10-14T00:00:00Z'
$script:fakeCim = [pscustomobject]@{Name='kubectl.exe';CommandLine='kubectl --context docker-desktop port-forward service/frontend 8080:80'}
$script:fakeProcess = [pscustomobject]@{Id=123;StartTime=$stamp}
$entry = @{pid=123;started=$stamp.ToUniversalTime().Ticks.ToString();marker='service/frontend 8080:80'}
if (-not (Owned-Process $entry 'kubectl.exe')) { throw 'Процесс с совпадающими PID, временем и командой должен принадлежать launcher.' }
$entry.started='1'
if (Owned-Process $entry 'kubectl.exe') { throw 'Повторно использованный PID не должен принадлежать launcher.' }
$entry.started=$stamp.ToUniversalTime().Ticks.ToString()
$entry.marker='service/frontend 8081:80'
if (Owned-Process $entry 'kubectl.exe') { throw 'Другая команда не должна принадлежать launcher.' }
$entry.marker='service/frontend 8080:80'
if (Owned-Process $entry 'powershell.exe') { throw 'Другой executable не должен принадлежать launcher.' }
$fixture = Join-Path ([IO.Path]::GetTempPath()) ('collaby-launcher-test-' + [guid]::NewGuid().ToString('N') + '.json')
try {
    '[{"name":"frontend","image":"collaby/frontend:abc"},{"name":"controller","image":"collaby/controller:abc"}]' | Set-Content $fixture -Encoding UTF8
    $image = Controller-Image $fixture
    if ($image -ne 'collaby/controller:abc') { throw 'Контроллер не выбран из JSON-массива в PowerShell 5.1.' }
    $job = @{spec=@{template=@{spec=@{containers=@(@{image=$image})}}}}
    $roundtrip = $job | ConvertTo-Json -Depth 12 | ConvertFrom-Json
    if ($roundtrip.spec.template.spec.containers[0].image -ne $image) { throw 'Образ потерян при сериализации Job.' }
    if (@(Read-State $fixture).Count -ne 2) { throw 'Read-State должен разворачивать массив состояния.' }
    foreach ($invalid in @('[]','[{"name":"controller","image":""}]','[{"name":"controller","image":"x"},{"name":"controller","image":"y"}]')) {
        $invalid | Set-Content $fixture -Encoding UTF8
        $rejected = $false
        try { Controller-Image $fixture | Out-Null } catch { $rejected = $true }
        if (-not $rejected) { throw 'Некорректный controller image не отклонён.' }
    }
} finally { Remove-Item -LiteralPath $fixture -Force -ErrorAction SilentlyContinue }
$unicode = & python -c "print('Проверка кодировки Collaby')"
if ($LASTEXITCODE -ne 0 -or $unicode -ne 'Проверка кодировки Collaby') { throw 'UTF-8 stdout дочернего Python повреждён.' }
$roundtrip = '{"текст":"Проверка кодировки Collaby"}' | & python -c "import sys,json; print(json.load(sys.stdin)['текст'])"
if ($LASTEXITCODE -ne 0 -or $roundtrip -ne 'Проверка кодировки Collaby') { throw 'UTF-8 JSON stdin дочернего Python повреждён.' }
Native-Output 'python' @('-c','import sys; sys.exit(7)') | Out-Null
if ($LASTEXITCODE -ne 7) { throw 'Native-Output потерял код ошибки дочернего процесса.' }
$script:diagnosticCommands=@()
function kubectl { $script:diagnosticCommands += ($args -join ' ') }
$diagnostics = Show-Diagnostics 'local-apply-test' 6>&1 | Out-String
if ($script:diagnosticCommands.Count -ne 3 -or -not ($script:diagnosticCommands -match 'job-name=local-apply-test')) { throw 'Диагностика должна читать workload/Job pods и Warning events.' }
if ($diagnostics -notmatch 'OOMKilled' -or $diagnostics -notmatch 'ImagePullBackOff' -or $diagnostics -notmatch 'Загрузка AI возобновляется' -or $diagnostics -notmatch 'logs job/local-apply-test') { throw 'В диагностике отсутствует причина ошибки или команда для расследования.' }
$script:nodeAttempts = 0
$script:missingContext = $false
$script:neverReady = $false
function Native-Output {
    param($Executable,$Arguments)
    if ($Arguments -contains 'get-contexts') {
        $script:LASTEXITCODE = 0
        if ($script:missingContext) { return 'other-context' }
        return 'docker-desktop'
    }
    $script:nodeAttempts++
    if ($script:neverReady -or $script:nodeAttempts -lt 3) { $script:LASTEXITCODE = 1; return $null }
    $script:LASTEXITCODE = 0
    return 'node/docker-desktop'
}
$node = Wait-Kubernetes -TimeoutSeconds 5 -PollSeconds 0
if ($node -ne 'node/docker-desktop' -or $script:nodeAttempts -ne 3) { throw 'Launcher не переждал гонку запуска Kubernetes после готовности Linux engine.' }
$script:neverReady = $true
$rejected = $false
try { Wait-Kubernetes -TimeoutSeconds 0 -PollSeconds 0 | Out-Null } catch { $rejected = $_.Exception.Message -match 'не запустился' }
if (-not $rejected) { throw 'Неготовый Kubernetes должен давать ограниченную по времени диагностику.' }
$script:missingContext = $true
$rejected = $false
try { Wait-Kubernetes -TimeoutSeconds 0 -PollSeconds 0 | Out-Null } catch { $rejected = $_.Exception.Message -match 'Контекст docker-desktop отсутствует' }
if (-not $rejected) { throw 'Отсутствующий Kubernetes context не должен приниматься за гонку запуска.' }
Write-Host 'PowerShell 5.1: синтаксис, владелец процесса, JSON controller/Job image, UTF-8 native stdin/stdout, диагностика и гонка старта Kubernetes проверены.'
