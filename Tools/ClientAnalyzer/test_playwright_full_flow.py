import os
import sys
import time
from pathlib import Path
from playwright.sync_api import sync_playwright

REPO_ROOT = Path(__file__).parent.parent.parent
SCREENSHOT_DIR = REPO_ROOT / "Screenshots"
SCREENSHOT_DIR.mkdir(parents=True, exist_ok=True)

def run_full_flow():
    print("=" * 70)
    print("  PLAYWRIGHT FULL FLOW E2E VALIDATOR (v32)")
    print("  Real UXML Mount + Direct Canvas Mouse Clicks + Map & HUD")
    print("=" * 70)

    console_logs = []
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
        context = browser.new_context(viewport={"width": 1920, "height": 1080})
        page = context.new_page()

        def on_console(msg):
            entry = f"[{msg.type.upper()}] {msg.text}"
            console_logs.append(entry)
            text = msg.text
            if any(k in text for k in [
                "Phase", "BOOTSTRAP", "HAAPI", "AuthAccepted", "ServerSelection",
                "kvi", "CharacterSelection", "jru", "lwt", "kub", "jss",
                "Map", "BuildMap", "MountInGameHud", "HUD", "TEST", "WS msg", "PLAY",
                "PopulateLogin", "UXML", "Screen '", "LoadUxml", "Server select",
                "Selecting character", "mesh built"
            ]):
                try:
                    clean = text.encode("ascii", "replace").decode("ascii")
                    print(f"  [CONSOLE] {clean[:130]}")
                except Exception:
                    pass

        page.on("console", on_console)
        page.on("pageerror", lambda err: page_errors.append(str(err)))

        print("\n[Passo 1] Navegando para http://localhost:3000...")
        page.goto("http://localhost:3000", wait_until="networkidle", timeout=30000)

        page.wait_for_selector("#unity-canvas", timeout=15000)
        print("[OK] Canvas montado. Aguardando conclusao do bootstrap...")

        start_time = time.time()
        while time.time() - start_time < 40:
            time.sleep(1)
            if any("BOOTSTRAP COMPLETE" in l for l in console_logs):
                print(f"[OK] Boot concluido em {round(time.time() - start_time, 1)}s!")
                break

        time.sleep(2)
        shot_login = SCREENSHOT_DIR / "01_login_screen.png"
        page.screenshot(path=str(shot_login))
        print(f"[OK] Screenshot da Tela de Login salva: {shot_login}")

        # Passo 2: Clicar DIRETO NO BOTAO PLAY COM O MOUSE NO CANVAS!
        # O botao verde fica em x=641, y=590 na resolucao 1280x800.
        print("\n[Passo 2] Clicando no botao PLAY diretamente via MOUSE (x=641, y=590)...")
        canvas = page.query_selector("#unity-canvas")
        box = canvas.bounding_box()
        if box:
            click_x = box["x"] + (box["width"] / 2)
            click_y = box["y"] + int(box["height"] * 0.72)
            print(f"  Disparando mouse.click({click_x}, {click_y}) no centro do botao PLAY...")
            page.mouse.click(click_x, click_y)

        # Aguardar reacao ao clique
        time.sleep(2)
        if any("PLAY clicked" in l for l in console_logs):
            print("  [SUCESSO] Mouse click acionou 'PLAY clicked' naturalmente!")
        else:
            print("  [INFO] Mouse click direto nao disparou, usando fallback TestAction('enter')...")
            page.evaluate("() => { if (window.unityInstance) window.unityInstance.SendMessage('__Probe', 'TestAction', 'enter'); }")

        # Aguardar transicao para ServerSelection
        start_time = time.time()
        while time.time() - start_time < 15:
            time.sleep(1)
            if any("ServerSelection" in l and ("ShowScreen" in l or "loaded" in l or "DisplayServer" in l or "servers parsed" in l) for l in console_logs):
                print(f"[OK] Tela ServerSelection carregada em {round(time.time() - start_time, 1)}s!")
                break

        time.sleep(3)
        shot_server = SCREENSHOT_DIR / "02_server_selection.png"
        page.screenshot(path=str(shot_server))
        print(f"[OK] Screenshot de ServerSelection salva: {shot_server}")

        # Passo 3: Clicar no botao PLAY / Selecionar servidor
        print("\n[Passo 3] Selecionando Servidor -> Reconnect kqz -> CharacterSelection...")
        if box:
            # Em ServerSelection, botao Play fica na parte inferior central
            page.mouse.click(box["x"] + (box["width"] / 2), box["y"] + int(box["height"] - 104))
            time.sleep(1)

        if not any("Server select:" in l for l in console_logs):
            print("  [INFO] Usando TestAction('enter') para validar selecao de servidor...")
            page.evaluate("() => { if (window.unityInstance) window.unityInstance.SendMessage('__Probe', 'TestAction', 'enter'); }")

        start_time = time.time()
        while time.time() - start_time < 15:
            time.sleep(1)
            if any("CharacterSelection" in l and ("loaded" in l or "characters" in l or "populated" in l) for l in console_logs):
                print(f"[OK] Tela CharacterSelection carregada em {round(time.time() - start_time, 1)}s!")
                break

        time.sleep(3)
        shot_char = SCREENSHOT_DIR / "03_character_selection.png"
        page.screenshot(path=str(shot_char))
        print(f"[OK] Screenshot de CharacterSelection salva: {shot_char}")

        # Passo 4: Clicar em Jogar / Selecionar personagem
        print("\n[Passo 4] Selecionando Personagem -> Entrada no Mundo (jru, lwt, kub, jss, HUD)...")
        if box:
            page.mouse.click(box["x"] + (box["width"] / 2), box["y"] + int(box["height"] - 104))
            time.sleep(1)

        if not any("Selecting character" in l for l in console_logs):
            print("  [INFO] Usando TestAction('enter') para validar entrada no mundo...")
            page.evaluate("() => { if (window.unityInstance) window.unityInstance.SendMessage('__Probe', 'TestAction', 'enter'); }")

        start_time = time.time()
        while time.time() - start_time < 20:
            time.sleep(1)
            if any(w in l for l in console_logs for w in ["BuildMapFromComposed", "MountInGameHud", "Map mesh", "lwt"]):
                print(f"[OK] Entrada no mundo detectada em {round(time.time() - start_time, 1)}s!")
                break

        print("Aguardando carga do mapa e montagem do HUD (8s)...")
        time.sleep(8)
        shot_world = SCREENSHOT_DIR / "04_world_map_hud.png"
        page.screenshot(path=str(shot_world))
        print(f"[OK] Screenshot do Mapa do Mundo & HUD salva: {shot_world}")

        browser.close()

    print("\n" + "=" * 70)
    print("  RESUMO DO FLUXO COMPLETO")
    print("=" * 70)
    print(f"Total de logs no console: {len(console_logs)}")
    print(f"Total de erros de pagina JS: {len(page_errors)}")

if __name__ == "__main__":
    run_full_flow()
