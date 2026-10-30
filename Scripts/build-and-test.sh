#!/bin/bash
# One-command build → deploy → start services → test → screenshot
# Usage: bash Scripts/build-and-test.sh [--skip-build] [--skip-emulator] [--validate-boot]

set -e
REPO_DIR="$(cd "$(dirname "$0")/.." && pwd)"
UNITY_PATH="${UNITY_PATH:-/c/Program Files/Unity/Hub/Editor/6000.3.16f1/Editor/Unity.exe}"
PROJECT_PATH="$REPO_DIR/UnityProject"
BUILD_SRC="$HOME/Documents/Projetos/JondoEmu/PlayBrowser/WebClient/unity-build"
WEBSERVER="$REPO_DIR/WebServer"
EMU_EXE="$(dirname "$REPO_DIR")/Jondo Server.exe"
LOG="$REPO_DIR/build-auto.log"

SKIP_BUILD=false
SKIP_EMU=false
VALIDATE_BOOT=false
for arg in "$@"; do
  case "$arg" in
    --skip-build) SKIP_BUILD=true ;;
    --skip-emulator) SKIP_EMU=true ;;
    --validate-boot) VALIDATE_BOOT=true ;;
  esac
done

if [ "$VALIDATE_BOOT" = true ]; then
  cd "$REPO_DIR"
  py.exe -3.12 -m unittest discover -s Tools/ClientAnalyzer -p test_translate_boot.py -v
  py.exe -3.12 -m unittest discover -s Tools/UnityPyTools -p test_extract_all_runtime_data.py -v
  py.exe -3.12 -m unittest discover -s Tools/UnityPyTools -p test_cell_grid.py -v
  if [ -n "${REGISTRATION_RUN:-}" ]; then
    py.exe -3.12 Tools/UnityPyTools/extract_all_runtime_data.py --registration-observations "$REGISTRATION_RUN"
  fi
  py.exe -3.12 Tools/ClientAnalyzer/translate_boot.py --deploy
  py.exe -3.12 Tools/UnityPyTools/generate_cell_grid.py --deploy
  py.exe -3.12 Tools/UnityPyTools/extract_all_runtime_data.py --renderer-contract
  py.exe -3.12 Tools/UnityPyTools/compose_map_atlas.py --all
  BOOT_LOG="$REPO_DIR/Generated/output/boot-validation-unity.log"
  "$UNITY_PATH" -batchmode -nographics -projectPath "$PROJECT_PATH" \
    -executeMethod WebGLBuildMenu.ValidateBootLogic -quit -logFile "$BOOT_LOG"
  if ! grep -Fq '[BootValidation] PASS' "$BOOT_LOG"; then
    echo "Boot validation failed: $BOOT_LOG"
    exit 1
  fi
  grep -F '[BootValidation] PASS' "$BOOT_LOG"
  exit 0
fi

echo "=== Build & Test Pipeline ==="
echo "$(date)"

# 1. Build (skip with --skip-build for data-only changes)
if [ "$SKIP_BUILD" = false ]; then
  echo "[1/5] Building WebGL..."
  "$UNITY_PATH" -batchmode -nographics -projectPath "$PROJECT_PATH" \
    -executeMethod WebGLBuildMenu.BuildWebGL -quit -logFile "$LOG" 2>&1

  if grep -q "Result: Succeeded" "$LOG"; then
    echo "  Build OK"
  else
    echo "  BUILD FAILED — check $LOG"
    exit 1
  fi

  # Deploy
  cp "$BUILD_SRC/Build/"* "$WEBSERVER/Build/"
  cp "$BUILD_SRC/index.html" "$WEBSERVER/index.html"
  sed -i 's/}).then((unityInstance) => {/}).then((unityInstance) => { window.unityInstance = unityInstance;/' "$WEBSERVER/index.html"
  echo "  Deployed"
else
  echo "[1/5] SKIP build (--skip-build)"
fi

# 2. Start emulator
if [ "$SKIP_EMU" = false ]; then
  echo "[2/5] Starting emulator..."
  taskkill //F //IM "Jondo Server.exe" 2>/dev/null || true
  sleep 2
  "$EMU_EXE" > /dev/null 2>&1 &
  sleep 15
  echo "  Emulator started"
else
  echo "[2/5] SKIP emulator (--skip-emulator)"
fi

# 3. Start web server
echo "[3/5] Starting web server..."
taskkill //F //PID $(netstat -ano 2>/dev/null | grep ":3000.*LISTEN" | awk '{print $5}' | head -1) 2>/dev/null || true
sleep 1
cd "$WEBSERVER" && node server.js > /dev/null 2>&1 &
sleep 3

# 4. Verify services
echo "[4/5] Verifying services..."
ALL_OK=true
for port in 3000 5555 8888; do
  if netstat -ano 2>/dev/null | grep ":$port.*LISTEN" > /dev/null; then
    echo "  Port $port: OK"
  else
    echo "  Port $port: FAIL"
    ALL_OK=false
  fi
done

if [ "$ALL_OK" = false ]; then
  echo "  Some services not ready"
  exit 1
fi

echo "[5/5] Ready at http://localhost:3000"
echo "  To test: open browser or use Playwright MCP"
echo "  SendMessage flow: enter (12s) enter (12s) enter → World + Map"
