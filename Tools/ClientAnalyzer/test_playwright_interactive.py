import os
import sys
import time
from pathlib import Path
from playwright.sync_api import sync_playwright

REPO_ROOT = Path(__file__).parent.parent.parent
SCREENSHOT_DIR = REPO_ROOT / "Screenshots"
SCREENSHOT_DIR.mkdir(parents=True, exist_ok=True)

def run():
    print("=" * 65)
    print("  PLAYWRIGHT INTERACTIVE BROWSER & PLAY BUTTON TEST")
    print("=" * 65)

    all_logs = []
    page_errors = []

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
        context = browser.new_context(viewport={"width": 1280, "height": 800})
        page = context.new_page()

        def log_handler(msg):
            entry = f"[{msg.type.upper()}] {msg.text}"
            all_logs.append(entry)
            text = msg.text
            if any(w in text for w in [
                "Phase", "BOOTSTRAP", "PLAY", "Audio", "Login", "LOGIN", 
                "btn_play", "FMOD", "error", "Exception", "missing"
            ]):
                try:
                    print(f"  {entry[:140]}")
                except Exception:
                    pass

        page.on("console", log_handler)
        page.on("pageerror", lambda err: page_errors.append(str(err)))

        print("Navegando para http://localhost:3000...")
        page.goto("http://localhost:3000", wait_until="networkidle")

        canvas = page.wait_for_selector("#unity-canvas", timeout=15000)
        print("[OK] Elemento #unity-canvas encontrado no DOM.")

        # Aguardar inicializacao e boot
        print("Aguardando boot sequencial do WebGLProbe...")
        start = time.time()
        booted = False
        while time.time() - start < 25:
            time.sleep(1)
            for entry in all_logs:
                if "BOOTSTRAP COMPLETE" in entry:
                    booted = True
                    break
            if booted:
                break

        print(f"[OK] Boot detectado: {booted} (tempo: {round(time.time() - start, 1)}s)")

        # Capturar screenshot logo apos bootstrap
        boot_shot = SCREENSHOT_DIR / "01_browser_boot.png"
        page.screenshot(path=str(boot_shot))
        print(f"[OK] Screenshot do boot salva em: {boot_shot}")

        # Obter bounding box do canvas
        box = canvas.bounding_box()
        print(f"Canvas Bounding Box: {box}")

        if box:
            # O botao de login/play no Dofus Unity fica posicionado no centro-inferior do painel de login
            # Dentro do canvas (width 960, height 600):
            # x =~ centro (width * 0.5)
            # y =~ centro-inferior (height * 0.70 a 0.75)
            click_x = box["x"] + box["width"] * 0.5
            click_y = box["y"] + box["height"] * 0.72

            print(f"Disparando clique de usuario na posicao do botao Play: ({click_x:.1f}, {click_y:.1f})...")
            # Primeiro clique para desbloquear AudioContext e focar o canvas
            page.mouse.click(click_x, click_y)
            time.sleep(1.5)

            # Segundo clique para ativar o botao Play
            page.mouse.click(click_x, click_y)
            time.sleep(2.5)

            clicked_shot = SCREENSHOT_DIR / "02_browser_play_clicked.png"
            page.screenshot(path=str(clicked_shot))
            print(f"[OK] Screenshot apos clique salva em: {clicked_shot}")

        # Avaliar estado de AudioContext no navegador
        try:
            audio_info = page.evaluate("""
                () => {
                    const ctx = window.AudioContext || window.webkitAudioContext;
                    return {
                        hasAudioContext: !!ctx,
                        instances: window._fmodAudioContext ? window._fmodAudioContext.state : 'untracked'
                    };
                }
            """)
            print(f"AudioContext Info: {audio_info}")
        except Exception as e:
            print(f"AudioContext Eval Info: {e}")

        browser.close()

    print("\n" + "=" * 65)
    print("  RESUMO DA EXECUCAO")
    print("=" * 65)
    print(f"Total de mensagens no console do navegador: {len(all_logs)}")
    print(f"Erros de pagina (JS): {len(page_errors)}")

    missing_scripts = [l for l in all_logs if "missing!" in l]
    print(f"Total de warnings 'script missing': {len(missing_scripts)}")
    for ms in missing_scripts[:5]:
        print(f"  {ms}")

    play_logs = [l for l in all_logs if "PLAY" in l or "btn_play" in l or "login" in l.lower()]
    print(f"Logs relacionados a Play/Login ({len(play_logs)}):")
    for pl in play_logs[-10:]:
        print(f"  {pl}")

if __name__ == "__main__":
    run()

