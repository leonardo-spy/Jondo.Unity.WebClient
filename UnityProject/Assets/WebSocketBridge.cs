using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;

public static class WebSocketBridge
{
    public const int STATE_CONNECTING = 0;
    public const int STATE_OPEN = 1;
    public const int STATE_CLOSING = 2;
    public const int STATE_CLOSED = 3;

    public const int EVENT_OPEN = 0;
    public const int EVENT_ERROR = -1;

    // ────────────────────── jslib imports ────────────────────────────────

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] static extern int WS_Create(string url);
    [DllImport("__Internal")] static extern int WS_Send(int id, byte[] data, int length);
    [DllImport("__Internal")] static extern void WS_Close(int id, int code);
    [DllImport("__Internal")] static extern void WS_Destroy(int id);
    [DllImport("__Internal")] static extern int WS_GetState(int id);
    [DllImport("__Internal")] static extern int WS_GetMessageCount(int id);
    [DllImport("__Internal")] static extern int WS_GetMessageSize(int id);
    [DllImport("__Internal")] static extern int WS_ReadMessage(int id, byte[] dest, int destLen);
    [DllImport("__Internal")] static extern int WS_GetEventCount(int id);
    [DllImport("__Internal")] static extern int WS_ReadEvent(int id);
    [DllImport("__Internal")] static extern int HTTP_Post(string url, string body, int callbackId);
    [DllImport("__Internal")] static extern int HTTP_Get(string url, string headers, int callbackId);
    [DllImport("__Internal")] static extern int HTTP_GetResult(int id, byte[] dest, int destLen);
    [DllImport("__Internal")] static extern int HTTP_IsReady(int id);
    [DllImport("__Internal")] static extern int Probe_ExposeTestHook();
#else
    static int WS_Create(string url) => -1;
    static int WS_Send(int id, byte[] data, int length) => 0;
    static void WS_Close(int id, int code) { }
    static void WS_Destroy(int id) { }
    static int WS_GetState(int id) => STATE_CLOSED;
    static int WS_GetMessageCount(int id) => 0;
    static int WS_GetMessageSize(int id) => 0;
    static int WS_ReadMessage(int id, byte[] dest, int destLen) => 0;
    static int WS_GetEventCount(int id) => 0;
    static int WS_ReadEvent(int id) => -999;
    static int HTTP_Post(string url, string body, int callbackId) => -1;
    static int HTTP_Get(string url, string headers, int callbackId) => -1;
    static int HTTP_GetResult(int id, byte[] dest, int destLen) => -1;
    static int HTTP_IsReady(int id) => 0;
    static int Probe_ExposeTestHook() => 0;
#endif

    public static void ExposeTestHook() => Probe_ExposeTestHook();

    // ────────────────────── connection tracking ─────────────────────────

    public class Connection
    {
        public int Id;
        public string Url;
        public Action OnOpen;
        public Action<byte[]> OnData;
        public Action<int> OnClose;
        public Action OnError;
    }

    static readonly Dictionary<int, Connection> _conns = new();
    static readonly byte[] _readBuf = new byte[65536];

    // ────────────────────── public API ───────────────────────────────────

    public static Connection Connect(string url,
        Action onOpen = null, Action<byte[]> onData = null,
        Action<int> onClose = null, Action onError = null)
    {
        int id = WS_Create(url);
        var conn = new Connection
        {
            Id = id,
            Url = url,
            OnOpen = onOpen,
            OnData = onData,
            OnClose = onClose,
            OnError = onError,
        };
        _conns[id] = conn;
        Debug.Log($"[WSBridge] #{id} connecting to {url}");
        return conn;
    }

    public static bool Send(Connection conn, byte[] data)
    {
        if (conn == null) return false;
        return WS_Send(conn.Id, data, data.Length) != 0;
    }

    public static void Close(Connection conn, int code = 1000)
    {
        if (conn == null) return;
        WS_Close(conn.Id, code);
    }

    public static void Destroy(Connection conn)
    {
        if (conn == null) return;
        WS_Destroy(conn.Id);
        _conns.Remove(conn.Id);
    }

    public static int GetState(Connection conn)
    {
        if (conn == null) return STATE_CLOSED;
        return WS_GetState(conn.Id);
    }

    public static bool IsOpen(Connection conn) => GetState(conn) == STATE_OPEN;

    // ────────────────────── polling (call from Update) ──────────────────

    public static void PollAll()
    {
        foreach (var kv in _conns)
            PollOne(kv.Value);
    }

    static void PollOne(Connection conn)
    {
        int id = conn.Id;

        while (WS_GetEventCount(id) > 0)
        {
            int ev = WS_ReadEvent(id);
            if (ev == EVENT_OPEN)
            {
                Debug.Log($"[WSBridge] #{id} opened");
                conn.OnOpen?.Invoke();
            }
            else if (ev == EVENT_ERROR)
            {
                Debug.LogError($"[WSBridge] #{id} error");
                conn.OnError?.Invoke();
            }
            else if (ev > 1000)
            {
                int code = ev - 1000;
                Debug.Log($"[WSBridge] #{id} closed code={code}");
                conn.OnClose?.Invoke(code);
            }
        }

        while (WS_GetMessageCount(id) > 0)
        {
            int size = WS_GetMessageSize(id);
            if (size <= 0) break;

            byte[] buf = size <= _readBuf.Length ? _readBuf : new byte[size];
            int read = WS_ReadMessage(id, buf, buf.Length);
            if (read > 0)
            {
                byte[] data = new byte[read];
                Buffer.BlockCopy(buf, 0, data, 0, read);
                conn.OnData?.Invoke(data);
            }
        }
    }

    // ────────────────────── HTTP (HAAPI calls) ──────────────────────────

    public static int PostJson(string url, string jsonBody)
    {
        return HTTP_Post(url, jsonBody, 0);
    }

    public static int GetJson(string url, string headersJson = "")
    {
        return HTTP_Get(url, headersJson, 0);
    }

    public static bool IsHttpReady(int requestId) => HTTP_IsReady(requestId) != 0;

    public static string ReadHttpResult(int requestId)
    {
        var buf = new byte[65536];
        int result = HTTP_GetResult(requestId, buf, buf.Length);
        if (result == -1) return null;
        int len = Math.Abs(result);
        return System.Text.Encoding.UTF8.GetString(buf, 0, len);
    }

    // ────────────────────── Protobuf helpers ────────────────────────────
    // Hand-encode the Connection Server auth message (no protobuf library needed)

    public static byte[] BuildAuthFrame(string token, string lang = "en")
    {
        // TokenData { field 1 (token): string }
        var tokenBytes = System.Text.Encoding.UTF8.GetBytes(token);
        var tokenData = ProtobufField(1, tokenBytes);

        // AuthenticationTicket { field 3 (tokenData): message }
        var ticket = ProtobufField(3, tokenData);

        // AuthenticationTicketMessage { field 1 (lang): string, field 3 (ticket): message }
        var langBytes = System.Text.Encoding.UTF8.GetBytes(lang);
        var authMsg = Concat(ProtobufField(1, langBytes), ProtobufField(3, ticket));

        // GameMessage { field 1 (auth): message }
        var gameMsg = ProtobufField(1, authMsg);

        // Varint-length prefix + payload
        return Concat(WriteVarint(gameMsg.Length), gameMsg);
    }

    public static byte[] BuildServerSelectFrame(int serverId, string lang = "en")
    {
        // SelectedServerSelection { field 1 (serverId): int32 }
        var serverSel = Concat(WriteVarint((1 << 3) | 0), WriteVarint(serverId));

        // AuthenticationTicketMessage { field 1 (lang): string, field 4 (selectedServer): message }
        var langBytes = System.Text.Encoding.UTF8.GetBytes(lang);
        var authMsg = Concat(ProtobufField(1, langBytes), ProtobufField(4, serverSel));

        // GameMessage { field 1 (auth): message }
        var gameMsg = ProtobufField(1, authMsg);

        return Concat(WriteVarint(gameMsg.Length), gameMsg);
    }

    public static byte[] ProtobufField(int fieldNumber, byte[] value)
    {
        var tag = WriteVarint((fieldNumber << 3) | 2);
        var len = WriteVarint(value.Length);
        return Concat(tag, len, value);
    }

    public static byte[] WriteVarint(int value)
    {
        var ms = new MemoryStream(5);
        uint v = (uint)value;
        while (v >= 0x80)
        {
            ms.WriteByte((byte)(v | 0x80));
            v >>= 7;
        }
        ms.WriteByte((byte)v);
        return ms.ToArray();
    }

    // Build a framed protobuf message following ConnectionProtocol.Push format:
    // f1 { f1: bytes(any) } where any = { f1: "type.ankama.com/{opcode}", f2: payload }
    public static byte[] BuildProtobufFrame(string opcode, byte[] payload)
    {
        var typeUrl = System.Text.Encoding.UTF8.GetBytes("type.ankama.com/" + opcode);
        var any = Concat(ProtobufField(1, typeUrl));
        if (payload != null && payload.Length > 0)
            any = Concat(any, ProtobufField(2, payload));
        var inner = ProtobufField(1, any);
        var outer = ProtobufField(1, inner);
        return Concat(WriteVarint(outer.Length), outer);
    }

    public static byte[] Concat(params byte[][] arrays)
    {
        int total = 0;
        foreach (var a in arrays) total += a.Length;
        var result = new byte[total];
        int pos = 0;
        foreach (var a in arrays)
        {
            Buffer.BlockCopy(a, 0, result, pos, a.Length);
            pos += a.Length;
        }
        return result;
    }
}
