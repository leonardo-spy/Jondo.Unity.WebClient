#!/bin/bash
# Start all services needed for WebGL testing.
# Usage: bash PlayBrowser/start-webgl.sh
# Kills anything on ports 3000/5555/8888 first, then starts emulator + node server.

REPO_DIR="$(cd "$(dirname "$0")/.." && pwd)"
EMU_EXE="$REPO_DIR/Jondo Server.exe"
NODE_SERVER="$REPO_DIR/PlayBrowser/WebClient/server.js"

# Kill processes on our ports
for PORT in 3000 5555 8888; do
  PID=$(netstat -ano 2>/dev/null | grep ":${PORT} " | grep LISTENING | awk '{print $5}' | head -1)
  if [ -n "$PID" ] && [ "$PID" != "0" ]; then
    echo "[start-webgl] Killing PID $PID on port $PORT"
    taskkill //F //PID "$PID" 2>/dev/null
    sleep 1
  fi
done

# Start emulator in background
if [ -f "$EMU_EXE" ]; then
  echo "[start-webgl] Starting emulator: $EMU_EXE"
  "$EMU_EXE" &
  EMU_PID=$!
  echo "[start-webgl] Emulator PID=$EMU_PID"
else
  echo "[start-webgl] ERROR: Emulator not found at $EMU_EXE"
  echo "[start-webgl] Run: dotnet publish Jondo.Unity.Server -c Release"
  exit 1
fi

# Wait for emulator to bind ports
echo "[start-webgl] Waiting for emulator ports (5555, 8888)..."
for i in $(seq 1 15); do
  if netstat -ano 2>/dev/null | grep -q ":5555.*LISTENING"; then
    echo "[start-webgl] Emulator ready (${i}s)"
    break
  fi
  sleep 1
done

# Start node server in background
if [ -f "$NODE_SERVER" ]; then
  echo "[start-webgl] Starting node server: $NODE_SERVER"
  cd "$(dirname "$NODE_SERVER")"
  node server.js &
  NODE_PID=$!
  echo "[start-webgl] Node server PID=$NODE_PID"
  cd "$REPO_DIR"
else
  echo "[start-webgl] ERROR: Node server not found at $NODE_SERVER"
  exit 1
fi

# Wait for node server
for i in $(seq 1 10); do
  if netstat -ano 2>/dev/null | grep -q ":3000.*LISTENING"; then
    echo "[start-webgl] Node server ready (${i}s)"
    break
  fi
  sleep 1
done

echo ""
echo "[start-webgl] ALL SERVICES RUNNING"
echo "  Emulator:    PID=$EMU_PID (ports 5555, 8888)"
echo "  Node server: PID=$NODE_PID (port 3000)"
echo "  WebGL URL:   http://localhost:3000"
echo ""
echo "Press Ctrl+C to stop all services."

# Wait for Ctrl+C, then cleanup
trap "echo '[start-webgl] Stopping...'; kill $EMU_PID $NODE_PID 2>/dev/null; exit 0" INT TERM
wait
