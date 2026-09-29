// Step C native bridge (docs/CAMERA_HANDOFF.md): loads the version-checked
// camera_shim.dll and runs its self-test. The shim holds only ABI glue into
// the host engine (near plane, absolute camera Y/look-at, FilterCamera ray);
// if it is missing or the engine build does not match, the client keeps the
// current managed behaviour and the deviations register applies.
using System;
using System.Runtime.InteropServices;

internal static class CameraShim
{
    [DllImport("camera_shim.dll", CallingConvention = CallingConvention.Cdecl)]
    static extern int RC_Shim_Init();

    [DllImport("camera_shim.dll", CallingConvention = CallingConvention.Cdecl)]
    static extern IntPtr RC_Shim_Status();

    [DllImport("camera_shim.dll", CallingConvention = CallingConvention.Cdecl)]
    static extern int RC_Shim_AbiSelfTest();

    [DllImport("camera_shim.dll", CallingConvention = CallingConvention.Cdecl)]
    static extern IntPtr RC_Probe_FindObjects(uint range);

    [DllImport("camera_shim.dll", CallingConvention = CallingConvention.Cdecl)]
    static extern IntPtr RC_Probe_Dump(IntPtr obj, uint off);

    [DllImport("camera_shim.dll", CallingConvention = CallingConvention.Cdecl)]
    static extern IntPtr RC_Probe_ScanModule();

    [DllImport("camera_shim.dll", CallingConvention = CallingConvention.Cdecl)]
    static extern IntPtr RC_Probe_Deep(uint range);

    [DllImport("camera_shim.dll", CallingConvention = CallingConvention.Cdecl)]
    static extern int RC_Probe_ObjectCount();

    [DllImport("camera_shim.dll", CallingConvention = CallingConvention.Cdecl)]
    static extern IntPtr RC_Probe_Object(int i);

    [DllImport("camera_shim.dll", CallingConvention = CallingConvention.Cdecl)]
    static extern IntPtr RC_Probe_ObjectClass(int i);

    [DllImport("camera_shim.dll", CallingConvention = CallingConvention.Cdecl)]
    static extern IntPtr RC_Probe_FindTriple(IntPtr obj, uint len, float x, float y, float z);

    [DllImport("camera_shim.dll", CallingConvention = CallingConvention.Cdecl)]
    static extern IntPtr RC_Probe_DumpQ(IntPtr obj, uint off);

    [DllImport("camera_shim.dll", CallingConvention = CallingConvention.Cdecl)]
    static extern IntPtr RC_Probe_DumpF(IntPtr obj, uint off, uint count);

    [DllImport("camera_shim.dll", CallingConvention = CallingConvention.Cdecl)]
    static extern IntPtr RC_Probe_ReadQ(IntPtr va);

    [DllImport("camera_shim.dll", CallingConvention = CallingConvention.Cdecl)]
    static extern int RC_Probe_WriteF(IntPtr obj, uint off, float v);

    [DllImport("camera_shim.dll", CallingConvention = CallingConvention.Cdecl)]
    static extern int RC_CamBind(float x, float y, float z);

    [DllImport("camera_shim.dll", CallingConvention = CallingConvention.Cdecl)]
    static extern int RC_CamSet(float x, float y, float z, float tx, float ty, float tz, int writeTarget);

    [DllImport("camera_shim.dll", CallingConvention = CallingConvention.Cdecl)]
    static extern IntPtr RC_Probe_FindAll(float x, float z);

    [DllImport("camera_shim.dll", CallingConvention = CallingConvention.Cdecl)]
    static extern int RC_Probe_ObjectOff(int i);

    [DllImport("camera_shim.dll", CallingConvention = CallingConvention.Cdecl)]
    static extern int RC_CamSetIndex(int i, float x, float y, float z,
                                     float tx, float ty, float tz);

    [DllImport("camera_shim.dll", CallingConvention = CallingConvention.Cdecl)]
    static extern IntPtr RC_Probe_InputPtr();

    [DllImport("camera_shim.dll", CallingConvention = CallingConvention.Cdecl)]
    static extern int RC_Probe_Snap(IntPtr obj, uint len);

    [DllImport("camera_shim.dll", CallingConvention = CallingConvention.Cdecl)]
    static extern IntPtr RC_Probe_SnapDiff();

    [DllImport("camera_shim.dll", CallingConvention = CallingConvention.Cdecl)]
    static extern IntPtr RC_CamInfo();

    [DllImport("camera_shim.dll", CallingConvention = CallingConvention.Cdecl)]
    static extern IntPtr RC_ModuleOf(IntPtr p);

    [DllImport("camera_shim.dll", CallingConvention = CallingConvention.Cdecl)]
    static extern IntPtr RC_Probe_ReadP(IntPtr va);

    [DllImport("camera_shim.dll", CallingConvention = CallingConvention.Cdecl)]
    static extern IntPtr RC_SceneCamRead(IntPtr scene);

    [DllImport("camera_shim.dll", CallingConvention = CallingConvention.Cdecl)]
    static extern int RC_CamGetVt(IntPtr cam, float[] outPos, float[] outTgt);

    [DllImport("camera_shim.dll", CallingConvention = CallingConvention.Cdecl)]
    static extern int RC_CamSetVt3(IntPtr cam, float x, float y, float z,
                                   float tx, float ty, float tz);

    static string Str(IntPtr p)
    {
        return p != IntPtr.Zero ? Marshal.PtrToStringAnsi(p) : "(null)";
    }

    public static string ModuleOf(IntPtr p)
    {
        try { return Str(RC_ModuleOf(p)); } catch { return "(shim off)"; }
    }

    public static IntPtr ReadP(IntPtr va)
    {
        try { return RC_Probe_ReadP(va); } catch { return IntPtr.Zero; }
    }

    public static IntPtr SceneCam(IntPtr scene)
    {
        try { return RC_SceneCamRead(scene); } catch { return IntPtr.Zero; }
    }

    public static int CamGetVt(IntPtr cam, float[] pos, float[] tgt)
    {
        try { return RC_CamGetVt(cam, pos, tgt); } catch { return -99; }
    }

    public static int CamSetVt3(IntPtr cam, float x, float y, float z,
                                float tx, float ty, float tz)
    {
        try { return RC_CamSetVt3(cam, x, y, z, tx, ty, tz); } catch { return -99; }
    }

    public static string FindObjects(uint range)
    {
        if (!Available) return "shim not available";
        try { return Str(RC_Probe_FindObjects(range)); }
        catch (Exception e) { return "probe ex: " + e.Message; }
    }

    public static string Dump(IntPtr obj, uint off)
    {
        if (!Available) return "shim not available";
        try { return Str(RC_Probe_Dump(obj, off)); }
        catch (Exception e) { return "dump ex: " + e.Message; }
    }

    public static string ScanModule()
    {
        if (!Available) return "shim not available";
        try { return Str(RC_Probe_ScanModule()); }
        catch (Exception e) { return "scan ex: " + e.Message; }
    }

    public static string Deep(uint range)
    {
        if (!Available) return "shim not available";
        try { return Str(RC_Probe_Deep(range)); }
        catch (Exception e) { return "deep ex: " + e.Message; }
    }

    public static int ObjectCount()
    {
        try { return RC_Probe_ObjectCount(); } catch { return 0; }
    }

    public static IntPtr Object(int i)
    {
        try { return RC_Probe_Object(i); } catch { return IntPtr.Zero; }
    }

    public static string ObjectClass(int i)
    {
        try { return Str(RC_Probe_ObjectClass(i)); } catch { return "?"; }
    }

    public static string FindTriple(IntPtr obj, uint len, float x, float y, float z)
    {
        try { return Str(RC_Probe_FindTriple(obj, len, x, y, z)); }
        catch (Exception e) { return "triple ex: " + e.Message; }
    }

    public static string DumpQ(IntPtr obj, uint off)
    {
        try { return Str(RC_Probe_DumpQ(obj, off)); }
        catch (Exception e) { return "dumpq ex: " + e.Message; }
    }

    public static string DumpF(IntPtr obj, uint off, uint count)
    {
        try { return Str(RC_Probe_DumpF(obj, off, count)); }
        catch (Exception e) { return "dumpf ex: " + e.Message; }
    }

    public static string ReadQ(IntPtr va)
    {
        try { return Str(RC_Probe_ReadQ(va)); }
        catch (Exception e) { return "readq ex: " + e.Message; }
    }

    public static int WriteF(IntPtr obj, uint off, float v)
    {
        try { return RC_Probe_WriteF(obj, off, v); }
        catch { return -3; }
    }

    public static bool Bound;
    // Engine camera resolved through the CLR scene proxy (m_pScene -> vt[+0x50]);
    // the engine-faithful set path uses this pointer's own setters.
    public static IntPtr EngineCam = IntPtr.Zero;

    public static int CamBind(float x, float y, float z)
    {
        try
        {
            int rc = RC_CamBind(x, y, z);
            if (rc == 0) Bound = true;
            return rc;
        }
        catch { return -4; }
    }

    public static int CamSet(float x, float y, float z, float tx, float ty, float tz, bool writeTarget)
    {
        try { return RC_CamSet(x, y, z, tx, ty, tz, writeTarget ? 1 : 0); }
        catch { return -4; }
    }

    [DllImport("camera_shim.dll", CallingConvention = CallingConvention.Cdecl)]
    static extern int RC_CamSetVt(float x, float y, float z, float tx, float ty, float tz);

    [DllImport("camera_shim.dll", CallingConvention = CallingConvention.Cdecl)]
    static extern IntPtr RC_CamVtInfo();

    // engine-faithful set: calls the camera object's own vtable position and
    // look-at setters (the calls the managed camera API makes)
    public static int CamSetVt(float x, float y, float z, float tx, float ty, float tz)
    {
        try { return RC_CamSetVt(x, y, z, tx, ty, tz); }
        catch { return -4; }
    }

    [DllImport("camera_shim.dll", CallingConvention = CallingConvention.Cdecl)]
    static extern int RC_CamUseScene(IntPtr scene);

    [DllImport("camera_shim.dll", CallingConvention = CallingConvention.Cdecl)]
    static extern int RC_CamUseClr(IntPtr clrObj);

    [DllImport("camera_shim.dll", CallingConvention = CallingConvention.Cdecl)]
    static extern int RC_CamUseObj(IntPtr cam);

    [DllImport("camera_shim.dll", CallingConvention = CallingConvention.Cdecl)]
    static extern IntPtr RC_CamVt2Info();

    [DllImport("camera_shim.dll", CallingConvention = CallingConvention.Cdecl)]
    static extern IntPtr RC_DumpClr(IntPtr clrObj);

    [DllImport("camera_shim.dll", CallingConvention = CallingConvention.Cdecl)]
    static extern IntPtr RC_DumpObj(IntPtr obj);

    [DllImport("camera_shim.dll", CallingConvention = CallingConvention.Cdecl)]
    static extern int RC_CamSetVt2(float x, float y, float z, float tx, float ty, float tz);

    [DllImport("camera_shim.dll", CallingConvention = CallingConvention.Cdecl)]
    static extern IntPtr RC_CamSceneInfo();

    public static int CamUseScene(IntPtr scene)
    {
        try { return RC_CamUseScene(scene); } catch { return -4; }
    }

    public static int CamUseClr(IntPtr clrObj)
    {
        try { return RC_CamUseClr(clrObj); } catch { return -4; }
    }

    public static int CamUseObj(IntPtr cam)
    {
        try { return RC_CamUseObj(cam); } catch { return -4; }
    }

    public static string CamVt2Info()
    {
        try { return Str(RC_CamVt2Info()); } catch { return "?"; }
    }

    public static string DumpClr(IntPtr clrObj)
    {
        try { return Str(RC_DumpClr(clrObj)); } catch { return "?"; }
    }

    public static string DumpObj(IntPtr obj)
    {
        try { return Str(RC_DumpObj(obj)); } catch { return "?"; }
    }

    [DllImport("camera_shim.dll", CallingConvention = CallingConvention.Cdecl)]
    static extern int RC_CamPosOnly(float x, float y, float z);

    [DllImport("camera_shim.dll", CallingConvention = CallingConvention.Cdecl)]
    static extern int RC_CamTgtSlot(float tx, float ty, float tz, uint off);

    public static int CamPosOnly(float x, float y, float z)
    {
        try { return RC_CamPosOnly(x, y, z); } catch { return -4; }
    }

    public static int CamTgtSlot(float tx, float ty, float tz, uint off)
    {
        try { return RC_CamTgtSlot(tx, ty, tz, off); } catch { return -4; }
    }

    [DllImport("camera_shim.dll", CallingConvention = CallingConvention.Cdecl)]
    static extern IntPtr RC_SlotBytes(uint off, uint len);

    public static string SlotBytes(uint off, uint len)
    {
        try { return Str(RC_SlotBytes(off, len)); } catch { return "?"; }
    }

    public static int CamSetVt2(float x, float y, float z, float tx, float ty, float tz)
    {
        try { return RC_CamSetVt2(x, y, z, tx, ty, tz); } catch { return -4; }
    }

    public static string CamSceneInfo()
    {
        try { return Str(RC_CamSceneInfo()); } catch { return "?"; }
    }

    public static string CamVtInfo()
    {
        try { return Str(RC_CamVtInfo()); }
        catch { return "?"; }
    }

    public static string CamInfo()
    {
        try { return Str(RC_CamInfo()); }
        catch { return "?"; }
    }

    [DllImport("camera_shim.dll", CallingConvention = CallingConvention.Cdecl)]
    static extern IntPtr RC_CamObject();

    public static IntPtr CamObject()
    {
        try { return RC_CamObject(); } catch { return IntPtr.Zero; }
    }

    public static string FindAll(float x, float z)
    {
        try { return Str(RC_Probe_FindAll(x, z)); }
        catch (Exception e) { return "findall ex: " + e.Message; }
    }

    public static int ObjectOff(int i)
    {
        try { return RC_Probe_ObjectOff(i); } catch { return -1; }
    }

    public static int CamSetIndex(int i, float x, float y, float z,
                                  float tx, float ty, float tz)
    {
        try { return RC_CamSetIndex(i, x, y, z, tx, ty, tz); } catch { return -4; }
    }

    public static IntPtr InputPtr()
    {
        try { return RC_Probe_InputPtr(); } catch { return IntPtr.Zero; }
    }

    public static int Snap(IntPtr obj, uint len)
    {
        try { return RC_Probe_Snap(obj, len); } catch { return -1; }
    }

    public static string SnapDiff()
    {
        try { return Str(RC_Probe_SnapDiff()); } catch { return "snapdiff ex"; }
    }

    public static bool Available;
    public static string Status = "not loaded";

    public static void TryLoad(Action<string> log)
    {
        try
        {
            int rc = RC_Shim_Init();
            IntPtr sp = RC_Shim_Status();
            Status = sp != IntPtr.Zero ? Marshal.PtrToStringAnsi(sp) : "no status";
            if (rc != 0)
            {
                log("CameraShim: unavailable rc=" + rc + " status=" + Status);
                return;
            }
            int self = RC_Shim_AbiSelfTest();
            Available = self == 0x53484D01;
            log(string.Format("CameraShim: loaded rc=0 selftest=0x{0:X} status={1}",
                self, Status));
        }
        catch (DllNotFoundException)
        {
            log("CameraShim: camera_shim.dll not found (managed fallback)");
        }
        catch (Exception e)
        {
            log("CameraShim ex: " + e.Message);
        }
    }
}
