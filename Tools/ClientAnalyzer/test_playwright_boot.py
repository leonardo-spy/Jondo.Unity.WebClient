import os
import sys
import time
from pathlib import Path
from playwright.sync_api import sync_playwright

REPO_ROOT = Path(__file__).parent.parent.parent
SCREENSHOT_DIR = REPO_ROOT / "Screenshots"
SCREENSHOT_DIR.mkdir(parents=True, exist_ok=True)
SCREENSHOT_PATH = SCREENSHOT_DIR / "playwright-boot-test.png"

def run_playwright_test():
    print("=" * 65)
    print("  PLAYWRIGHT WEBGL BOOT & RUNTIME VALIDATOR")
    print("=" * 65)

    console_logs = []
    errors = []

    with sync_playwright() as p:
        browser = p.chromium.launch(
            channel="msedge",
            headless=True,
            args=[
                "--enable-webgl",
                "--use-gl=angle",
                "--use-angle=d3d11",
                "--ignore-gpu-blocklist",
                "--no-sandbox",
                "--disable-setuid-sandbox",
            ]
        )
        context = browser.new_context(
            viewport={"width": 1280, "height": 720}
        )
        page = context.new_page()

        def on_console(msg):
            entry = f"[{msg.type}] {msg.text}"
            console_logs.append(entry)
            # Print important log messages immediately
            if any(k in msg.text for k in ["=== Phase", "BOOTSTRAP", "PASS", "DataCenter", "UIBinding", "UIScreen", "Protocol", "FATAL", "Error", "Exception"]):
                print(f"  [BROWSER CONSOLE] {entry}")

        def on_page_error(err):
            errors.append(str(err))
            print(f"  [PAGE ERROR] {err}")

        page.on("console", on_console)
        page.on("pageerror", on_page_error)

        print("Navegando para http://localhost:3000...")
        response = page.goto("http://localhost:3000", wait_until="networkidle", timeout=30000)
        print(f"Status HTTP: {response.status if response else 'None'}")

        # Wait for canvas
        page.wait_for_selector("#unity-canvas", timeout=15000)
        print("[OK] Elemento #unity-canvas encontrado no DOM.")

        # Give Unity time to instantiate and boot WebGLProbe
        print("Aguardando boot do WebGL (15s)...")
        start_time = time.time()
        boot_completed = False

        while time.time() - start_time < 20:
            time.sleep(1)
            for l in console_logs:
                if "BOOTSTRAP COMPLETE" in l or "=== BOOTSTRAP COMPLETE ===" in l:
                    boot_completed = True
                    break
            if boot_completed:
                print(f"[OK] Boot detectado completo em {round(time.time() - start_time, 1)}s!")
                break

        page.screenshot(path=str(SCREENSHOT_PATH))
        print(f"[OK] Screenshot capturada em: {SCREENSHOT_PATH}")

        browser.close()

    print("\n--- RESUMO DA EXECUÇÃO PLAYWRIGHT ---")
    print(f"Total de mensagens de console: {len(console_logs)}")
    print(f"Total de erros de página: {len(errors)}")

    phases_found = [l for l in console_logs if "=== Phase" in l]
    print(f"Fases de boot observadas ({len(phases_found)}):")
    for pf in phases_found:
        print(f"  {pf}")

    return {
        "console_count": len(console_logs),
        "errors_count": len(errors),
        "boot_completed": boot_completed,
        "screenshot": str(SCREENSHOT_PATH)
    }

if __name__ == "__main__":
    run_playwright_test()
