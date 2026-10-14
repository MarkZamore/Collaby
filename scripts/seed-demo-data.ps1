param(
    [string]$BaseUrl     = "http://localhost",
    [string]$Email       = "",
    [string]$Password    = "",
    [string]$ProjectName = ""
)

$ErrorActionPreference = "Stop"

function Invoke-Api {
    param(
        [string]$Method,
        [string]$Path,
        [object]$Body  = $null,
        [string]$Token = ""
    )
    $uri     = "$BaseUrl$Path"
    $headers = @{ "Content-Type" = "application/json" }
    if ($Token -ne "") { $headers["Authorization"] = "Bearer $Token" }
    $params = @{ Method = $Method; Uri = $uri; Headers = $headers; ErrorAction = "Stop" }
    if ($null -ne $Body) { $params["Body"] = ($Body | ConvertTo-Json -Depth 10 -Compress) }
    try {
        return Invoke-RestMethod @params
    }
    catch {
        $status  = "?"
        $message = $_.Exception.Message
        if ($null -ne $_.Exception.Response) { $status = [int]$_.Exception.Response.StatusCode }
        if ($null -ne $_.ErrorDetails -and $null -ne $_.ErrorDetails.Message) { $message = $_.ErrorDetails.Message }
        Write-Host "  [HTTP $status] $Method $Path -- $message" -ForegroundColor Red
        throw
    }
}

function Write-Step { param([string]$Msg) Write-Host ""; Write-Host ">> $Msg" -ForegroundColor Cyan }
function Write-Ok   { param([string]$Msg) Write-Host "  OK: $Msg" -ForegroundColor Green }
function Write-Info { param([string]$Msg) Write-Host "   . $Msg"  -ForegroundColor DarkGray }

if ($Email -eq "")    { $Email    = Read-Host "Email" }
if ($Password -eq "") {
    $secPwd   = Read-Host "Password" -AsSecureString
    $bstr     = [System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($secPwd)
    $Password = [System.Runtime.InteropServices.Marshal]::PtrToStringAuto($bstr)
}

# --- Login ---
Write-Step "Login ($Email)"
$loginResult = Invoke-Api -Method POST -Path "/api/auth/login" -Body @{ emailOrUsername = $Email; password = $Password }
$token = $null
if ($null -ne $loginResult.accessToken -and $loginResult.accessToken -ne "") { $token = $loginResult.accessToken }
elseif ($null -ne $loginResult.token   -and $loginResult.token -ne "")        { $token = $loginResult.token }
elseif ($null -ne $loginResult.access_token -and $loginResult.access_token -ne "") { $token = $loginResult.access_token }
if ($null -eq $token) { Write-Host "Failed to get token" -ForegroundColor Red; exit 1 }
Write-Ok "Token received"

# --- Bootstrap ---
Write-Step "Bootstrap"
$bootstrap  = Invoke-Api -Method GET -Path "/api/management/bootstrap" -Token $token
$workspaces = $bootstrap.workspaces
if ($null -eq $workspaces -or $workspaces.Count -eq 0) { Write-Host "No workspaces found" -ForegroundColor Yellow; exit 1 }
$workspace   = $workspaces[0]
$workspaceId = $workspace.id
Write-Ok "Workspace: '$($workspace.name)'"

# --- Project ---
Write-Step "Select project"
$projects = Invoke-Api -Method GET -Path "/api/management/workspaces/$workspaceId/projects" -Token $token
if ($null -eq $projects -or $projects.Count -eq 0) { Write-Host "No projects found" -ForegroundColor Yellow; exit 1 }
$project = $null
if ($ProjectName -ne "") {
    foreach ($p in $projects) { if ($p.name -eq $ProjectName) { $project = $p; break } }
    if ($null -eq $project) {
        $names = ($projects | ForEach-Object { $_.name }) -join ", "
        Write-Host "Project '$ProjectName' not found. Available: $names" -ForegroundColor Yellow; exit 1
    }
} else { $project = $projects[0] }
$projectId = $project.id
Write-Ok "Project: '$($project.name)'"

# --- Statuses ---
Write-Step "Loading statuses"
$statuses = Invoke-Api -Method GET -Path "/api/management/projects/$projectId/statuses" -Token $token
if ($null -eq $statuses -or $statuses.Count -eq 0) { Write-Host "No statuses" -ForegroundColor Yellow; exit 1 }
$statusMap = @{}
foreach ($s in $statuses) { $statusMap[$s.name] = $s.id; Write-Info "Status: '$($s.name)'" }

# Assign statuses by index order: 0=todo, 1=in-progress, 2=done, 3+=planned
$todoId   = $statuses[0].id
$inProgId = if ($statuses.Count -gt 1) { $statuses[1].id } else { $statuses[0].id }
$doneId   = if ($statuses.Count -gt 2) { $statuses[2].id } else { $statuses[0].id }
$planId   = if ($statuses.Count -gt 3) { $statuses[3].id } else { $statuses[0].id }
Write-Ok "Statuses mapped"

# --- Members ---
Write-Step "Loading members"
$memberIds = @()
try {
    $members   = Invoke-Api -Method GET -Path "/api/management/workspaces/$workspaceId/members" -Token $token
    $memberIds = @($members | Where-Object { $null -ne $_.userId } | ForEach-Object { $_.userId })
    Write-Ok "Members: $($memberIds.Count)"
} catch { throw "Failed to load workspace members for seed run." }

function Pick-Member { param([int]$Index=0); if ($memberIds.Count -gt 0) { return $memberIds[$Index % $memberIds.Count] }; return $null }

function Move-TaskStatus {
    param(
        [string]$TaskId,
        [string]$StatusId
    )

    if ([string]::IsNullOrWhiteSpace($TaskId) -or [string]::IsNullOrWhiteSpace($StatusId)) {
        return
    }

    Invoke-Api -Method PUT -Path "/api/management/tasks/$TaskId/move" -Token $token -Body @{ statusId = $StatusId; order = 0 } | Out-Null
}

function Add-TaskComment {
    param(
        [string]$TaskId,
        [string]$Text
    )

    if ([string]::IsNullOrWhiteSpace($TaskId) -or [string]::IsNullOrWhiteSpace($Text)) {
        return
    }

    Invoke-Api -Method POST -Path "/api/management/tasks/$TaskId/comments" -Token $token -Body @{ text = $Text } | Out-Null
}

function Save-AiContext {
    param(
        [string]$WorkspaceId,
        [string]$ProjectId,
        [string]$Title,
        [string]$Content,
        [string]$Kind = "approved-summary",
        [string]$Visibility = "hidden",
        [string]$TaskId = "",
        [string]$SourceRef = ""
    )

    $body = @{
        workspaceId = $WorkspaceId
        projectId   = $ProjectId
        title       = $Title
        content     = $Content
        kind        = $Kind
        status      = "approved"
        visibility  = $Visibility
    }
    if (-not [string]::IsNullOrWhiteSpace($TaskId)) {
        $body["taskId"] = $TaskId
    }
    if (-not [string]::IsNullOrWhiteSpace($SourceRef)) {
        $body["sourceRef"] = $SourceRef
    }

    Invoke-Api -Method POST -Path "/api/ai/mentor/context" -Token $token -Body $body | Out-Null
}

function New-TaskSubtask {
    param(
        [string]$TaskId,
        [string]$Title
    )

    if ([string]::IsNullOrWhiteSpace($TaskId) -or [string]::IsNullOrWhiteSpace($Title)) {
        return $null
    }

    return Invoke-Api -Method POST -Path "/api/management/tasks/$TaskId/subtasks" -Token $token -Body @{ title = $Title }
}

function Complete-Subtask {
    param(
        [string]$SubtaskId,
        [string]$Title
    )

    if ([string]::IsNullOrWhiteSpace($SubtaskId)) {
        return
    }

    Invoke-Api -Method PUT -Path "/api/management/subtasks/$SubtaskId" -Token $token -Body @{ title = $Title; isCompleted = $true } | Out-Null
}

# --- Labels ---
Write-Step "Creating labels"
$labelDefs = @(
    @{ name="Bug";         color="#ef4444" },
    @{ name="Feature";     color="#8b5cf6" },
    @{ name="Refactoring"; color="#f59e0b" },
    @{ name="Mobile";      color="#06b6d4" },
    @{ name="Security";    color="#dc2626" },
    @{ name="Release";     color="#10b981" },
    @{ name="Analytics";   color="#6366f1" },
    @{ name="Performance"; color="#f97316" }
)
$labels = @{}
foreach ($ld in $labelDefs) {
    try {
        $lb = Invoke-Api -Method POST -Path "/api/management/projects/$projectId/labels" -Token $token -Body @{ name=$ld.name; color=$ld.color }
        $labels[$ld.name] = $lb.id; Write-Ok "Label '$($ld.name)'"
    } catch { throw "Failed to create label '$($ld.name)'." }
}
try {
    $existing = Invoke-Api -Method GET -Path "/api/management/projects/$projectId/labels" -Token $token
    foreach ($el in $existing) { if (-not $labels.ContainsKey($el.name)) { $labels[$el.name] = $el.id } }
} catch { throw "Failed to re-read project labels after label creation." }

# --- New-Task helper ---
$firstStatusId = ($statuses | Select-Object -First 1).id

function New-Task {
    param(
        [string]$Title, [string]$Desc="", [int]$Priority = 1,
        [string]$StatusId, $AssigneeId=$null, [int]$DueDays=0,
        [string[]]$LabelNames=@(), [string[]]$SubTasks=@(), [string[]]$Comments=@()
    )
    $body = @{ title=$Title; description=$Desc; priority=$Priority }
    if ($null -ne $AssigneeId) { $body["assigneeId"] = $AssigneeId }
    if ($DueDays -ne 0) { $body["dueDate"] = (Get-Date).AddDays($DueDays).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ") }

    $task   = Invoke-Api -Method POST -Path "/api/management/projects/$projectId/tasks" -Token $token -Body $body
    $taskId = $task.id
    $script:LastCreatedTaskId = $taskId
    Write-Ok "Task: '$Title'"

    if ($StatusId -ne $firstStatusId) {
        Invoke-Api -Method PUT -Path "/api/management/tasks/$taskId/move" -Token $token -Body @{ statusId=$StatusId; order=0 } | Out-Null
        Write-Info "   Status moved"
    }
    foreach ($lname in $LabelNames) {
        if ($labels.ContainsKey($lname)) {
            Invoke-Api -Method POST -Path "/api/management/tasks/$taskId/labels" -Token $token -Body @{ labelId=$labels[$lname] } | Out-Null
            Write-Info "   Label: $lname"
        }
    }
    foreach ($sub in $SubTasks) {
        Invoke-Api -Method POST -Path "/api/management/tasks/$taskId/subtasks" -Token $token -Body @{ title=$sub } | Out-Null
        Write-Info "   Subtask: $sub"
    }
    foreach ($c in $Comments) {
        Invoke-Api -Method POST -Path "/api/management/tasks/$taskId/comments" -Token $token -Body @{ text=$c } | Out-Null
        Write-Info "   Comment added"
    }
    return $task
}

# --- Tasks ---
Write-Step "Creating tasks"

# TODO
New-Task -Title "Setup CI/CD pipeline for production" `
    -Desc "GitHub CI/CD with checks and builds on push. Stages: build, test, deploy." `
    -Priority 2 -StatusId $todoId -AssigneeId (Pick-Member 0) -DueDays 7 `
    -LabelNames @("Release") `
    -SubTasks @("Write .github/workflows/ci.yml","Configure Docker registry","Add health-check after deploy","Document deploy process") `
    -Comments @("Check which variables are needed in GitHub Settings CI/CD.","Pipeline template is in branch ci-setup.")
$ciPipelineTaskId = $script:LastCreatedTaskId

New-Task -Title "Write unit tests for AuthService" `
    -Desc "Cover key scenarios: successful login, wrong password, expired token, refresh." `
    -Priority 1 -StatusId $todoId -AssigneeId (Pick-Member 1) -DueDays 5 `
    -LabelNames @("Bug","Security") `
    -SubTasks @("Test: successful login","Test: wrong password -> 401","Test: refresh token","Test: expired refresh -> logout") `
    -Comments @("xUnit + FluentAssertions. DB stub via InMemory EF.","Mock IEmailService to avoid real emails during tests.")

New-Task -Title "Implement My Tasks page" `
    -Desc "Show tasks from all workspaces where current user is assignee. Group by project." `
    -Priority 1 -StatusId $todoId -AssigneeId (Pick-Member 0) -DueDays 10 `
    -LabelNames @("Feature","Mobile") `
    -SubTasks @("Design in Figma","API endpoint GET /me/inbox","TaskCard component","Filter by priority") `
    -Comments @("Design is ready in Figma - link in task description.")

New-Task -Title "Fix memory leak in WebSocket Hub" `
    -Desc "SignalR Groups not cleaned when tab closes. Need cleanup in OnDisconnectedAsync." `
    -Priority 3 -StatusId $todoId -AssigneeId (Pick-Member 1) -DueDays 2 `
    -LabelNames @("Bug","Performance") `
    -SubTasks @("Reproduce bug locally","Add cleanup in OnDisconnectedAsync","Write load test") `
    -Comments @("Repro: open 10 tabs, close them, watch memory via dotnet-counters.","CancellationToken may be needed in Hub background tasks.")
$websocketHubTaskId = $script:LastCreatedTaskId

New-Task -Title "Add pagination to task list" `
    -Desc "Cursor-based pagination when board has many tasks." `
    -Priority 0 -StatusId $todoId -DueDays 15 `
    -LabelNames @("Feature","Performance") `
    -SubTasks @("Add cursor/limit params to GET /tasks","Frontend IntersectionObserver","Index on Order column in DB") `
    -Comments @("Threshold: tasks > 100 enables pagination.")

# IN PROGRESS
New-Task -Title "Unsplash API integration for board backgrounds" `
    -Desc "Search and pick background images for boards via Unsplash. Cache results in Redis." `
    -Priority 0 -StatusId $inProgId -AssigneeId (Pick-Member 0) -DueDays 14 `
    -LabelNames @("Feature","Refactoring") `
    -SubTasks @("Register app in Unsplash","Implement IUnsplashImageSearchService","Redis cache TTL 1h","Image search UI component","Lazy loading thumbnails") `
    -Comments @("UNSPLASH_ACCESS_KEY in .env. Rate limit: 50 req/h.","Attribution per Unsplash Guidelines is required.")

New-Task -Title "Refactor ManagementDbContext" `
    -Desc "Split DbContext into IEntityTypeConfiguration per entity. Fluent API over annotations." `
    -Priority 1 -StatusId $inProgId -AssigneeId (Pick-Member 1) -DueDays 6 `
    -LabelNames @("Refactoring","Performance") `
    -SubTasks @("IEntityTypeConfiguration for Task","IEntityTypeConfiguration for Project","IEntityTypeConfiguration for Workspace","Move indexes","Run all tests") `
    -Comments @("After changes: dotnet ef migrations add RefactorDbContext")

New-Task -Title "Add Prometheus metrics" `
    -Desc "Export HTTP latency, active SignalR connections, email queue metrics." `
    -Priority 0 -StatusId $inProgId -AssigneeId (Pick-Member 0) -DueDays 21 `
    -LabelNames @("Analytics","Performance") `
    -SubTasks @("Add prometheus-net.AspNetCore","Configure /metrics endpoint","Metric: active WS connections","Grafana dashboard") `
    -Comments @("prometheus-net 8.x is compatible with .NET 10.")
$prometheusTaskId = $script:LastCreatedTaskId

# DONE
New-Task -Title "Google OAuth login" `
    -Desc "Sign in with Google. Create profile if user is new." `
    -Priority 2 -StatusId $doneId -AssigneeId (Pick-Member 0) -DueDays -3 `
    -LabelNames @("Feature","Security") `
    -SubTasks @("Configure Google Cloud Console","Implement callback endpoint","Create profile on first login","Test: new user via Google","Test: existing user") `
    -Comments @("Client ID / Secret added to .env and Kubernetes.","Avatar is fetched correctly on first login.","Passed code review. Merged to main.")

New-Task -Title "Rate limiting for auth endpoints" `
    -Desc "Max 10 attempts per minute per IP on /auth/login and /auth/register." `
    -Priority 2 -StatusId $doneId -AssigneeId (Pick-Member 1) -DueDays -7 `
    -LabelNames @("Security","Performance") `
    -SubTasks @("Implement RateLimitMiddleware","Store counters in Redis","Return 429 Too Many Requests","Add Retry-After header") `
    -Comments @("IP blocked for 60s after 10 attempts. Verified via curl.","Logs confirm correct behavior.")

New-Task -Title "ManagementService initial structure" `
    -Desc "EF Core + PostgreSQL, CRUD for Workspace and Project." `
    -Priority 2 -StatusId $doneId -AssigneeId (Pick-Member 0) -DueDays -14 `
    -LabelNames @("Release","Refactoring") `
    -SubTasks @("Create .csproj with dependencies","DbContext + migrations","WorkspacesController","ProjectsController","Kubernetes integration") `
    -Comments @("All endpoints available via Swagger UI.","Migrations run automatically on startup in Dev.")

# PLANNED
if ($planId -ne $todoId) {
    New-Task -Title "AI assistant: task suggestions" `
        -Desc "Suggest next steps based on project history. OpenAI GPT-4 integration." `
        -Priority 0 -StatusId $planId -AssigneeId (Pick-Member 0) -DueDays 60 `
        -LabelNames @("Feature","Analytics") `
        -SubTasks @("Model selection: GPT-4 vs Claude","UX chat design","Backend streaming endpoint","Frontend ChatPanel component","Safety filters") `
        -Comments @("Waiting for feedback from early users.","Consider Vercel AI SDK for streaming.")

    New-Task -Title "Mobile app React Native" `
        -Desc "iOS and Android. MVP: tasks, status change, comments, push notifications." `
        -Priority 1 -StatusId $planId -AssigneeId (Pick-Member 1) -DueDays 90 `
        -LabelNames @("Mobile","Feature") `
        -SubTasks @("Init Expo project","Auth flow: login + tokens","My Tasks screen","Board screen","Push notifications") `
        -Comments @("React Native + Expo. Start with iOS simulator.")

    New-Task -Title "Burndown Chart for sprints" `
        -Desc "Sprint progress visualization - burndown and velocity chart." `
        -Priority 0 -StatusId $planId -DueDays 45 `
        -LabelNames @("Analytics","Feature") `
        -SubTasks @("Sprint data aggregation endpoint","Recharts component","Export to PNG / PDF") `
        -Comments @("Recharts is already used elsewhere in the project.")
}

Write-Step "Creating analytics drill"
New-Task -Title "Analytics drill: release board signal" `
    -Desc "Deterministic activity for AnalyticsService demo aggregation." `
    -Priority 2 -StatusId $todoId -AssigneeId (Pick-Member 0) -DueDays 3 `
    -LabelNames @("Analytics", "Release")
$analyticsDrillTaskId = $script:LastCreatedTaskId
Write-Info "Analytics drill task: $analyticsDrillTaskId"

Add-TaskComment -TaskId $analyticsDrillTaskId -Text "Analytics drill comment: release checklist started."
Add-TaskComment -TaskId $analyticsDrillTaskId -Text "Analytics drill comment: release checklist verified."

$subtaskA = New-TaskSubtask -TaskId $analyticsDrillTaskId -Title "Analytics drill subtask: verify metrics ingestion"
Complete-Subtask -SubtaskId $subtaskA.id -Title "Analytics drill subtask: verify metrics ingestion"
Write-Info "Completed subtask: verify metrics ingestion"

$subtaskB = New-TaskSubtask -TaskId $analyticsDrillTaskId -Title "Analytics drill subtask: verify dashboard refresh"
Complete-Subtask -SubtaskId $subtaskB.id -Title "Analytics drill subtask: verify dashboard refresh"
Write-Info "Completed subtask: verify dashboard refresh"

if ($doneId -ne $todoId) {
    Move-TaskStatus -TaskId $analyticsDrillTaskId -StatusId $doneId
    Write-Info "Moved analytics drill task to done"
}

if ($inProgId -ne $doneId -and $inProgId -ne $todoId) {
    Move-TaskStatus -TaskId $analyticsDrillTaskId -StatusId $inProgId
    Write-Info "Reopened analytics drill task into in-progress"
}

if ($doneId -ne $inProgId) {
    Move-TaskStatus -TaskId $analyticsDrillTaskId -StatusId $doneId
    Write-Info "Completed analytics drill task again"
}

Write-Step "Creating AI hidden context"
$mentorAcceptanceSummary = @"
Контекст релиза для публичного acceptance:
- Если участник спрашивает про deploy-launch, сначала опирайтесь на внутренний task-context проекта.
- Главные внутренние темы этого демо: CI/CD pipeline, memory leak в WebSocket Hub и релизные метрики.
- Внешние источники можно подключать только после краткого внутреннего ответа и с явным указанием источника.
"@
Save-AiContext -WorkspaceId $workspaceId -ProjectId $projectId -Title "[DEMO] Публичный mentor acceptance" -Content $mentorAcceptanceSummary -Visibility "hidden" -SourceRef "seed:mentor-acceptance"
Write-Info "Saved hidden context: mentor acceptance"

$releaseFocusSummary = @"
Релизный фокус проекта:
- Перед релизом нужно стабилизировать deploy-launch и проверить шаги cutover.
- Memory leak в WebSocket Hub считается релизным блокером до подтверждения cleanup в OnDisconnectedAsync.
- Если вопрос касается приоритета работ, сначала называйте CI/CD и WebSocket Hub как основные внутренние задачи.
"@
Save-AiContext -WorkspaceId $workspaceId -ProjectId $projectId -Title "Релизный фокус: WebSocket Hub и CI/CD" -Content $releaseFocusSummary -Visibility "task-context" -TaskId $websocketHubTaskId -SourceRef "seed:release-focus"
Write-Info "Saved task-context: release focus"

$ciChecklistSummary = @"
CI/CD checklist для production:
- Проверить build, test и deploy-launch в GitHub CI/CD pipeline.
- После deploy-launch перепроверить health-check и доступность panel routes.
- Если участник спрашивает про релиз, напоминать о rollback plan и post-deploy smoke.
"@
Save-AiContext -WorkspaceId $workspaceId -ProjectId $projectId -Title "CI/CD checklist для production" -Content $ciChecklistSummary -Visibility "task-context" -TaskId $ciPipelineTaskId -SourceRef "seed:ci-checklist"
Write-Info "Saved task-context: ci checklist"

$metricsSummary = @"
Контекст метрик и analytics drill:
- Prometheus metrics и analytics drill нужны для подтверждения, что релиз не деградировал по latency и dashboard refresh.
- Если вопрос касается наблюдаемости, сначала опирайтесь на внутренние задачи про metrics ingestion, dashboard refresh и release checklist.
- После ответа перечисляйте связанные внутренние источники, а внешние статьи добавляйте только как дополнение.
"@
Save-AiContext -WorkspaceId $workspaceId -ProjectId $projectId -Title "Метрики релиза и analytics drill" -Content $metricsSummary -Visibility "task-context" -TaskId $prometheusTaskId -SourceRef "seed:metrics-focus"
Write-Info "Saved task-context: metrics focus"

Write-Host ""
Write-Host "===================================================" -ForegroundColor Cyan
Write-Host "  Done! Demo data loaded successfully." -ForegroundColor Green
Write-Host "  Workspace : $($workspace.name)" -ForegroundColor Green
Write-Host "  Project   : $($project.name)" -ForegroundColor Green
Write-Host "  Labels    : $($labelDefs.Count)" -ForegroundColor Green
Write-Host "===================================================" -ForegroundColor Cyan
