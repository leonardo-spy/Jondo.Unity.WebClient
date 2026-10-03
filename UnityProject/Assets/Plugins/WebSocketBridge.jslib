var WebSocketPlugin = {

    $wsState: {
        sockets: {},
        nextId: 1,
        inbox: {},
        events: {},
    },

    WS_Create__deps: ['$wsState'],
    WS_Create: function (urlPtr) {
        var url = UTF8ToString(urlPtr);
        var id = wsState.nextId++;
        wsState.inbox[id] = [];
        wsState.events[id] = [];

        try {
            var ws = new WebSocket(url);
            ws.binaryType = 'arraybuffer';
            wsState.sockets[id] = ws;

            ws.onopen = function () {
                console.log('[WS] #' + id + ' connected to ' + url);
                wsState.events[id].push(0);
            };

            ws.onmessage = function (e) {
                if (e.data instanceof ArrayBuffer) {
                    wsState.inbox[id].push(new Uint8Array(e.data));
                } else if (typeof e.data === 'string') {
                    var enc = new TextEncoder();
                    wsState.inbox[id].push(enc.encode(e.data));
                }
            };

            ws.onclose = function (ev) {
                console.log('[WS] #' + id + ' closed: code=' + ev.code);
                wsState.events[id].push(1000 + ev.code);
            };

            ws.onerror = function () {
                console.error('[WS] #' + id + ' error');
                wsState.events[id].push(-1);
            };
        } catch (ex) {
            console.error('[WS] create failed: ' + ex);
            wsState.events[id].push(-1);
        }

        return id;
    },

    WS_Send: function (id, bufPtr, bufLen) {
        var ws = wsState.sockets[id];
        if (!ws || ws.readyState !== 1) return 0;
        try {
            var data = new Uint8Array(bufLen);
            data.set(HEAPU8.subarray(bufPtr, bufPtr + bufLen));
            ws.send(data.buffer);
            return 1;
        } catch (ex) {
            console.error('[WS] send error: ' + ex);
            return 0;
        }
    },

    WS_Close: function (id, code) {
        var ws = wsState.sockets[id];
        if (ws) {
            try { ws.close(code || 1000); } catch (e) {}
        }
    },

    WS_Destroy: function (id) {
        var ws = wsState.sockets[id];
        if (ws) {
            try { ws.close(); } catch (e) {}
        }
        delete wsState.sockets[id];
        delete wsState.inbox[id];
        delete wsState.events[id];
    },

    WS_GetState: function (id) {
        var ws = wsState.sockets[id];
        if (!ws) return 3;
        return ws.readyState;
    },

    WS_GetMessageCount: function (id) {
        var q = wsState.inbox[id];
        return q ? q.length : 0;
    },

    WS_GetMessageSize: function (id) {
        var q = wsState.inbox[id];
        if (!q || q.length === 0) return 0;
        return q[0].length;
    },

    WS_ReadMessage: function (id, destPtr, destLen) {
        var q = wsState.inbox[id];
        if (!q || q.length === 0) return 0;
        var msg = q.shift();
        var len = Math.min(msg.length, destLen);
        HEAPU8.set(msg.subarray(0, len), destPtr);
        return len;
    },

    WS_GetEventCount: function (id) {
        var q = wsState.events[id];
        return q ? q.length : 0;
    },

    WS_ReadEvent: function (id) {
        var q = wsState.events[id];
        if (!q || q.length === 0) return -999;
        return q.shift();
    },

    // ─── HTTP fetch for HAAPI calls ────────────────────────────────────

    $httpState: {
        nextId: 1,
        results: {},
    },

    HTTP_Post__deps: ['$httpState'],
    HTTP_Post: function (urlPtr, bodyPtr, callbackId) {
        var url = UTF8ToString(urlPtr);
        var body = UTF8ToString(bodyPtr);
        var id = httpState.nextId++;

        fetch(url, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: body
        })
        .then(function (r) { return r.text(); })
        .then(function (text) {
            httpState.results[id] = { status: 200, body: text };
        })
        .catch(function (err) {
            httpState.results[id] = { status: 0, body: err.message };
        });

        return id;
    },

    HTTP_Get__deps: ['$httpState'],
    HTTP_Get: function (urlPtr, headersPtr, callbackId) {
        var url = UTF8ToString(urlPtr);
        var hdrs = UTF8ToString(headersPtr);
        var id = httpState.nextId++;
        var h = {};
        if (hdrs.length > 0) {
            try { h = JSON.parse(hdrs); } catch (e) {}
        }

        fetch(url, { method: 'GET', headers: h })
        .then(function (r) { return r.text(); })
        .then(function (text) {
            httpState.results[id] = { status: 200, body: text };
        })
        .catch(function (err) {
            httpState.results[id] = { status: 0, body: err.message };
        });

        return id;
    },

    HTTP_GetResult__deps: ['$httpState'],
    HTTP_GetResult: function (id, destPtr, destLen) {
        var r = httpState.results[id];
        if (!r) return -1;
        var bytes = new TextEncoder().encode(r.body);
        var len = Math.min(bytes.length, destLen);
        HEAPU8.set(bytes.subarray(0, len), destPtr);
        delete httpState.results[id];
        return r.status === 200 ? len : -len;
    },

    HTTP_IsReady__deps: ['$httpState'],
    HTTP_IsReady: function (id) {
        return httpState.results[id] ? 1 : 0;
    },
    // Expose probe trigger for Playwright testing
    Probe_ExposeTestHook: function () {
        window.triggerProbe = function(action) {
            // Send a synthetic keyboard event that Unity's Input System picks up
            var canvas = document.querySelector('canvas');
            if (!canvas) return 'no canvas';
            var keyMap = { 'enter': 13, 'escape': 27, 'h': 72, 'f': 70 };
            var keyCode = keyMap[action] || 0;
            if (!keyCode) return 'unknown action: ' + action;
            canvas.dispatchEvent(new KeyboardEvent('keydown', {
                keyCode: keyCode, which: keyCode, code: 'Enter',
                key: action === 'enter' ? 'Enter' : action,
                bubbles: true, cancelable: true
            }));
            setTimeout(function() {
                canvas.dispatchEvent(new KeyboardEvent('keyup', {
                    keyCode: keyCode, which: keyCode, code: 'Enter',
                    key: action === 'enter' ? 'Enter' : action,
                    bubbles: true, cancelable: true
                }));
            }, 50);
            return 'triggered: ' + action;
        };
        return 0;
    },
};

mergeInto(LibraryManager.library, WebSocketPlugin);
