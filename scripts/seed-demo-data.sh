#!/bin/bash

set -e

# Default parameters
BASE_URL="${1:-http://localhost}"
EMAIL="${2:-}"
PASSWORD="${3:-}"
PROJECT_NAME="${4:-}"

# Color codes
RED='\033[0;31m'
GREEN='\033[0;32m'
CYAN='\033[0;36m'
GRAY='\033[0;90m'
NC='\033[0m' # No Color

# Helper functions
invoke_api() {
    local method=$1
    local path=$2
    local body=$3
    local token=$4

    local uri="$BASE_URL$path"
    local headers="-H 'Content-Type: application/json'"

    if [ -n "$token" ]; then
        headers="$headers -H 'Authorization: Bearer $token'"
    fi

    local cmd="curl -s -X $method '$uri' $headers"

    if [ -n "$body" ]; then
        cmd="$cmd -d '$body'"
    fi

    local response
    response=$(eval "$cmd")

    if [ -z "$response" ]; then
        echo "Error: Empty response from $method $path" >&2
        return 1
    fi

    if echo "$response" | jq empty 2>/dev/null; then
        echo "$response"
    else
        echo "Error: Invalid JSON response from $method $path" >&2
        echo "$response" >&2
        return 1
    fi
}

write_step() {
    echo ""
    echo -e "${CYAN}>> $1${NC}"
}

write_ok() {
    echo -e "${GREEN}  OK: $1${NC}"
}

write_info() {
    echo -e "${GRAY}   . $1${NC}"
}

# Prompt for credentials if not provided
if [ -z "$EMAIL" ]; then
    read -p "Email: " EMAIL
fi

if [ -z "$PASSWORD" ]; then
    read -sp "Password: " PASSWORD
    echo ""
fi

# --- Login ---
write_step "Login ($EMAIL)"
LOGIN_RESULT=$(invoke_api POST "/api/auth/login" "{\"emailOrUsername\":\"$EMAIL\",\"password\":\"$PASSWORD\"}")

TOKEN=$(echo "$LOGIN_RESULT" | jq -r '.accessToken // .token // .access_token // empty' 2>/dev/null)
if [ -z "$TOKEN" ] || [ "$TOKEN" = "null" ]; then
    echo -e "${RED}Failed to get token${NC}" >&2
    echo "Response was: $LOGIN_RESULT" >&2
    exit 1
fi
write_ok "Token received"

# --- Bootstrap ---
write_step "Bootstrap"
BOOTSTRAP=$(invoke_api GET "/api/management/bootstrap" "" "$TOKEN")

WORKSPACES=$(echo "$BOOTSTRAP" | jq -r '.workspaces // []' 2>/dev/null)
WORKSPACE_COUNT=$(echo "$WORKSPACES" | jq 'length' 2>/dev/null)

if [ "$WORKSPACE_COUNT" -eq 0 ]; then
    echo -e "${RED}No workspaces found${NC}" >&2
    exit 1
fi

WORKSPACE=$(echo "$WORKSPACES" | jq '.[0]' 2>/dev/null)
WORKSPACE_ID=$(echo "$WORKSPACE" | jq -r '.id' 2>/dev/null)
WORKSPACE_NAME=$(echo "$WORKSPACE" | jq -r '.name' 2>/dev/null)
write_ok "Workspace: '$WORKSPACE_NAME'"

# --- Project ---
write_step "Select project"
PROJECTS=$(invoke_api GET "/api/management/workspaces/$WORKSPACE_ID/projects" "" "$TOKEN")
PROJECT_COUNT=$(echo "$PROJECTS" | jq 'length' 2>/dev/null)

if [ "$PROJECT_COUNT" -eq 0 ]; then
    echo -e "${RED}No projects found${NC}" >&2
    exit 1
fi

PROJECT=null
if [ -n "$PROJECT_NAME" ]; then
    PROJECT=$(echo "$PROJECTS" | jq ".[] | select(.name == \"$PROJECT_NAME\")" 2>/dev/null)
    if [ -z "$PROJECT" ] || [ "$PROJECT" = "null" ]; then
        NAMES=$(echo "$PROJECTS" | jq -r '.[].name' 2>/dev/null | tr '\n' ', ')
        echo -e "${RED}Project '$PROJECT_NAME' not found. Available: $NAMES${NC}" >&2
        exit 1
    fi
else
    PROJECT=$(echo "$PROJECTS" | jq '.[0]' 2>/dev/null)
fi

PROJECT_ID=$(echo "$PROJECT" | jq -r '.id' 2>/dev/null)
PROJECT_NAME=$(echo "$PROJECT" | jq -r '.name' 2>/dev/null)
write_ok "Project: '$PROJECT_NAME'"

# --- Statuses ---
write_step "Loading statuses"
STATUSES=$(invoke_api GET "/api/management/projects/$PROJECT_ID/statuses" "" "$TOKEN")
STATUS_COUNT=$(echo "$STATUSES" | jq 'length' 2>/dev/null)

if [ "$STATUS_COUNT" -eq 0 ]; then
    echo -e "${RED}No statuses${NC}" >&2
    exit 1
fi

TODO_ID=$(echo "$STATUSES" | jq -r '.[0].id' 2>/dev/null)
IN_PROG_ID=$(echo "$STATUSES" | jq -r ".[1].id // .[0].id" 2>/dev/null)
DONE_ID=$(echo "$STATUSES" | jq -r ".[2].id // .[0].id" 2>/dev/null)
PLAN_ID=$(echo "$STATUSES" | jq -r ".[3].id // .[0].id" 2>/dev/null)

echo "$STATUSES" | jq -r '.[] | .name' 2>/dev/null | while read -r status; do
    write_info "Status: '$status'"
done
write_ok "Statuses mapped"

# --- Members ---
write_step "Loading members"
MEMBER_IDS=()
MEMBERS=$(invoke_api GET "/api/management/workspaces/$WORKSPACE_ID/members" "" "$TOKEN" 2>/dev/null || echo '[]')
MEMBER_COUNT=$(echo "$MEMBERS" | jq 'length' 2>/dev/null)

if [ "$MEMBER_COUNT" -gt 0 ]; then
    MEMBER_IDS=($(echo "$MEMBERS" | jq -r '.[] | select(.userId != null) | .userId' 2>/dev/null))
    write_ok "Members: ${#MEMBER_IDS[@]}"
else
    write_info "Could not load members - tasks will have no assignee"
fi

pick_member() {
    local index=${1:-0}
    if [ ${#MEMBER_IDS[@]} -gt 0 ]; then
        echo "${MEMBER_IDS[$((index % ${#MEMBER_IDS[@]}))]}"
    fi
}

move_task_status() {
    local task_id=$1
    local status_id=$2

    if [ -z "$task_id" ] || [ -z "$status_id" ]; then
        return 1
    fi

    invoke_api PUT "/api/management/tasks/$task_id/move" "{\"statusId\":\"$status_id\",\"order\":0}" "$TOKEN" >/dev/null
}

add_task_comment() {
    local task_id=$1
    local comment_text=$2

    if [ -z "$task_id" ] || [ -z "$comment_text" ]; then
        return 1
    fi

    invoke_api POST "/api/management/tasks/$task_id/comments" "{\"text\":\"$comment_text\"}" "$TOKEN" >/dev/null
}

save_ai_context() {
    local workspace_id=$1
    local project_id=$2
    local title=$3
    local content=$4
    local kind=${5:-approved-summary}
    local visibility=${6:-hidden}
    local task_id=${7:-}
    local source_ref=${8:-}

    local body
    body=$(jq -nc \
        --arg workspaceId "$workspace_id" \
        --arg projectId "$project_id" \
        --arg title "$title" \
        --arg content "$content" \
        --arg kind "$kind" \
        --arg visibility "$visibility" \
        --arg taskId "$task_id" \
        --arg sourceRef "$source_ref" \
        '{
            workspaceId: $workspaceId,
            projectId: $projectId,
            title: $title,
            content: $content,
            kind: $kind,
            status: "approved",
            visibility: $visibility
        }
        + (if $taskId != "" then {taskId: $taskId} else {} end)
        + (if $sourceRef != "" then {sourceRef: $sourceRef} else {} end)')

    invoke_api POST "/api/ai/mentor/context" "$body" "$TOKEN" >/dev/null
}

create_subtask() {
    local task_id=$1
    local title=$2

    if [ -z "$task_id" ] || [ -z "$title" ]; then
        return 1
    fi

    local response
    response=$(invoke_api POST "/api/management/tasks/$task_id/subtasks" "{\"title\":\"$title\"}" "$TOKEN")
    SUBTASK_ID=$(echo "$response" | jq -r '.id // empty' 2>/dev/null)
    [ -n "$SUBTASK_ID" ]
}

complete_subtask() {
    local subtask_id=$1
    local title=$2

    if [ -z "$subtask_id" ]; then
        return 1
    fi

    invoke_api PUT "/api/management/subtasks/$subtask_id" "{\"title\":\"$title\",\"isCompleted\":true}" "$TOKEN" >/dev/null
}

# --- Labels (using JSON file to avoid bash subshell issues) ---
write_step "Creating labels"
: > /tmp/collaby_labels.json
echo "{}" > /tmp/collaby_labels.json

LABEL_DEFS=(
    "Bug|#ef4444"
    "Feature|#8b5cf6"
    "Refactoring|#f59e0b"
    "Mobile|#06b6d4"
    "Security|#dc2626"
    "Release|#10b981"
    "Analytics|#6366f1"
    "Performance|#f97316"
    "UX/UI|#ec4899"
    "Backend|#3b82f6"
    "Frontend|#a855f7"
    "DevOps|#14b8a6"
    "Documentation|#78716c"
    "Urgent|#b91c1c"
)

for label_def in "${LABEL_DEFS[@]}"; do
    IFS='|' read -r name color <<< "$label_def"
    LABEL_RESULT=$(invoke_api POST "/api/management/projects/$PROJECT_ID/labels" "{\"name\":\"$name\",\"color\":\"$color\"}" "$TOKEN" 2>/dev/null || echo '{}')
    LABEL_ID=$(echo "$LABEL_RESULT" | jq -r '.id // empty' 2>/dev/null)
    if [ -n "$LABEL_ID" ] && [ "$LABEL_ID" != "null" ]; then
        LABELS_JSON=$(cat /tmp/collaby_labels.json)
        LABELS_JSON=$(echo "$LABELS_JSON" | jq --arg n "$name" --arg id "$LABEL_ID" '.[$n] = $id')
        echo "$LABELS_JSON" > /tmp/collaby_labels.json
        write_ok "Label '$name'"
    else
        write_info "Label '$name' skipped"
    fi
done

# Fetch and add existing labels
EXISTING_LABELS=$(invoke_api GET "/api/management/projects/$PROJECT_ID/labels" "" "$TOKEN" 2>/dev/null || echo '[]')
LABELS_JSON=$(echo "$EXISTING_LABELS" | jq 'reduce .[] as $label ({}; .[$label.name] = $label.id)' 2>/dev/null || echo "{}")
echo "$LABELS_JSON" > /tmp/collaby_labels.json

FIRST_STATUS_ID=$TODO_ID

# --- New-Task helper ---
new_task() {
    local title=$1
    local desc=$2
    local priority=$3
    local status_id=$4
    local assignee_id=$5
    local due_days=$6
    shift 6
    local label_names=("$@")

    local body="{\"title\":\"$title\",\"description\":\"$desc\",\"priority\":$priority"

    if [ -n "$assignee_id" ] && [ "$assignee_id" != "null" ]; then
        body+=",\"assigneeId\":\"$assignee_id\""
    fi

    if [ "$due_days" -ne 0 ]; then
        local due_date
        due_date=$(date -u -d "$due_days days" +"%Y-%m-%dT%H:%M:%SZ")
        body+=",\"dueDate\":\"$due_date\""
    fi

    body+="}"

    TASK=$(invoke_api POST "/api/management/projects/$PROJECT_ID/tasks" "$body" "$TOKEN")
    TASK_ID=$(echo "$TASK" | jq -r '.id // empty' 2>/dev/null)

    if [ -z "$TASK_ID" ] || [ "$TASK_ID" = "null" ]; then
        write_info "Task: '$title' (Failed to create)"
        return 1
    fi

    NEW_TASK_ID="$TASK_ID"
    write_ok "Task: '$title'"

    if [ "$status_id" != "$FIRST_STATUS_ID" ] && [ -n "$status_id" ]; then
        invoke_api PUT "/api/management/tasks/$TASK_ID/move" "{\"statusId\":\"$status_id\",\"order\":0}" "$TOKEN" >/dev/null 2>&1 || write_info "   Could not move to status"
    fi

    # Load labels from file and add them
    LABELS_JSON=$(cat /tmp/collaby_labels.json 2>/dev/null || echo "{}")

    for label_name in "${label_names[@]}"; do
        label_id=$(echo "$LABELS_JSON" | jq -r ".\"$label_name\" // empty" 2>/dev/null)
        if [ -n "$label_id" ] && [ "$label_id" != "null" ]; then
            invoke_api POST "/api/management/tasks/$TASK_ID/labels" "{\"labelId\":\"$label_id\"}" "$TOKEN" >/dev/null 2>&1 && write_info "   Label: $label_name" || write_info "   Label error: $label_name"
        fi
    done
}

# --- Tasks ---
write_step "Creating tasks"

# ---- TODO ----
new_task "Setup CI/CD pipeline for production" \
    "GitHub CI/CD with checks and builds on push. Stages: build, test, deploy, smoke. Requirements: Docker multi-stage build, rollback plan." \
    2 "$TODO_ID" "$(pick_member 0)" 7 \
    "Release"
CI_PIPELINE_TASK_ID="$NEW_TASK_ID"
add_task_comment "$CI_PIPELINE_TASK_ID" "Need to decide between GitHub runners and GitHub-hosted runners."
add_task_comment "$CI_PIPELINE_TASK_ID" "Let's start with shared runners and migrate later if needed."
create_subtask "$CI_PIPELINE_TASK_ID" "Write Dockerfile for each service" && complete_subtask "$SUBTASK_ID" "Write Dockerfile for each service"
create_subtask "$CI_PIPELINE_TASK_ID" "Configure GitHub CI YAML"
create_subtask "$CI_PIPELINE_TASK_ID" "Setup staging environment"
create_subtask "$CI_PIPELINE_TASK_ID" "Add post-deploy smoke tests"
create_subtask "$CI_PIPELINE_TASK_ID" "Document rollback procedure"

new_task "Write unit tests for AuthService" \
    "Cover key scenarios: successful login, wrong password, expired token, refresh, rate limiting, Google OAuth." \
    1 "$TODO_ID" "$(pick_member 1)" 5 \
    "Security"
add_task_comment "$NEW_TASK_ID" "Current coverage is ~30%. Target: 80%+ on auth endpoints."
create_subtask "$NEW_TASK_ID" "Test login success scenario"
create_subtask "$NEW_TASK_ID" "Test wrong password"
create_subtask "$NEW_TASK_ID" "Test expired token"
create_subtask "$NEW_TASK_ID" "Test refresh flow"
create_subtask "$NEW_TASK_ID" "Test rate limiting"
create_subtask "$NEW_TASK_ID" "Test Google OAuth"

new_task "Implement My Tasks page" \
    "Show tasks from all workspaces where current user is assignee. Group by project, sort by due date." \
    1 "$TODO_ID" "$(pick_member 0)" 10 \
    "Feature"
create_subtask "$NEW_TASK_ID" "API endpoint: GET /api/management/tasks/my"
create_subtask "$NEW_TASK_ID" "Frontend component: MyTasksPage"
create_subtask "$NEW_TASK_ID" "Add filters and sorting"
create_subtask "$NEW_TASK_ID" "Mobile responsive layout"

new_task "Fix memory leak in WebSocket Hub" \
    "SignalR Groups not cleaned when tab closes. Root cause: OnDisconnectedAsync doesn't remove user from all groups. Impact: ~2MB/hour leak." \
    3 "$TODO_ID" "$(pick_member 1)" 2 \
    "Bug" "Performance"
WEBSOCKET_HUB_TASK_ID="$NEW_TASK_ID"
add_task_comment "$WEBSOCKET_HUB_TASK_ID" "Reproduced locally: open 10 tabs, close them, memory doesn't decrease."
add_task_comment "$WEBSOCKET_HUB_TASK_ID" "Redis tracking approach approved. Will store userId -> groupIds in Redis Set."
add_task_comment "$WEBSOCKET_HUB_TASK_ID" "This is a release blocker - must fix before next deployment."
create_subtask "$WEBSOCKET_HUB_TASK_ID" "Add Redis tracking for SignalR groups"
create_subtask "$WEBSOCKET_HUB_TASK_ID" "Implement cleanup in OnDisconnectedAsync"
create_subtask "$WEBSOCKET_HUB_TASK_ID" "Load test with 100 concurrent connections"
create_subtask "$WEBSOCKET_HUB_TASK_ID" "Monitor memory in staging for 24h"

new_task "Add cursor-based pagination to task list" \
    "Large boards with 100+ tasks are slow. Implement cursor-based pagination with infinite scroll." \
    0 "$TODO_ID" "" 15 \
    "Feature" "Performance"
create_subtask "$NEW_TASK_ID" "Backend: cursor-based query"
create_subtask "$NEW_TASK_ID" "API contract: cursor param + hasMore"
create_subtask "$NEW_TASK_ID" "Frontend: IntersectionObserver hook"
create_subtask "$NEW_TASK_ID" "Test with 500 tasks"

new_task "Polish dark mode across all pages" \
    "Audit and fix hardcoded light colors in modals, dropdowns, toasts, charts." \
    0 "$TODO_ID" "$(pick_member 0)" 12 \
    "Feature"
create_subtask "$NEW_TASK_ID" "Audit all components for hardcoded colors"
create_subtask "$NEW_TASK_ID" "Fix modal backgrounds"
create_subtask "$NEW_TASK_ID" "Fix dropdown menus"
create_subtask "$NEW_TASK_ID" "Fix toast notifications"
create_subtask "$NEW_TASK_ID" "Update chart color palette"

new_task "Design email notification templates" \
    "HTML email templates: welcome, password reset, task assigned, weekly digest. Use MJML." \
    0 "$TODO_ID" "" 20 \
    "Feature"
create_subtask "$NEW_TASK_ID" "Design base template layout"
create_subtask "$NEW_TASK_ID" "Welcome email"
create_subtask "$NEW_TASK_ID" "Password reset email"
create_subtask "$NEW_TASK_ID" "Task assignment email"
create_subtask "$NEW_TASK_ID" "Weekly digest email"

# ---- IN PROGRESS ----
new_task "Unsplash API integration for board backgrounds" \
    "Search and set board backgrounds via Unsplash. Cache in Redis (TTL 1h). Attribution required." \
    0 "$IN_PROG_ID" "$(pick_member 0)" 14 \
    "Feature" "Refactoring"
add_task_comment "$NEW_TASK_ID" "Unsplash API key obtained. Using free tier (50 req/hour)."
add_task_comment "$NEW_TASK_ID" "Caching in Redis reduces API calls by ~80%."
create_subtask "$NEW_TASK_ID" "Backend proxy for Unsplash API" && complete_subtask "$SUBTASK_ID" "Backend proxy for Unsplash API"
create_subtask "$NEW_TASK_ID" "Redis caching layer" && complete_subtask "$SUBTASK_ID" "Redis caching layer"
create_subtask "$NEW_TASK_ID" "Frontend search UI component"
create_subtask "$NEW_TASK_ID" "Board background preview and apply"
create_subtask "$NEW_TASK_ID" "Attribution display"

new_task "Refactor ManagementDbContext" \
    "Split DbContext into IEntityTypeConfiguration per entity. Fluent API over annotations." \
    1 "$IN_PROG_ID" "$(pick_member 1)" 6 \
    "Refactoring" "Performance"
add_task_comment "$NEW_TASK_ID" "Started with Task and Project entities. Workspace next."
create_subtask "$NEW_TASK_ID" "TaskConfiguration" && complete_subtask "$SUBTASK_ID" "TaskConfiguration"
create_subtask "$NEW_TASK_ID" "ProjectConfiguration" && complete_subtask "$SUBTASK_ID" "ProjectConfiguration"
create_subtask "$NEW_TASK_ID" "WorkspaceConfiguration"
create_subtask "$NEW_TASK_ID" "MemberConfiguration"
create_subtask "$NEW_TASK_ID" "LabelConfiguration"
create_subtask "$NEW_TASK_ID" "Remove data annotations"

new_task "Add Prometheus metrics to all services" \
    "Export HTTP latency (p50/p95/p99), SignalR connections, email queue depth, task/auth rates." \
    0 "$IN_PROG_ID" "$(pick_member 0)" 21 \
    "Analytics" "Performance"
PROMETHEUS_TASK_ID="$NEW_TASK_ID"
add_task_comment "$PROMETHEUS_TASK_ID" "Using prometheus-net library for .NET services."
add_task_comment "$PROMETHEUS_TASK_ID" "Grafana dashboards will be created after metrics are stable."
create_subtask "$PROMETHEUS_TASK_ID" "Add prometheus-net to IdentityService" && complete_subtask "$SUBTASK_ID" "Add prometheus-net to IdentityService"
create_subtask "$PROMETHEUS_TASK_ID" "Add prometheus-net to ManagementService" && complete_subtask "$SUBTASK_ID" "Add prometheus-net to ManagementService"
create_subtask "$PROMETHEUS_TASK_ID" "Custom metrics: task rates"
create_subtask "$PROMETHEUS_TASK_ID" "Custom metrics: auth attempts"
create_subtask "$PROMETHEUS_TASK_ID" "Grafana dashboard templates"

new_task "Real-time notification system" \
    "In-app notifications via SignalR: task assigned, comment on task, workspace invitation, achievement unlocked." \
    1 "$IN_PROG_ID" "$(pick_member 0)" 10 \
    "Feature"
add_task_comment "$NEW_TASK_ID" "Using existing SignalR hub. Adding NotificationHub."
create_subtask "$NEW_TASK_ID" "NotificationHub SignalR setup" && complete_subtask "$SUBTASK_ID" "NotificationHub SignalR setup"
create_subtask "$NEW_TASK_ID" "Notification model and DB table" && complete_subtask "$SUBTASK_ID" "Notification model and DB table"
create_subtask "$NEW_TASK_ID" "Frontend bell icon component"
create_subtask "$NEW_TASK_ID" "Notification dropdown panel"
create_subtask "$NEW_TASK_ID" "Mark as read / mark all read"
create_subtask "$NEW_TASK_ID" "Push events from task service"

new_task "Complete Kazakh translation" \
    "Kazakh (kk) locale is ~60% complete. Missing: dashboard strings, gamification, errors." \
    0 "$IN_PROG_ID" "$(pick_member 0)" 8 \
    "Feature"
create_subtask "$NEW_TASK_ID" "Platform dashboard strings"
create_subtask "$NEW_TASK_ID" "Gamification and achievements"
create_subtask "$NEW_TASK_ID" "Error messages"
create_subtask "$NEW_TASK_ID" "Review by native speaker"

# ---- DONE ----
new_task "Google OAuth login" \
    "Sign in with Google Identity Services. Creates profile if new, links account if existing." \
    2 "$DONE_ID" "$(pick_member 0)" -3 \
    "Feature" "Security"
add_task_comment "$NEW_TASK_ID" "Implemented using Google Identity Services library."
add_task_comment "$NEW_TASK_ID" "Both login and registration flows working."
add_task_comment "$NEW_TASK_ID" "Edge case fixed: user registers with email, then tries Google with same email."
create_subtask "$NEW_TASK_ID" "Backend Google token verification" && complete_subtask "$SUBTASK_ID" "Backend Google token verification"
create_subtask "$NEW_TASK_ID" "Frontend Google button component" && complete_subtask "$SUBTASK_ID" "Frontend Google button component"
create_subtask "$NEW_TASK_ID" "Link existing account flow" && complete_subtask "$SUBTASK_ID" "Link existing account flow"
create_subtask "$NEW_TASK_ID" "New user registration flow" && complete_subtask "$SUBTASK_ID" "New user registration flow"
create_subtask "$NEW_TASK_ID" "Tests" && complete_subtask "$SUBTASK_ID" "Tests"

new_task "Rate limiting for auth endpoints" \
    "Sliding window: login 10/min/IP, register 5/min/IP, password reset 3/min/email." \
    2 "$DONE_ID" "$(pick_member 1)" -7 \
    "Security" "Performance"
add_task_comment "$NEW_TASK_ID" "Using Redis sliding window counter. Clean and efficient."
create_subtask "$NEW_TASK_ID" "Redis sliding window implementation" && complete_subtask "$SUBTASK_ID" "Redis sliding window implementation"
create_subtask "$NEW_TASK_ID" "Apply to login endpoint" && complete_subtask "$SUBTASK_ID" "Apply to login endpoint"
create_subtask "$NEW_TASK_ID" "Apply to register endpoint" && complete_subtask "$SUBTASK_ID" "Apply to register endpoint"
create_subtask "$NEW_TASK_ID" "Apply to password reset" && complete_subtask "$SUBTASK_ID" "Apply to password reset"
create_subtask "$NEW_TASK_ID" "429 response with Retry-After header" && complete_subtask "$SUBTASK_ID" "429 response with Retry-After header"

new_task "ManagementService initial structure" \
    "EF Core + PostgreSQL. CRUD for Workspace, Project, Members. Role-based access." \
    2 "$DONE_ID" "$(pick_member 0)" -14 \
    "Release" "Refactoring"
create_subtask "$NEW_TASK_ID" "EF Core setup with PostgreSQL" && complete_subtask "$SUBTASK_ID" "EF Core setup with PostgreSQL"
create_subtask "$NEW_TASK_ID" "Workspace CRUD" && complete_subtask "$SUBTASK_ID" "Workspace CRUD"
create_subtask "$NEW_TASK_ID" "Project CRUD" && complete_subtask "$SUBTASK_ID" "Project CRUD"
create_subtask "$NEW_TASK_ID" "Member management" && complete_subtask "$SUBTASK_ID" "Member management"
create_subtask "$NEW_TASK_ID" "Role-based authorization" && complete_subtask "$SUBTASK_ID" "Role-based authorization"

new_task "Landing page visual redesign" \
    "Split-screen auth, animated gradient hero, feature cards with glassmorphism, FAQ accordion, responsive." \
    1 "$DONE_ID" "$(pick_member 0)" -5 \
    "Feature"
add_task_comment "$NEW_TASK_ID" "Design approved. Using CSS custom properties for theming."
add_task_comment "$NEW_TASK_ID" "All animations respect prefers-reduced-motion."
create_subtask "$NEW_TASK_ID" "Hero section with gradient mesh" && complete_subtask "$SUBTASK_ID" "Hero section with gradient mesh"
create_subtask "$NEW_TASK_ID" "Feature cards grid" && complete_subtask "$SUBTASK_ID" "Feature cards grid"
create_subtask "$NEW_TASK_ID" "Benefits section" && complete_subtask "$SUBTASK_ID" "Benefits section"
create_subtask "$NEW_TASK_ID" "FAQ accordion" && complete_subtask "$SUBTASK_ID" "FAQ accordion"
create_subtask "$NEW_TASK_ID" "Mobile responsive" && complete_subtask "$SUBTASK_ID" "Mobile responsive"
create_subtask "$NEW_TASK_ID" "Dark mode support" && complete_subtask "$SUBTASK_ID" "Dark mode support"

new_task "Gamification engine - core mechanics" \
    "XP for task completion/comments/logins. Level progression. Achievements. Leaderboard." \
    2 "$DONE_ID" "$(pick_member 0)" -21 \
    "Feature"
add_task_comment "$NEW_TASK_ID" "XP formula: base_xp * priority_multiplier * streak_bonus."
add_task_comment "$NEW_TASK_ID" "Levels: 1-100, exponential curve."
create_subtask "$NEW_TASK_ID" "XP calculation service" && complete_subtask "$SUBTASK_ID" "XP calculation service"
create_subtask "$NEW_TASK_ID" "Level progression logic" && complete_subtask "$SUBTASK_ID" "Level progression logic"
create_subtask "$NEW_TASK_ID" "Achievement definitions" && complete_subtask "$SUBTASK_ID" "Achievement definitions"
create_subtask "$NEW_TASK_ID" "Leaderboard queries" && complete_subtask "$SUBTASK_ID" "Leaderboard queries"
create_subtask "$NEW_TASK_ID" "Kafka events for XP triggers" && complete_subtask "$SUBTASK_ID" "Kafka events for XP triggers"

new_task "Built-in messenger - MVP" \
    "Real-time chat: DMs, workspace channels, history, typing indicators, read receipts." \
    2 "$DONE_ID" "$(pick_member 0)" -10 \
    "Feature"
create_subtask "$NEW_TASK_ID" "MongoDB message storage" && complete_subtask "$SUBTASK_ID" "MongoDB message storage"
create_subtask "$NEW_TASK_ID" "SignalR ChatHub" && complete_subtask "$SUBTASK_ID" "SignalR ChatHub"
create_subtask "$NEW_TASK_ID" "Frontend chat component" && complete_subtask "$SUBTASK_ID" "Frontend chat component"
create_subtask "$NEW_TASK_ID" "Typing indicators" && complete_subtask "$SUBTASK_ID" "Typing indicators"
create_subtask "$NEW_TASK_ID" "Message pagination" && complete_subtask "$SUBTASK_ID" "Message pagination"

# ---- PLANNED ----
if [ "$PLAN_ID" != "$TODO_ID" ]; then
    new_task "AI assistant: smart task suggestions" \
        "Use local Qwen model for next steps, description generation, priority recommendations, duplicate detection." \
        0 "$PLAN_ID" "$(pick_member 0)" 60 \
        "Feature" "Analytics"
    create_subtask "$NEW_TASK_ID" "Task embedding generation"
    create_subtask "$NEW_TASK_ID" "Similar task detection"
    create_subtask "$NEW_TASK_ID" "Next step suggestions"
    create_subtask "$NEW_TASK_ID" "Description generation from title"

    new_task "Mobile app (React Native)" \
        "iOS and Android. Task board, status changes, comments, push notifications, offline support." \
        1 "$PLAN_ID" "$(pick_member 1)" 90 \
        "Mobile" "Feature"
    create_subtask "$NEW_TASK_ID" "React Native project setup"
    create_subtask "$NEW_TASK_ID" "Auth flow with biometrics"
    create_subtask "$NEW_TASK_ID" "Task board screen"
    create_subtask "$NEW_TASK_ID" "Push notifications (FCM/APNs)"
    create_subtask "$NEW_TASK_ID" "Offline mode with sync queue"

    new_task "Burndown and velocity charts" \
        "Sprint progress: burndown (ideal vs actual), velocity chart, cumulative flow diagram." \
        0 "$PLAN_ID" "" 45 \
        "Analytics" "Feature"

    new_task "File attachments for tasks" \
        "Drag-and-drop upload, image/PDF preview, max 10MB/file. S3-compatible storage." \
        0 "$PLAN_ID" "" 50 \
        "Feature"

    new_task "Audit log for workspace actions" \
        "Track task/member/role/settings changes. Queryable log with filters and export." \
        0 "$PLAN_ID" "" 55 \
        "Feature" "Security"
fi

write_step "Creating analytics drill"
new_task "Analytics drill: release board signal" \
    "Deterministic activity for AnalyticsService demo aggregation." \
    2 "$TODO_ID" "$(pick_member 0)" 3 \
    "Analytics" "Release"
ANALYTICS_DRILL_TASK_ID="$NEW_TASK_ID"
write_info "Analytics drill task: '$ANALYTICS_DRILL_TASK_ID'"

add_task_comment "$ANALYTICS_DRILL_TASK_ID" "Analytics drill comment: release checklist started."
add_task_comment "$ANALYTICS_DRILL_TASK_ID" "Analytics drill comment: release checklist verified."

create_subtask "$ANALYTICS_DRILL_TASK_ID" "Analytics drill subtask: verify metrics ingestion"
DRILL_SUBTASK_A_ID="$SUBTASK_ID"
complete_subtask "$DRILL_SUBTASK_A_ID" "Analytics drill subtask: verify metrics ingestion"
write_info "Completed subtask: verify metrics ingestion"

create_subtask "$ANALYTICS_DRILL_TASK_ID" "Analytics drill subtask: verify dashboard refresh"
DRILL_SUBTASK_B_ID="$SUBTASK_ID"
complete_subtask "$DRILL_SUBTASK_B_ID" "Analytics drill subtask: verify dashboard refresh"
write_info "Completed subtask: verify dashboard refresh"

if [ "$DONE_ID" != "$TODO_ID" ]; then
    move_task_status "$ANALYTICS_DRILL_TASK_ID" "$DONE_ID"
    write_info "Moved analytics drill task to done"
fi

if [ "$IN_PROG_ID" != "$DONE_ID" ] && [ "$IN_PROG_ID" != "$TODO_ID" ]; then
    move_task_status "$ANALYTICS_DRILL_TASK_ID" "$IN_PROG_ID"
    write_info "Reopened analytics drill task into in-progress"
fi

if [ "$DONE_ID" != "$IN_PROG_ID" ]; then
    move_task_status "$ANALYTICS_DRILL_TASK_ID" "$DONE_ID"
    write_info "Completed analytics drill task again"
fi

write_step "Creating AI hidden context"
MENTOR_ACCEPTANCE_SUMMARY=$(cat <<'EOF'
Контекст релиза для публичного acceptance:
- Если участник спрашивает про deploy-launch, сначала опирайтесь на внутренний task-context проекта.
- Главные внутренние темы этого демо: CI/CD pipeline, memory leak в WebSocket Hub и релизные метрики.
- Внешние источники можно подключать только после краткого внутреннего ответа и с явным указанием источника.
EOF
)
save_ai_context "$WORKSPACE_ID" "$PROJECT_ID" "[DEMO] Публичный mentor acceptance" "$MENTOR_ACCEPTANCE_SUMMARY" "approved-summary" "hidden" "" "seed:mentor-acceptance"
write_info "Saved hidden context: mentor acceptance"

RELEASE_FOCUS_SUMMARY=$(cat <<'EOF'
Релизный фокус проекта:
- Перед релизом нужно стабилизировать deploy-launch и проверить шаги cutover.
- Memory leak в WebSocket Hub считается релизным блокером до подтверждения cleanup в OnDisconnectedAsync.
- Если вопрос касается приоритета работ, сначала называйте CI/CD и WebSocket Hub как основные внутренние задачи.
EOF
)
save_ai_context "$WORKSPACE_ID" "$PROJECT_ID" "Релизный фокус: WebSocket Hub и CI/CD" "$RELEASE_FOCUS_SUMMARY" "approved-summary" "task-context" "$WEBSOCKET_HUB_TASK_ID" "seed:release-focus"
write_info "Saved task-context: release focus"

CI_CHECKLIST_SUMMARY=$(cat <<'EOF'
CI/CD checklist для production:
- Проверить build, test и deploy-launch в GitHub CI/CD pipeline.
- После deploy-launch перепроверить health-check и доступность panel routes.
- Если участник спрашивает про релиз, напоминать о rollback plan и post-deploy smoke.
EOF
)
save_ai_context "$WORKSPACE_ID" "$PROJECT_ID" "CI/CD checklist для production" "$CI_CHECKLIST_SUMMARY" "approved-summary" "task-context" "$CI_PIPELINE_TASK_ID" "seed:ci-checklist"
write_info "Saved task-context: ci checklist"

METRICS_SUMMARY=$(cat <<'EOF'
Контекст метрик и analytics drill:
- Prometheus metrics и analytics drill нужны для подтверждения, что релиз не деградировал по latency и dashboard refresh.
- Если вопрос касается наблюдаемости, сначала опирайтесь на внутренние задачи про metrics ingestion, dashboard refresh и release checklist.
- После ответа перечисляйте связанные внутренние источники, а внешние статьи добавляйте только как дополнение.
EOF
)
save_ai_context "$WORKSPACE_ID" "$PROJECT_ID" "Метрики релиза и analytics drill" "$METRICS_SUMMARY" "approved-summary" "task-context" "$PROMETHEUS_TASK_ID" "seed:metrics-focus"
write_info "Saved task-context: metrics focus"

# --- Gamification shop items ---
write_step "Seeding gamification shop items"
SHOP_ITEMS=(
    '{"id":"a0000001-0000-0000-0000-000000000001","name":"Gold Frame","description":"A premium golden avatar frame","type":"frame","price":500,"rarity":"epic","imageUrl":""}'
    '{"id":"a0000001-0000-0000-0000-000000000002","name":"Neon Glow","description":"Electric neon avatar frame","type":"frame","price":300,"rarity":"rare","imageUrl":""}'
    '{"id":"a0000001-0000-0000-0000-000000000003","name":"Dark Theme Pro","description":"Exclusive dark theme with custom accents","type":"theme","price":400,"rarity":"rare","imageUrl":""}'
    '{"id":"a0000001-0000-0000-0000-000000000004","name":"Star Badge","description":"Show your star status on your profile","type":"badge","price":200,"rarity":"common","imageUrl":""}'
    '{"id":"a0000001-0000-0000-0000-000000000005","name":"Fire Badge","description":"For the most productive team members","type":"badge","price":350,"rarity":"rare","imageUrl":""}'
    '{"id":"a0000001-0000-0000-0000-000000000006","name":"Crystal Frame","description":"Elegant crystal-themed avatar frame","type":"frame","price":600,"rarity":"legendary","imageUrl":""}'
    '{"id":"a0000001-0000-0000-0000-000000000007","name":"Ocean Breeze","description":"Calming blue theme with wave accents","type":"theme","price":250,"rarity":"common","imageUrl":""}'
    '{"id":"a0000001-0000-0000-0000-000000000008","name":"Pioneer Badge","description":"For early adopters of the platform","type":"badge","price":150,"rarity":"common","imageUrl":""}'
)

for item in "${SHOP_ITEMS[@]}"; do
    ITEM_NAME=$(echo "$item" | jq -r '.name')
    invoke_api POST "/api/gamification/shop/items" "$item" "$TOKEN" >/dev/null 2>&1 && write_ok "Shop item: $ITEM_NAME" || write_info "Shop item: $ITEM_NAME (skipped)"
done

# --- Second project for variety ---
write_step "Creating second project"
SECOND_PROJECT_NAME="Mobile App"
SECOND_PROJECT=$(invoke_api POST "/api/management/workspaces/$WORKSPACE_ID/projects" "{\"name\":\"$SECOND_PROJECT_NAME\",\"key\":\"MOB\"}" "$TOKEN" 2>/dev/null || echo '{}')
SECOND_PROJECT_ID=$(echo "$SECOND_PROJECT" | jq -r '.id // empty' 2>/dev/null)

if [ -n "$SECOND_PROJECT_ID" ] && [ "$SECOND_PROJECT_ID" != "null" ]; then
    write_ok "Project: '$SECOND_PROJECT_NAME'"

    # Get statuses for second project
    SECOND_STATUSES=$(invoke_api GET "/api/management/projects/$SECOND_PROJECT_ID/statuses" "" "$TOKEN" 2>/dev/null || echo '[]')
    SEC_TODO_ID=$(echo "$SECOND_STATUSES" | jq -r '.[0].id // empty' 2>/dev/null)

    if [ -n "$SEC_TODO_ID" ]; then
        # Save original project ID, swap temporarily
        ORIG_PROJECT_ID="$PROJECT_ID"
        PROJECT_ID="$SECOND_PROJECT_ID"
        FIRST_STATUS_ID="$SEC_TODO_ID"

        new_task "Setup React Native project" \
            "Initialize project with Expo, configure navigation, set up auth flow." \
            2 "$SEC_TODO_ID" "$(pick_member 0)" 14 \
            "Mobile"
        create_subtask "$NEW_TASK_ID" "Expo initialization"
        create_subtask "$NEW_TASK_ID" "Navigation setup"
        create_subtask "$NEW_TASK_ID" "Auth screens"

        new_task "Push notification service" \
            "FCM for Android, APNs for iOS. Backend sends via unified endpoint." \
            1 "$SEC_TODO_ID" "$(pick_member 1)" 21 \
            "Mobile" "Feature"

        new_task "Offline mode with sync queue" \
            "SQLite local cache, background sync when online, conflict resolution." \
            0 "$SEC_TODO_ID" "" 30 \
            "Mobile" "Feature"

        # Restore original project
        PROJECT_ID="$ORIG_PROJECT_ID"
        FIRST_STATUS_ID="$TODO_ID"
    fi
else
    write_info "Second project skipped (may already exist)"
fi

echo ""
echo -e "${CYAN}===================================================${NC}"
echo -e "${GREEN}  Done! Demo data loaded successfully.${NC}"
echo -e "${GREEN}  Workspace : $WORKSPACE_NAME${NC}"
echo -e "${GREEN}  Project   : $PROJECT_NAME${NC}"
echo -e "${CYAN}===================================================${NC}"

# Cleanup
rm -f /tmp/collaby_labels.json
