import subprocess
import os
import shutil
import sys
import time

UNITY_EXE = r"C:\Program Files\Unity\Hub\Editor\6000.3.16f1\Editor\Unity.exe"
PROJECT_PATH = os.path.abspath("UnityProject")
BUILD_LOG = os.path.abspath("build-auto.log")
BUILD_SRC = os.path.expanduser(r"~\Documents\Projetos\JondoEmu\PlayBrowser\WebClient\unity-build")
WEBSERVER = os.path.abspath("WebServer")

def main():
    print("=" * 65)
    print("  UNITY WEBGL BUILD & DEPLOY PIPELINE")
    print("=" * 65)
    print(f"Unity: {UNITY_EXE}")
    print(f"Project: {PROJECT_PATH}")
    print(f"Build destination: {BUILD_SRC}")
    print(f"WebServer destination: {WEBSERVER}")

    start_time = time.time()
    cmd = [
        UNITY_EXE,
        "-batchmode",
        "-nographics",
        "-projectPath", PROJECT_PATH,
        "-executeMethod", "WebGLBuildMenu.BuildWebGL",
        "-quit",
        "-logFile", BUILD_LOG
    ]

    print("\n[1/3] Executando Unity BuildWebGL (aguarde alguns minutos)...")
    result = subprocess.run(cmd)

    print(f"Unity finalizou com codigo de saida: {result.returncode}")

    if not os.path.exists(BUILD_LOG):
        print(f"[ERRO] Log de build nao encontrado em {BUILD_LOG}")
        sys.exit(1)

    with open(BUILD_LOG, "r", encoding="utf-8", errors="ignore") as f:
        log_content = f.read()

    if "Result: Succeeded" in log_content:
        print("[OK] Unity BuildWebGL completado com SUCESSO!")
    else:
        print("[ERRO] Build falhou ou nao reportou sucesso. Ultimas linhas do log:")
        for line in log_content.splitlines()[-30:]:
            print("  ", line)
        sys.exit(1)

    print("\n[2/3] Copiando arquivos compilados para WebServer/...")
    src_build = os.path.join(BUILD_SRC, "Build")
    dst_build = os.path.join(WEBSERVER, "Build")

    if os.path.exists(src_build):
        for item in os.listdir(src_build):
            s = os.path.join(src_build, item)
            d = os.path.join(dst_build, item)
            if os.path.isfile(s):
                shutil.copy2(s, d)
                print(f"  Copiado: {item} ({os.path.getsize(d)} bytes)")

    src_index = os.path.join(BUILD_SRC, "index.html")
    dst_index = os.path.join(WEBSERVER, "index.html")
    if os.path.exists(src_index):
        with open(src_index, "r", encoding="utf-8") as f:
            html = f.read()
        # Patch window.unityInstance
        target = "}).then((unityInstance) => {"
        replacement = "}).then((unityInstance) => { window.unityInstance = unityInstance;"
        if target in html and "window.unityInstance =" not in html:
            html = html.replace(target, replacement, 1)
        with open(dst_index, "w", encoding="utf-8") as f:
            f.write(html)
        print("  Copiado e patcheado: index.html")

    elapsed = round(time.time() - start_time, 1)
    print(f"\n[3/3] [BUILD & DEPLOY COMPLETO] em {elapsed}s!")

if __name__ == "__main__":
    main()

