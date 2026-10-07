// Character 3.4 face pipeline (agent/3x-face): loads the MetaFace JSON produced
// offline by tools/character/face_data.py (FaceLiftDataConverterX64 KMETAFACE)
// and applies it to the player model. The apply call goes through the shared
// camera_shim export RC_ModelLoadMetaFaceJson (agent A owns that export); until
// it exists the client logs a pending status and keeps the default face.
using System;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;

internal static class FaceData
{
    [DllImport("camera_shim.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    static extern int RC_ModelLoadMetaFaceJson(IntPtr model, string jsonUtf8);

    // KGModelCLR.m_pModel is a raw pointer field (IKG3DModelProxy*), and the CLR
    // object cannot be pinned (it holds reference fields), so read the field with
    // a tiny DynamicMethod (ldfld + ret) instead of Marshal.ReadIntPtr.
    static IntPtr ReadPointerField(object obj, string fieldName)
    {
        Type t = obj.GetType();
        FieldInfo f = t.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
        if (f == null) throw new MissingFieldException(t.FullName, fieldName);
        DynamicMethod dm = new DynamicMethod(
            "rc_read_" + fieldName, typeof(IntPtr), new Type[] { typeof(object) }, t, true);
        ILGenerator il = dm.GetILGenerator();
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Castclass, t);
        il.Emit(OpCodes.Ldfld, f);
        il.Emit(OpCodes.Ret);
        Func<object, IntPtr> read = (Func<object, IntPtr>)dm.CreateDelegate(typeof(Func<object, IntPtr>));
        return read(obj);
    }

    internal static void Apply(string jsonPath, long handle, object model, Action<string> log)
    {
        try
        {
            if (log == null) log = delegate(string m) { };
            if (!File.Exists(jsonPath))
            {
                log("face: json not found: " + jsonPath);
                return;
            }
            string json = File.ReadAllText(jsonPath);
            if (json.IndexOf("\"Bone\"", StringComparison.Ordinal) < 0 ||
                json.IndexOf("\"BodyType\"", StringComparison.Ordinal) < 0)
            {
                log("face: json lacks Bone/BodyType markers, not a MetaFace JSON");
                return;
            }
            // Prefer the AddDummyModel handle: it is the RTTI-verified
            // KG3DModelProxy (KG_EngineEditorX64.dll) that the shim's other
            // proxy exports (anchor/find) already use. The CLR's m_pModel is a
            // different proxy object and returned E_FAIL for the face apply
            // (2026-10-06). Fall back to m_pModel when the handle is 0.
            IntPtr mp = handle > 0 ? new IntPtr(handle) : IntPtr.Zero;
            string src = "handle";
            if (mp == IntPtr.Zero)
            {
                try { mp = ReadPointerField(model, "m_pModel"); src = "m_pModel"; }
                catch (Exception pe)
                {
                    log("face: m_pModel read ex: " + pe.Message);
                    return;
                }
            }
            if (mp == IntPtr.Zero)
            {
                log("face: no proxy pointer (handle=0, m_pModel null)");
                return;
            }
            log(string.Format("face: metaface json={0} bytes={1} proxy=0x{2:X} ({3})",
                Path.GetFileName(jsonPath), json.Length, mp.ToInt64(), src));
            int rc = RC_ModelLoadMetaFaceJson(mp, json);
            log("face: apply rc=" + rc);
        }
        catch (EntryPointNotFoundException)
        {
            log("face: apply pending agent A shim export RC_ModelLoadMetaFaceJson");
        }
        catch (DllNotFoundException e) { log("face: shim dll missing: " + e.Message); }
        catch (Exception e) { log("face ex: " + e.Message); }
    }
}
