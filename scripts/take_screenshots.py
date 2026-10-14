#!/usr/bin/env python3
"""Take screenshots of the Collaby frontend for the diploma document.

Strategy: login via API, inject token into sessionStorage, then navigate.
"""

import sys
import io
import json
import time
import urllib.request
from pathlib import Path
from playwright.sync_api import sync_playwright

sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8', errors='replace')

BASE_URL = "http://localhost:3000"
API_URL = "http://localhost:5001"
OUTPUT_DIR = Path(__file__).resolve().parent.parent / "output" / "screenshots"
OUTPUT_DIR.mkdir(parents=True, exist_ok=True)

VIEWPORT = {"width": 1280, "height": 800}


def api_login():
    """Login via API and return access token."""
    data = json.dumps({"emailOrUsername": "test@collaby.dev", "password": "TestPass123@"}).encode()
    req = urllib.request.Request(
        f"{API_URL}/api/auth/login",
        data=data,
        headers={"Content-Type": "application/json"}
    )
    with urllib.request.urlopen(req) as resp:
        result = json.loads(resp.read())
    if result.get("success"):
        return result["accessToken"]
    raise RuntimeError(f"Login failed: {result}")


def take_screenshots():
    # Get token first
    print("[0] Logging in via API...")
    token = api_login()
    print(f"    Token obtained ({len(token)} chars)")

    with sync_playwright() as p:
        browser = p.chromium.launch(headless=True)
        context = browser.new_context(
            viewport=VIEWPORT,
            device_scale_factor=2,
            locale="ru-RU",
            color_scheme="light",
        )
        page = context.new_page()

        def inject_token():
            """Inject access token into sessionStorage."""
            page.evaluate(f'''() => {{
                sessionStorage.setItem('accessToken', '{token}');
                // Set Russian language and light theme
                const prefs = JSON.stringify({{
                    language: "ru", theme: "light", accountHydrated: true,
                    hasGuestOverride: false,
                    themeConfig: {{ mode: "light", accent: "#F97316", contrast: 50, sidebarCustom: false }}
                }});
                localStorage.setItem('collaby-ui-preferences', prefs);
            }}''')

        def save(filename):
            path = OUTPUT_DIR / filename
            page.screenshot(path=str(path), full_page=False)
            size = path.stat().st_size / 1024
            print(f"    -> {filename} ({size:.0f} KB)")
            return size

        def click_nav(text, wait=2.5):
            """Click sidebar navigation button by text."""
            page.evaluate(f'''() => {{
                const btns = document.querySelectorAll('nav button, aside button');
                for (const b of btns) {{
                    if (b.textContent.trim().includes("{text}")) {{ b.click(); return; }}
                }}
            }}''')
            time.sleep(wait)

        # 1. Landing (no auth needed)
        print("[1] Landing...")
        page.goto(BASE_URL, wait_until="networkidle")
        time.sleep(1.5)
        save("landing.png")

        # 2. Auth page
        print("[2] Auth page...")
        page.goto(f"{BASE_URL}/auth?mode=login", wait_until="networkidle")
        time.sleep(1)
        save("auth.png")

        # 3. Inject token and go to dashboard
        print("[3] Dashboard...")
        page.goto(BASE_URL, wait_until="domcontentloaded")
        inject_token()
        page.goto(f"{BASE_URL}/dashboard", wait_until="networkidle")
        time.sleep(2)
        # Re-inject in case of redirect
        if "/auth" in page.url:
            inject_token()
            page.goto(f"{BASE_URL}/dashboard", wait_until="networkidle")
            time.sleep(2)
        sz = save("dashboard.png")
        if sz < 50:
            print("    WARNING: Dashboard may be empty, retrying...")
            inject_token()
            page.reload(wait_until="networkidle")
            time.sleep(2)
            save("dashboard.png")

        print(f"    URL: {page.url}")

        # 4. Tasks
        print("[4] Tasks...")
        click_nav("Мои задачи")
        save("kanban_board.png")

        # 5. Messenger
        print("[5] Messenger...")
        click_nav("Мессенджер")
        save("messenger.png")

        # 6. Analytics
        print("[6] Analytics...")
        click_nav("Аналитика")
        save("analytics.png")

        # 7. Gamification
        print("[7] Gamification...")
        click_nav("Геймификация")
        save("gamification.png")

        # 8. AI assistant
        print("[8] AI assistant...")
        page.evaluate('''() => {
            const btns = document.querySelectorAll('button');
            for (const b of btns) {
                if (b.textContent.includes("AI-ассистент")) { b.click(); return; }
            }
        }''')
        time.sleep(2)
        save("ai_assistant.png")

        browser.close()

    # Summary
    files = sorted(OUTPUT_DIR.glob("*.png"))
    print(f"\nTotal: {len(files)} files")
    for f in files:
        size_kb = f.stat().st_size / 1024
        status = "OK" if size_kb > 50 else "SMALL"
        print(f"  [{status}] {f.name} ({size_kb:.0f} KB)")


if __name__ == "__main__":
    print("Taking Collaby screenshots...")
    take_screenshots()
