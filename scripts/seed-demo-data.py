#!/usr/bin/env python3
"""
Collaby Demo Seed — creates a realistic workspace with projects, tasks,
comments, subtasks, labels, and AI context for the test account.

Usage:
    python scripts/seed-demo-data.py [BASE_URL] [EMAIL] [PASSWORD]

Defaults:
    BASE_URL  = http://localhost:5001 (identity) / 5003 (management) / 8100 (ai)
    EMAIL     = test@collaby.dev
    PASSWORD  = TestPass123@
"""

import sys
import json
import requests
from datetime import datetime, timedelta, timezone

# ---------------------------------------------------------------------------
# Config
# ---------------------------------------------------------------------------
IDENTITY_URL = sys.argv[1] if len(sys.argv) > 1 else "http://localhost:5001"
MANAGEMENT_URL = sys.argv[2] if len(sys.argv) > 2 else "http://localhost:5003"
AI_URL = sys.argv[3] if len(sys.argv) > 3 else "http://localhost:8100"
EMAIL = sys.argv[4] if len(sys.argv) > 4 else "test@collaby.dev"
PASSWORD = sys.argv[5] if len(sys.argv) > 5 else "TestPass123@"

# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------
session = requests.Session()
session.headers.update({"Content-Type": "application/json"})


def api(method, url, body=None):
    r = session.request(method, url, json=body, timeout=15)
    if r.status_code >= 400:
        print(f"  WARN {method} {url} => {r.status_code}: {r.text[:200]}")
        return None
    try:
        return r.json()
    except Exception:
        return r.text


def ok(msg):
    print(f"  \033[32mOK\033[0m {msg}")


def info(msg):
    print(f"  \033[90m.  {msg}\033[0m")


def step(msg):
    print(f"\n\033[36m>> {msg}\033[0m")


def due(days):
    return (datetime.now(timezone.utc) + timedelta(days=days)).strftime("%Y-%m-%dT%H:%M:%SZ")


# ---------------------------------------------------------------------------
# 1. Login
# ---------------------------------------------------------------------------
step("Login")
login_resp = api("POST", f"{IDENTITY_URL}/api/auth/login", {
    "emailOrUsername": EMAIL, "password": PASSWORD
})
if not login_resp or not login_resp.get("accessToken"):
    print(f"\033[31mLogin failed: {login_resp}\033[0m")
    sys.exit(1)

token = login_resp["accessToken"]
user_id = login_resp.get("user", {}).get("id")
session.headers["Authorization"] = f"Bearer {token}"
ok(f"Logged in as {EMAIL}")

# ---------------------------------------------------------------------------
# 2. Create workspace + projects
# ---------------------------------------------------------------------------
step("Create workspace")
ws_resp = api("POST", f"{MANAGEMENT_URL}/api/management/workspaces", {
    "name": "Collaby Team",
    "projectName": "Platform Core"
})
if not ws_resp:
    # Maybe workspace already exists — try bootstrap
    bootstrap = api("GET", f"{MANAGEMENT_URL}/api/management/bootstrap")
    if bootstrap and bootstrap.get("workspaces"):
        ws = bootstrap["workspaces"][0]
        ws_id = ws["id"]
        ws_name = ws["name"]
        ok(f"Using existing workspace: {ws_name}")
    else:
        print("\033[31mCannot create or find workspace\033[0m")
        sys.exit(1)
else:
    ws_id = ws_resp.get("id") or ws_resp.get("workspaceId")
    ws_name = ws_resp.get("name", "Collaby Team")
    ok(f"Created workspace: {ws_name}")

# Get or create first project
projects = api("GET", f"{MANAGEMENT_URL}/api/management/workspaces/{ws_id}/projects") or []
if not projects:
    info("No projects found, creating one...")
    p_resp = api("POST", f"{MANAGEMENT_URL}/api/management/workspaces/{ws_id}/projects", {
        "name": "Platform Core"
    })
    if p_resp and p_resp.get("id"):
        projects = [p_resp]
    else:
        # Retry fetch after creation
        projects = api("GET", f"{MANAGEMENT_URL}/api/management/workspaces/{ws_id}/projects") or []
    if not projects:
        print("\033[31mCannot create project\033[0m")
        sys.exit(1)

project = projects[0]
project_id = project["id"]
project_name = project["name"]
ok(f"Project: {project_name}")

# Create second project
step("Create additional project")
p2_resp = api("POST", f"{MANAGEMENT_URL}/api/management/workspaces/{ws_id}/projects", {
    "name": "Mobile App"
})
if p2_resp and p2_resp.get("id"):
    project2_id = p2_resp["id"]
    ok("Created project: Mobile App")
else:
    project2_id = None
    info("Second project skipped (may exist)")

# ---------------------------------------------------------------------------
# 3. Load statuses
# ---------------------------------------------------------------------------
step("Load statuses")
statuses = api("GET", f"{MANAGEMENT_URL}/api/management/projects/{project_id}/statuses") or []
if not statuses:
    print("\033[31mNo statuses found\033[0m")
    sys.exit(1)

status_map = {}
for s in statuses:
    status_map[s["name"].lower()] = s["id"]
    info(f"Status: {s['name']}")

todo_id = statuses[0]["id"]
in_prog_id = statuses[1]["id"] if len(statuses) > 1 else todo_id
done_id = statuses[2]["id"] if len(statuses) > 2 else todo_id
plan_id = statuses[3]["id"] if len(statuses) > 3 else todo_id
ok(f"{len(statuses)} statuses loaded")

# ---------------------------------------------------------------------------
# 4. Get members
# ---------------------------------------------------------------------------
step("Load members")
members = api("GET", f"{MANAGEMENT_URL}/api/management/workspaces/{ws_id}/members") or []
member_ids = [m.get("userId") for m in members if m.get("userId")]
assignee = member_ids[0] if member_ids else None
ok(f"{len(member_ids)} member(s)")

# ---------------------------------------------------------------------------
# 5. Create labels
# ---------------------------------------------------------------------------
step("Create labels")
label_defs = [
    ("Bug", "#ef4444"), ("Feature", "#8b5cf6"), ("Refactoring", "#f59e0b"),
    ("Mobile", "#06b6d4"), ("Security", "#dc2626"), ("Release", "#10b981"),
    ("Analytics", "#6366f1"), ("Performance", "#f97316"), ("UX/UI", "#ec4899"),
    ("Backend", "#3b82f6"), ("Frontend", "#a855f7"), ("DevOps", "#14b8a6"),
    ("Documentation", "#78716c"), ("Urgent", "#b91c1c"),
]

labels = {}
for name, color in label_defs:
    resp = api("POST", f"{MANAGEMENT_URL}/api/management/projects/{project_id}/labels", {
        "name": name, "color": color
    })
    if resp and resp.get("id"):
        labels[name] = resp["id"]
        ok(f"Label: {name}")
    else:
        info(f"Label: {name} (skipped)")

# Fetch all existing labels
existing = api("GET", f"{MANAGEMENT_URL}/api/management/projects/{project_id}/labels") or []
for lb in existing:
    labels[lb["name"]] = lb["id"]

# ---------------------------------------------------------------------------
# 6. Create tasks — comprehensive set
# ---------------------------------------------------------------------------
step("Creating tasks")

task_ids = {}


def new_task(key, title, desc, priority, status_id, due_days, label_names,
             comments=None, subtasks=None):
    body = {
        "title": title,
        "description": desc,
        "priority": priority,
    }
    if assignee:
        body["assigneeId"] = assignee
    if due_days != 0:
        body["dueDate"] = due(due_days)

    resp = api("POST", f"{MANAGEMENT_URL}/api/management/projects/{project_id}/tasks", body)
    if not resp or not resp.get("id"):
        info(f"Task FAILED: {title}")
        return
    tid = resp["id"]
    task_ids[key] = tid
    ok(f"Task: {title}")

    # Move to status
    if status_id != todo_id:
        api("PUT", f"{MANAGEMENT_URL}/api/management/tasks/{tid}/move",
            {"statusId": status_id, "order": 0})

    # Labels
    for ln in label_names:
        lid = labels.get(ln)
        if lid:
            api("POST", f"{MANAGEMENT_URL}/api/management/tasks/{tid}/labels", {"labelId": lid})

    # Comments
    if comments:
        for c in comments:
            api("POST", f"{MANAGEMENT_URL}/api/management/tasks/{tid}/comments", {"text": c})
            info(f"  Comment added")

    # Subtasks
    if subtasks:
        for st_title, completed in subtasks:
            st_resp = api("POST", f"{MANAGEMENT_URL}/api/management/tasks/{tid}/subtasks",
                          {"title": st_title})
            if st_resp and st_resp.get("id") and completed:
                api("PUT", f"{MANAGEMENT_URL}/api/management/subtasks/{st_resp['id']}",
                    {"title": st_title, "isCompleted": True})
                info(f"  Subtask done: {st_title}")
            elif st_resp:
                info(f"  Subtask: {st_title}")


# ---- TODO tasks ----
new_task("ci_pipeline", "Setup CI/CD pipeline for production",
         "GitHub CI/CD with checks and builds on push.\n\n"
         "**Stages:** build → test → deploy → smoke\n\n"
         "Requirements:\n"
         "- Docker multi-stage build\n"
         "- Run unit + integration tests\n"
         "- Deploy to staging, then promote to prod\n"
         "- Rollback plan on failure",
         2, todo_id, 7, ["Release", "DevOps"],
         comments=[
             "Need to decide between GitHub runners and GitHub-hosted runners. "
             "Self-hosted gives us more control but requires maintenance.",
             "Let's start with shared runners and migrate later if needed.",
         ],
         subtasks=[
             ("Write Dockerfile for each service", True),
             ("Configure GitHub CI YAML", False),
             ("Setup staging environment", False),
             ("Add post-deploy smoke tests", False),
             ("Document rollback procedure", False),
         ])

new_task("auth_tests", "Write unit tests for AuthService",
         "Cover key scenarios for authentication:\n"
         "- Successful login with email\n"
         "- Wrong password returns 401\n"
         "- Expired token returns 401\n"
         "- Refresh token flow\n"
         "- Rate limiting after 10 failed attempts\n"
         "- Google OAuth flow",
         1, todo_id, 5, ["Backend", "Security"],
         comments=["Current coverage is ~30%. Target: 80%+ on auth endpoints."],
         subtasks=[
             ("Test login success scenario", False),
             ("Test wrong password", False),
             ("Test expired token", False),
             ("Test refresh flow", False),
             ("Test rate limiting", False),
             ("Test Google OAuth", False),
         ])

new_task("my_tasks_page", "Implement My Tasks page",
         "Show tasks from all workspaces where current user is assignee.\n\n"
         "Group by project, sort by due date. Include:\n"
         "- Filter by status (todo/in-progress/done)\n"
         "- Quick status change via dropdown\n"
         "- Due date indicator (overdue = red)",
         1, todo_id, 10, ["Feature", "Frontend"],
         subtasks=[
             ("API endpoint: GET /api/management/tasks/my", False),
             ("Frontend component: MyTasksPage", False),
             ("Add filters and sorting", False),
             ("Mobile responsive layout", False),
         ])

new_task("websocket_leak", "Fix memory leak in WebSocket Hub",
         "SignalR Groups not cleaned when tab closes unexpectedly.\n\n"
         "**Root cause:** `OnDisconnectedAsync` doesn't remove user from all groups.\n\n"
         "**Impact:** Memory grows ~2MB/hour on production with 50+ active users.\n\n"
         "**Fix:** Track user-group associations in Redis, clean up on disconnect.",
         3, todo_id, 2, ["Bug", "Performance", "Urgent"],
         comments=[
             "Reproduced locally: open 10 tabs, close them, memory doesn't decrease.",
             "Redis tracking approach approved. Will store userId -> groupIds in a Redis Set.",
             "This is a release blocker — must fix before next deployment.",
         ],
         subtasks=[
             ("Add Redis tracking for SignalR groups", False),
             ("Implement cleanup in OnDisconnectedAsync", False),
             ("Load test with 100 concurrent connections", False),
             ("Monitor memory in staging for 24h", False),
         ])

new_task("pagination", "Add cursor-based pagination to task list",
         "Large boards with 100+ tasks are slow to load.\n\n"
         "Implement cursor-based pagination:\n"
         "- Page size: 25 tasks\n"
         "- Cursor: `createdAt` + `id`\n"
         "- Frontend: infinite scroll with intersection observer",
         0, todo_id, 15, ["Feature", "Performance", "Backend"],
         subtasks=[
             ("Backend: cursor-based query", False),
             ("API contract: cursor param + hasMore", False),
             ("Frontend: IntersectionObserver hook", False),
             ("Test with 500 tasks", False),
         ])

new_task("dark_mode_polish", "Polish dark mode across all pages",
         "Some components have hardcoded light colors.\n\n"
         "Audit and fix:\n"
         "- Modal backgrounds\n"
         "- Dropdown menus\n"
         "- Toast notifications\n"
         "- Form validation messages\n"
         "- Chart colors",
         0, todo_id, 12, ["UX/UI", "Frontend"],
         subtasks=[
             ("Audit all components for hardcoded colors", False),
             ("Fix modal backgrounds", False),
             ("Fix dropdown menus", False),
             ("Fix toast notifications", False),
             ("Update chart color palette", False),
         ])

new_task("email_templates", "Design email notification templates",
         "HTML email templates for:\n"
         "- Welcome email after registration\n"
         "- Password reset\n"
         "- Task assigned to you\n"
         "- Weekly digest\n\n"
         "Use MJML for responsive email markup.",
         0, todo_id, 20, ["Feature", "UX/UI"],
         subtasks=[
             ("Design base template layout", False),
             ("Welcome email", False),
             ("Password reset email", False),
             ("Task assignment email", False),
             ("Weekly digest email", False),
         ])

# ---- IN PROGRESS tasks ----
new_task("unsplash", "Unsplash API integration for board backgrounds",
         "Users can search and set board background images via Unsplash.\n\n"
         "- Search endpoint with debounce\n"
         "- Cache results in Redis (TTL: 1h)\n"
         "- Attribution as required by Unsplash API\n"
         "- Fallback solid colors",
         0, in_prog_id, 14, ["Feature", "Frontend"],
         comments=[
             "Unsplash API key obtained. Using free tier (50 req/hour).",
             "Caching in Redis reduces API calls by ~80%.",
         ],
         subtasks=[
             ("Backend proxy for Unsplash API", True),
             ("Redis caching layer", True),
             ("Frontend search UI component", False),
             ("Board background preview & apply", False),
             ("Attribution display", False),
         ])

new_task("refactor_dbcontext", "Refactor ManagementDbContext",
         "Split monolithic DbContext into per-entity configurations.\n\n"
         "Use `IEntityTypeConfiguration<T>` and Fluent API.\n"
         "Remove all data annotations from entity classes.",
         1, in_prog_id, 6, ["Refactoring", "Backend"],
         comments=["Started with Task and Project entities. Workspace next."],
         subtasks=[
             ("TaskConfiguration", True),
             ("ProjectConfiguration", True),
             ("WorkspaceConfiguration", False),
             ("MemberConfiguration", False),
             ("LabelConfiguration", False),
             ("Remove data annotations", False),
         ])

new_task("prometheus", "Add Prometheus metrics to all services",
         "Export key metrics for monitoring:\n"
         "- HTTP request latency (p50, p95, p99)\n"
         "- Active SignalR connections\n"
         "- Email queue depth\n"
         "- Task creation/completion rates\n"
         "- Auth attempts (success/fail)",
         0, in_prog_id, 21, ["Analytics", "DevOps"],
         comments=[
             "Using prometheus-net library for .NET services.",
             "Grafana dashboards will be created after metrics are stable.",
         ],
         subtasks=[
             ("Add prometheus-net to IdentityService", True),
             ("Add prometheus-net to ManagementService", True),
             ("Custom metrics: task rates", False),
             ("Custom metrics: auth attempts", False),
             ("Grafana dashboard templates", False),
         ])

new_task("notification_system", "Real-time notification system",
         "In-app notifications via SignalR:\n"
         "- Task assigned to you\n"
         "- Comment on your task\n"
         "- Workspace invitation\n"
         "- Achievement unlocked\n\n"
         "Bell icon with unread count badge.",
         1, in_prog_id, 10, ["Feature", "Frontend", "Backend"],
         comments=["Using existing SignalR hub. Adding NotificationHub."],
         subtasks=[
             ("NotificationHub SignalR setup", True),
             ("Notification model & DB table", True),
             ("Frontend bell icon component", False),
             ("Notification dropdown panel", False),
             ("Mark as read / mark all read", False),
             ("Push events from task service", False),
         ])

new_task("i18n_kazakh", "Complete Kazakh translation",
         "Kazakh (kk) locale is ~60% complete.\n\n"
         "Missing sections:\n"
         "- Platform dashboard strings\n"
         "- Gamification descriptions\n"
         "- Error messages\n"
         "- Email templates",
         0, in_prog_id, 8, ["Frontend", "Documentation"],
         subtasks=[
             ("Platform dashboard strings", False),
             ("Gamification & achievements", False),
             ("Error messages", False),
             ("Review by native speaker", False),
         ])

# ---- DONE tasks ----
new_task("google_oauth", "Google OAuth login",
         "Sign in with Google Identity Services.\n"
         "Creates profile if user is new. Links OAuth account if user exists.",
         2, done_id, -3, ["Feature", "Security", "Backend"],
         comments=[
             "Implemented using Google Identity Services library.",
             "Both login and registration flows working.",
             "Edge case fixed: user registers with email, then tries Google with same email.",
         ],
         subtasks=[
             ("Backend Google token verification", True),
             ("Frontend Google button component", True),
             ("Link existing account flow", True),
             ("New user registration flow", True),
             ("Tests", True),
         ])

new_task("rate_limiting", "Rate limiting for auth endpoints",
         "Sliding window rate limiting:\n"
         "- Login: 10 attempts/min per IP\n"
         "- Register: 5 attempts/min per IP\n"
         "- Password reset: 3 attempts/min per email",
         2, done_id, -7, ["Security", "Backend"],
         comments=["Using Redis sliding window counter. Clean and efficient."],
         subtasks=[
             ("Redis sliding window implementation", True),
             ("Apply to login endpoint", True),
             ("Apply to register endpoint", True),
             ("Apply to password reset", True),
             ("429 response with Retry-After header", True),
         ])

new_task("management_structure", "ManagementService initial structure",
         "EF Core + PostgreSQL setup for workspace and project management.\n"
         "CRUD operations, member management, role-based access.",
         2, done_id, -14, ["Release", "Backend"],
         subtasks=[
             ("EF Core setup with PostgreSQL", True),
             ("Workspace CRUD", True),
             ("Project CRUD", True),
             ("Member management", True),
             ("Role-based authorization", True),
         ])

new_task("landing_redesign", "Landing page visual redesign",
         "Complete redesign of the landing page:\n"
         "- Split-screen auth layout\n"
         "- Animated gradient hero\n"
         "- Feature cards with glassmorphism\n"
         "- FAQ accordion\n"
         "- Responsive for mobile",
         1, done_id, -5, ["UX/UI", "Frontend"],
         comments=[
             "Design approved. Using CSS custom properties for theming.",
             "All animations respect prefers-reduced-motion.",
         ],
         subtasks=[
             ("Hero section with gradient mesh", True),
             ("Feature cards grid", True),
             ("Benefits section", True),
             ("FAQ accordion", True),
             ("Mobile responsive", True),
             ("Dark mode support", True),
         ])

new_task("gamification_core", "Gamification engine — core mechanics",
         "Points system, levels, and achievements:\n"
         "- XP for task completion, comments, logins\n"
         "- Level progression with thresholds\n"
         "- Achievement definitions and unlock logic\n"
         "- Leaderboard queries",
         2, done_id, -21, ["Feature", "Backend"],
         comments=[
             "XP formula: base_xp * priority_multiplier * streak_bonus.",
             "Levels: 1-100, exponential curve.",
         ],
         subtasks=[
             ("XP calculation service", True),
             ("Level progression logic", True),
             ("Achievement definitions", True),
             ("Leaderboard queries", True),
             ("Kafka events for XP triggers", True),
         ])

new_task("chat_mvp", "Built-in messenger — MVP",
         "Real-time chat within workspaces:\n"
         "- Direct messages between members\n"
         "- Workspace channels\n"
         "- Message history with pagination\n"
         "- Typing indicators\n"
         "- Read receipts",
         2, done_id, -10, ["Feature", "Backend", "Frontend"],
         subtasks=[
             ("MongoDB message storage", True),
             ("SignalR ChatHub", True),
             ("Frontend chat component", True),
             ("Typing indicators", True),
             ("Message pagination", True),
         ])

# ---- PLANNED tasks ----
if plan_id != todo_id:
    new_task("ai_suggestions", "AI assistant: smart task suggestions",
             "Use local Qwen model to suggest:\n"
             "- Next steps based on project history\n"
             "- Task descriptions from titles\n"
             "- Priority recommendations\n"
             "- Similar task detection (duplicates)",
             0, plan_id, 60, ["Feature", "Analytics"],
             subtasks=[
                 ("Task embedding generation", False),
                 ("Similar task detection", False),
                 ("Next step suggestions", False),
                 ("Description generation from title", False),
             ])

    new_task("mobile_app", "Mobile app (React Native)",
             "iOS and Android app with core features:\n"
             "- Task board with drag & drop\n"
             "- Status changes\n"
             "- Comments\n"
             "- Push notifications\n"
             "- Offline support with sync",
             1, plan_id, 90, ["Mobile", "Feature"],
             subtasks=[
                 ("React Native project setup", False),
                 ("Auth flow with biometrics", False),
                 ("Task board screen", False),
                 ("Push notifications (FCM/APNs)", False),
                 ("Offline mode with sync queue", False),
             ])

    new_task("burndown", "Burndown & velocity charts",
             "Sprint progress visualization:\n"
             "- Burndown chart (ideal vs actual)\n"
             "- Velocity chart (story points per sprint)\n"
             "- Cumulative flow diagram\n"
             "- Configurable sprint length",
             0, plan_id, 45, ["Analytics", "Feature", "Frontend"])

    new_task("file_attachments", "File attachments for tasks",
             "Upload and attach files to tasks:\n"
             "- Drag & drop upload\n"
             "- Image preview\n"
             "- PDF preview\n"
             "- Max 10MB per file, 50MB per task\n"
             "- S3-compatible storage backend",
             0, plan_id, 50, ["Feature", "Backend"])

    new_task("audit_log", "Audit log for workspace actions",
             "Track all significant actions:\n"
             "- Task created/updated/deleted\n"
             "- Member added/removed\n"
             "- Role changed\n"
             "- Settings modified\n\n"
             "Queryable log with filters and export.",
             0, plan_id, 55, ["Feature", "Security", "Backend"])

# ---------------------------------------------------------------------------
# 7. Analytics drill — create activity for charts
# ---------------------------------------------------------------------------
step("Analytics drill")
new_task("analytics_drill", "Analytics drill: release board signal",
         "Deterministic activity for AnalyticsService demo aggregation.\n"
         "This task generates multiple state transitions for chart data.",
         2, todo_id, 3, ["Analytics", "Release"],
         comments=[
             "Analytics drill: release checklist started.",
             "Analytics drill: all checks passed.",
             "Analytics drill: metrics ingestion verified.",
         ],
         subtasks=[
             ("Verify metrics ingestion", True),
             ("Verify dashboard refresh", True),
             ("Run smoke test suite", True),
             ("Sign off release", False),
         ])

drill_id = task_ids.get("analytics_drill")
if drill_id:
    # Move through statuses to generate activity
    if done_id != todo_id:
        api("PUT", f"{MANAGEMENT_URL}/api/management/tasks/{drill_id}/move",
            {"statusId": done_id, "order": 0})
        info("Moved drill task to done")
    if in_prog_id != done_id:
        api("PUT", f"{MANAGEMENT_URL}/api/management/tasks/{drill_id}/move",
            {"statusId": in_prog_id, "order": 0})
        info("Reopened drill task to in-progress")
    if done_id != in_prog_id:
        api("PUT", f"{MANAGEMENT_URL}/api/management/tasks/{drill_id}/move",
            {"statusId": done_id, "order": 0})
        info("Completed drill task again")

# ---------------------------------------------------------------------------
# 8. AI mentor context
# ---------------------------------------------------------------------------
step("AI mentor context")


def save_context(title, content, kind="approved-summary", visibility="hidden",
                 task_id=None, source_ref=None):
    body = {
        "workspaceId": ws_id,
        "projectId": project_id,
        "title": title,
        "content": content,
        "kind": kind,
        "status": "approved",
        "visibility": visibility,
    }
    if task_id:
        body["taskId"] = task_id
    if source_ref:
        body["sourceRef"] = source_ref
    resp = api("POST", f"{AI_URL}/api/ai/mentor/context", body)
    if resp is not None:
        info(f"Saved context: {title}")
    else:
        info(f"Context skipped (AI service may be down): {title}")


save_context(
    "[DEMO] Project overview",
    "Collaby — платформа для командной работы. Основные модули:\n"
    "- Task management (Kanban boards, statuses, priorities, labels)\n"
    "- Built-in messenger (SignalR + MongoDB)\n"
    "- Gamification (XP, levels, achievements, leaderboard)\n"
    "- Analytics (Prometheus metrics, dashboards)\n"
    "- AI assistant (Qwen model, task suggestions)\n\n"
    "Стек: .NET 8 microservices, React 19, PostgreSQL, MongoDB, Redis, Kafka.",
    source_ref="seed:project-overview"
)

save_context(
    "Release focus: WebSocket Hub & CI/CD",
    "Релизный фокус проекта:\n"
    "- Memory leak в WebSocket Hub — релизный блокер\n"
    "- CI/CD pipeline должен быть настроен до следующего деплоя\n"
    "- Prometheus metrics нужны для post-deploy мониторинга\n"
    "- После fix WebSocket Hub: нагрузочное тестирование 24ч в staging",
    kind="approved-summary",
    visibility="task-context",
    task_id=task_ids.get("websocket_leak"),
    source_ref="seed:release-focus"
)

save_context(
    "CI/CD checklist for production",
    "CI/CD checklist:\n"
    "1. Docker multi-stage build для каждого сервиса\n"
    "2. Unit + integration tests в CI\n"
    "3. Deploy to staging → smoke test → promote to prod\n"
    "4. Health-check verification после deploy\n"
    "5. Rollback plan: revert to previous Docker image tag\n"
    "6. Post-deploy: проверить Prometheus metrics и Grafana dashboards",
    kind="approved-summary",
    visibility="task-context",
    task_id=task_ids.get("ci_pipeline"),
    source_ref="seed:ci-checklist"
)

save_context(
    "Metrics & observability context",
    "Контекст метрик:\n"
    "- prometheus-net интегрирован в Identity и Management сервисы\n"
    "- Кастомные метрики: task_created_total, task_completed_total, auth_attempts_total\n"
    "- Grafana dashboards будут созданы после стабилизации метрик\n"
    "- Analytics drill task используется для проверки ingestion pipeline",
    kind="approved-summary",
    visibility="task-context",
    task_id=task_ids.get("prometheus"),
    source_ref="seed:metrics-focus"
)

# ---------------------------------------------------------------------------
# Done
# ---------------------------------------------------------------------------
total_tasks = len(task_ids)
print(f"\n\033[36m{'='*50}\033[0m")
print(f"\033[32m  Done! {total_tasks} tasks created.\033[0m")
print(f"\033[32m  Workspace : {ws_name}\033[0m")
print(f"\033[32m  Project   : {project_name}\033[0m")
print(f"\033[36m{'='*50}\033[0m")
