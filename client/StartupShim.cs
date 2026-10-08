// startup_shim.dll P/Invoke - host override of the editor-only shader-DB TCP
// stall in KG3D_MaterialSystemX64.dll (RC_STARTUP=nodb). Registered deviation
// D7; see docs/engine_host/PREDRAW_STARTUP.md. The DLL is only touched when
// RC_STARTUP is set, so shipped behaviour (no env var) is byte-identical and
// the DLL is never even loaded.
using System;
using System.Runtime.InteropServices;

static class StartupShim
{
    [DllImport("startup_shim.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern int RC_Startup_EarlyInit();

    [DllImport("startup_shim.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr RC_Startup_Status();

    public static string Init()
    {
        try
        {
            int rc = RC_Startup_EarlyInit();
            return "rc=" + rc + " " + Status();
        }
        catch (Exception e)
        {
            return "load failed: " + e.Message;
        }
    }

    public static string Status()
    {
        try
        {
            IntPtr p = RC_Startup_Status();
            return p == IntPtr.Zero ? "(null)" : Marshal.PtrToStringAnsi(p);
        }
        catch (Exception e)
        {
            return "status failed: " + e.Message;
        }
    }
}
