using System;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

// Direct FMOD P/Invoke proxy for WebGL — bypasses the reflection+enum marshalling failure.
// SOURCE: BootLogic.json → AudioManager.<InitializationRoutine>d__25 (dump.cs L33428)
// REFERENCE: Tools/FMODUnity/Assets/Plugins/FMOD/src/fmod_studio.cs (official wrappers)
// WHY: MethodInfo.Invoke() on a P/Invoke with FMOD.RESULT enum return type fails on
//   IL2CPP WebGL ("Cannot marshal type 'FMOD.RESULT'"). Direct compiled [DllImport]
//   with int return type generates correct thunks at build time.
// VERSION: libfmodstudio.a is FMOD 2.03.14 → header 0x00020314
// IF CLIENT UPDATES: check FMOD version in Ankama.AudioManagement.dll VERSION.number const
public static class FMODProxy
{
    const uint HEADER_VERSION = 0x00020314;

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    static extern int FMOD_Studio_System_Create(out IntPtr system, uint headerversion);

    [DllImport("__Internal")]
    static extern int FMOD_Studio_System_Initialize(IntPtr system, int maxchannels,
        uint studioflags, uint flags, IntPtr extradriverdata);

    [DllImport("__Internal")]
    static extern int FMOD_Studio_System_Release(IntPtr system);

    [DllImport("__Internal")]
    static extern int FMOD_Studio_System_Update(IntPtr system);

    [DllImport("__Internal")]
    static extern int FMOD_Studio_System_LoadBankMemory(IntPtr system, IntPtr buffer,
        int length, int mode, uint flags, out IntPtr bank);

    [DllImport("__Internal")]
    static extern int FMOD_Studio_System_GetEvent(IntPtr system, byte[] path, out IntPtr desc);

    [DllImport("__Internal")]
    static extern int FMOD_Studio_EventDescription_CreateInstance(IntPtr desc, out IntPtr instance);

    [DllImport("__Internal")]
    static extern int FMOD_Studio_EventInstance_Start(IntPtr instance);

    [DllImport("__Internal")]
    static extern int FMOD_Studio_EventInstance_Stop(IntPtr instance, int mode);

    [DllImport("__Internal")]
    static extern int FMOD_Studio_EventInstance_Release(IntPtr instance);

    [DllImport("__Internal")]
    static extern int FMOD_Studio_EventInstance_SetVolume(IntPtr instance, float volume);

    [DllImport("__Internal")]
    static extern int FMOD_Studio_EventInstance_SetPaused(IntPtr instance, int paused);

    [DllImport("__Internal")]
    static extern int FMOD_Studio_Bank_GetEventCount(IntPtr bank, out int count);

    [DllImport("__Internal")]
    static extern int FMOD_Studio_Bank_GetEventList(IntPtr bank, IntPtr[] array, int capacity, out int count);

    [DllImport("__Internal")]
    static extern int FMOD_Studio_EventDescription_GetPath(IntPtr desc, IntPtr path, int size, out int retrieved);
#else
    static int FMOD_Studio_System_Create(out IntPtr system, uint v)
    { system = IntPtr.Zero; return -1; }
    static int FMOD_Studio_System_Initialize(IntPtr s, int mc, uint sf, uint f, IntPtr e) => -1;
    static int FMOD_Studio_System_Release(IntPtr s) => -1;
    static int FMOD_Studio_System_Update(IntPtr s) => -1;
    static int FMOD_Studio_System_LoadBankMemory(IntPtr s, IntPtr b, int l, int m, uint f,
        out IntPtr bank) { bank = IntPtr.Zero; return -1; }
    static int FMOD_Studio_System_GetEvent(IntPtr s, byte[] p, out IntPtr d)
    { d = IntPtr.Zero; return -1; }
    static int FMOD_Studio_EventDescription_CreateInstance(IntPtr d, out IntPtr i)
    { i = IntPtr.Zero; return -1; }
    static int FMOD_Studio_EventInstance_Start(IntPtr i) => -1;
    static int FMOD_Studio_EventInstance_Stop(IntPtr i, int m) => -1;
    static int FMOD_Studio_EventInstance_Release(IntPtr i) => -1;
    static int FMOD_Studio_EventInstance_SetVolume(IntPtr i, float v) => -1;
    static int FMOD_Studio_EventInstance_SetPaused(IntPtr i, int p) => -1;
    static int FMOD_Studio_Bank_GetEventCount(IntPtr b, out int c) { c = 0; return -1; }
    static int FMOD_Studio_Bank_GetEventList(IntPtr b, IntPtr[] a, int cap, out int c)
    { c = 0; return -1; }
    static int FMOD_Studio_EventDescription_GetPath(IntPtr d, IntPtr p, int s, out int r)
    { r = 0; return -1; }
#endif

    static IntPtr _system;
    static bool _initialized;
    static IntPtr _musicInstance;

    public static bool IsInitialized => _initialized;
    public static IntPtr System => _system;

    public static int Init(int maxChannels = 128)
    {
        if (_initialized) return 0;

        int result = FMOD_Studio_System_Create(out _system, HEADER_VERSION);
        if (result != 0)
        {
            Debug.Log($"[FMODProxy] Create failed: result={result}");
            return result;
        }
        Debug.Log($"[FMODProxy] System created: ptr={_system}");

        result = FMOD_Studio_System_Initialize(_system, maxChannels, 0, 0, IntPtr.Zero);
        if (result != 0)
        {
            Debug.Log($"[FMODProxy] Initialize failed: result={result}");
            FMOD_Studio_System_Release(_system);
            _system = IntPtr.Zero;
            return result;
        }

        _initialized = true;
        Debug.Log($"[FMODProxy] FMOD Studio 2.03.14 initialized (channels={maxChannels})");
        return 0;
    }

    public static int LoadBankFromMemory(byte[] data, out IntPtr bank)
    {
        bank = IntPtr.Zero;
        if (!_initialized || _system == IntPtr.Zero) return -1;

        var pin = GCHandle.Alloc(data, GCHandleType.Pinned);
        try
        {
            int result = FMOD_Studio_System_LoadBankMemory(
                _system, pin.AddrOfPinnedObject(), data.Length,
                0 /* LOAD_MEMORY */, 0 /* NORMAL */, out bank);
            if (result != 0)
                Debug.Log($"[FMODProxy] LoadBank failed: result={result} size={data.Length}");
            else
                Debug.Log($"[FMODProxy] Bank loaded: ptr={bank} size={data.Length}");
            return result;
        }
        finally { pin.Free(); }
    }

    public static void LogBankEvents(IntPtr bank)
    {
        if (bank == IntPtr.Zero) return;
        int r = FMOD_Studio_Bank_GetEventCount(bank, out int count);
        if (r != 0) { Debug.Log($"[FMODProxy] GetEventCount failed: {r}"); return; }
        Debug.Log($"[FMODProxy] Bank has {count} events");
        if (count == 0) return;

        var descs = new IntPtr[Math.Min(count, 50)];
        r = FMOD_Studio_Bank_GetEventList(bank, descs, descs.Length, out int retrieved);
        if (r != 0) { Debug.Log($"[FMODProxy] GetEventList failed: {r}"); return; }

        var buf = Marshal.AllocHGlobal(512);
        for (int i = 0; i < retrieved; i++)
        {
            r = FMOD_Studio_EventDescription_GetPath(descs[i], buf, 512, out int len);
            if (r == 0 && len > 0)
            {
                string path = Marshal.PtrToStringAnsi(buf, len - 1);
                Debug.Log($"[FMODProxy]   event[{i}]: {path}");
            }
        }
        Marshal.FreeHGlobal(buf);
    }

    public static int PlayEvent(string eventPath, float volume = 1f)
    {
        if (!_initialized || _system == IntPtr.Zero) return -1;

        byte[] pathBytes = Encoding.UTF8.GetBytes(eventPath + "\0");
        int r = FMOD_Studio_System_GetEvent(_system, pathBytes, out IntPtr desc);
        if (r != 0)
        {
            Debug.Log($"[FMODProxy] GetEvent failed: result={r} path={eventPath}");
            return r;
        }

        r = FMOD_Studio_EventDescription_CreateInstance(desc, out IntPtr instance);
        if (r != 0)
        {
            Debug.Log($"[FMODProxy] CreateInstance failed: result={r}");
            return r;
        }

        if (volume < 1f) FMOD_Studio_EventInstance_SetVolume(instance, volume);

        r = FMOD_Studio_EventInstance_Start(instance);
        if (r != 0)
        {
            Debug.Log($"[FMODProxy] Start failed: result={r}");
            FMOD_Studio_EventInstance_Release(instance);
            return r;
        }

        Debug.Log($"[FMODProxy] Playing: {eventPath}");
        return 0;
    }

    public static int PlayMusic(string eventPath, float volume = 0.5f)
    {
        StopMusic();

        if (!_initialized || _system == IntPtr.Zero) return -1;

        byte[] pathBytes = Encoding.UTF8.GetBytes(eventPath + "\0");
        int r = FMOD_Studio_System_GetEvent(_system, pathBytes, out IntPtr desc);
        if (r != 0) { Debug.Log($"[FMODProxy] Music GetEvent failed: {r} path={eventPath}"); return r; }

        r = FMOD_Studio_EventDescription_CreateInstance(desc, out _musicInstance);
        if (r != 0) { Debug.Log($"[FMODProxy] Music CreateInstance failed: {r}"); return r; }

        FMOD_Studio_EventInstance_SetVolume(_musicInstance, volume);
        r = FMOD_Studio_EventInstance_Start(_musicInstance);
        Debug.Log(r == 0 ? $"[FMODProxy] Music playing: {eventPath}" : $"[FMODProxy] Music start failed: {r}");
        return r;
    }

    public static void StopMusic()
    {
        if (_musicInstance != IntPtr.Zero)
        {
            FMOD_Studio_EventInstance_Stop(_musicInstance, 0);
            FMOD_Studio_EventInstance_Release(_musicInstance);
            _musicInstance = IntPtr.Zero;
        }
    }

    public static void PauseMusic()
    {
        if (_musicInstance != IntPtr.Zero)
            FMOD_Studio_EventInstance_SetPaused(_musicInstance, 1);
    }

    public static void ResumeMusic()
    {
        if (_musicInstance != IntPtr.Zero)
            FMOD_Studio_EventInstance_SetPaused(_musicInstance, 0);
    }

    public static void Update()
    {
        if (_initialized && _system != IntPtr.Zero)
            FMOD_Studio_System_Update(_system);
    }

    public static void Shutdown()
    {
        StopMusic();
        if (_system != IntPtr.Zero)
        {
            FMOD_Studio_System_Release(_system);
            _system = IntPtr.Zero;
            _initialized = false;
            Debug.Log("[FMODProxy] Shutdown");
        }
    }
}
