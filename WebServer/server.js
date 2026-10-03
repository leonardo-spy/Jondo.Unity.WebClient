const express = require('express');
const http = require('http');
const https = require('https');
const net = require('net');
const { WebSocketServer } = require('ws');
const path = require('path');
const fs = require('fs');

// ─── Config ──────────────────────────────────────────────────────────────────

const PORT = 3000;
const EMU_HOST = '127.0.0.1';
const EMU_GAME_PORT = 5555;
const EMU_HAAPI_PORT = 8888;

// Where the Unity WebGL build lives (after AssetRipper + rebuild)
const WEBGL_BUILD_DIR = path.join(__dirname, 'unity-build');

// Where the client's streaming assets live (served on demand)
const ASSETS_DIR = path.resolve(__dirname, '../../Cliente 3.6.10.11/Dofus_Data/StreamingAssets/Content');

// ─── Express app ─────────────────────────────────────────────────────────────

const app = express();
const server = http.createServer(app);

// CORS for all routes
app.use((req, res, next) => {
  res.header('Access-Control-Allow-Origin', '*');
  res.header('Access-Control-Allow-Methods', 'GET, POST, OPTIONS');
  res.header('Access-Control-Allow-Headers', 'Content-Type');
  next();
});

// Asset bundles served from the client's Content directory (on-demand loading)
app.use('/assets', express.static(ASSETS_DIR, {
  maxAge: '7d',
  immutable: true,
}));

// HAAPI JSON proxy — forward /json/* directly (no prefix stripping needed)
app.use('/json', (req, res) => {
  const options = {
    hostname: EMU_HOST,
    port: EMU_HAAPI_PORT,
    path: req.originalUrl,
    method: req.method,
    headers: { ...req.headers, host: `${EMU_HOST}:${EMU_HAAPI_PORT}` },
  };
  const proxy = http.request(options, (proxyRes) => {
    res.writeHead(proxyRes.statusCode, proxyRes.headers);
    proxyRes.pipe(res);
  });
  proxy.on('error', (err) => { res.status(502).json({ error: 'HAAPI not reachable' }); });
  req.pipe(proxy);
});

// HAAPI proxy — forward /api/* to the emulator's HTTP service on port 8888
app.use('/api', (req, res) => {
  const options = {
    hostname: EMU_HOST,
    port: EMU_HAAPI_PORT,
    path: req.originalUrl,
    method: req.method,
    headers: { ...req.headers, host: `${EMU_HOST}:${EMU_HAAPI_PORT}` },
  };

  const proxy = http.request(options, (proxyRes) => {
    res.writeHead(proxyRes.statusCode, proxyRes.headers);
    proxyRes.pipe(res);
  });

  proxy.on('error', (err) => {
    console.error(`[HAAPI Proxy] ${err.message}`);
    res.status(502).json({ error: 'Emulator HAAPI not reachable' });
  });

  req.pipe(proxy);
});

// CDN proxy — forward /cdn/* to dofus2.cdn.ankama.com (avoids CORS)
app.use('/cdn', (req, res) => {
  const targetUrl = `https://dofus2.cdn.ankama.com${req.url}`;
  console.log(`[CDN] Proxying: ${targetUrl}`);

  https.get(targetUrl, (proxyRes) => {
    const headers = { ...proxyRes.headers };
    delete headers['content-encoding'];
    res.writeHead(proxyRes.statusCode, headers);
    proxyRes.pipe(res);
  }).on('error', (err) => {
    console.error(`[CDN Proxy] ${err.message}`);
    res.status(502).json({ error: 'CDN not reachable' });
  });
});

// Unity WebGL build — serve at root so relative paths in index.html work.
// Build/ TemplateData/ StreamingAssets/ all resolve from root.
const unityStaticOpts = {
  setHeaders: (res, filePath) => {
    const p = filePath.toLowerCase();
    if (p.endsWith('.br'))      res.set('Content-Encoding', 'br');
    if (p.endsWith('.gz'))      res.set('Content-Encoding', 'gzip');
    if (p.endsWith('.wasm') || p.endsWith('.wasm.gz') || p.endsWith('.wasm.br'))
      res.set('Content-Type', 'application/wasm');
    else if (p.endsWith('.js.gz') || p.endsWith('.js.br'))
      res.set('Content-Type', 'application/javascript');
    else if (p.endsWith('.data.gz') || p.endsWith('.data.br'))
      res.set('Content-Type', 'application/octet-stream');
  }
};
app.use(express.static(WEBGL_BUILD_DIR, unityStaticOpts));

// ─── WebSocket → TCP proxy (game protocol) ──────────────────────────────────

const wss = new WebSocketServer({ server, path: '/game-ws' });

wss.on('connection', (ws, req) => {
  const clientIp = req.socket.remoteAddress;
  console.log(`[WS] Browser connected from ${clientIp}`);

  const tcp = new net.Socket();

  tcp.connect(EMU_GAME_PORT, EMU_HOST, () => {
    console.log(`[WS] TCP bridge opened to ${EMU_HOST}:${EMU_GAME_PORT}`);
  });

  // Browser → TCP: forward binary messages as-is
  ws.on('message', (data) => {
    if (tcp.writable) {
      tcp.write(data);
    }
  });

  // TCP → Browser: forward raw bytes as binary WebSocket messages
  // The game protocol is varint-prefixed, so we need to frame properly.
  // Each complete frame = varint length + that many payload bytes.
  let buffer = Buffer.alloc(0);

  tcp.on('data', (chunk) => {
    buffer = Buffer.concat([buffer, chunk]);

    // Parse and forward complete frames
    while (buffer.length > 0) {
      const frame = tryReadFrame(buffer);
      if (!frame) break;
      ws.send(frame.raw, { binary: true });
      buffer = buffer.subarray(frame.totalLength);
    }
  });

  tcp.on('error', (err) => {
    console.error(`[WS] TCP error: ${err.message}`);
    ws.close(1011, 'TCP connection error');
  });

  tcp.on('close', () => {
    console.log('[WS] TCP connection closed');
    ws.close(1000, 'Game server disconnected');
  });

  ws.on('close', (code, reason) => {
    console.log(`[WS] Browser disconnected: ${code} ${reason}`);
    tcp.destroy();
  });

  ws.on('error', (err) => {
    console.error(`[WS] WebSocket error: ${err.message}`);
    tcp.destroy();
  });
});

// Read a varint-prefixed frame from a buffer. Returns null if incomplete.
function tryReadFrame(buf) {
  let length = 0;
  let shift = 0;
  let pos = 0;

  while (pos < buf.length) {
    const b = buf[pos++];
    length |= (b & 0x7F) << shift;
    if ((b & 0x80) === 0) {
      // Varint complete — check if full payload is available
      const totalLength = pos + length;
      if (buf.length >= totalLength) {
        return { raw: buf.subarray(0, totalLength), totalLength };
      }
      return null; // Payload incomplete
    }
    shift += 7;
    if (shift > 28) return null; // Varint too long
  }

  return null; // Varint incomplete
}

// ─── Start ───────────────────────────────────────────────────────────────────

server.listen(PORT, () => {
  console.log('');
  console.log('  ╔══════════════════════════════════════════════════╗');
  console.log('  ║     JondoEmu Web Client Server                  ║');
  console.log('  ╠══════════════════════════════════════════════════╣');
  console.log(`  ║  Web UI:      http://localhost:${PORT}              ║`);
  console.log(`  ║  Game WS:     ws://localhost:${PORT}/game-ws        ║`);
  console.log(`  ║  Assets:      http://localhost:${PORT}/assets/...   ║`);
  console.log(`  ║  HAAPI proxy: http://localhost:${PORT}/api/...      ║`);
  console.log(`  ║  CDN proxy:   http://localhost:${PORT}/cdn/...      ║`);
  console.log('  ╠══════════════════════════════════════════════════╣');
  console.log(`  ║  Emulator:    ${EMU_HOST}:${EMU_GAME_PORT} (TCP)            ║`);
  console.log(`  ║  HAAPI:       ${EMU_HOST}:${EMU_HAAPI_PORT} (HTTP)           ║`);
  console.log('  ╚══════════════════════════════════════════════════╝');
  console.log('');

  if (fs.existsSync(ASSETS_DIR)) {
    console.log(`  [OK] Assets dir found: ${ASSETS_DIR}`);
  } else {
    console.log(`  [!!] Assets dir NOT found: ${ASSETS_DIR}`);
    console.log('       Place the Unity client in "Cliente 3.6.10.11/" next to the emulator.');
  }

  const buildExists = fs.existsSync(path.join(WEBGL_BUILD_DIR, 'index.html'));
  if (buildExists) {
    console.log('  [OK] Unity WebGL build found — serving game at /');
  } else {
    console.log('  [..] No WebGL build yet — serving test page at /');
    console.log('       After building with Unity, place output in unity-build/');
  }

  console.log('');
});
