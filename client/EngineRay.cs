// Native scene ray through the host engine - the same backend the game's
// camera obstruction uses (KG3D_Scene::RayIntersectionTerrain, game mask
// 0x301 includes terrain). The client loads KG3DEngineDX11EX64.dll; the
// engine/window/scene are reached through its exports:
//   KG3D_GetEngine() -> ?GetActiveWindow2@KG3D_Engine@@... -> ?Get3DScene2@KG3D_Window@@...
// RayIntersectionTerrain contract (proof/netcode/disasm/host_ray_terrain_fn.txt):
//   rcx=scene, rdx=float3 pos, r8=float3 dir, xmm3=maxDist,
//   r9 unused, stack: float* retDist, int* retIntersect
using System;
using System.Runtime.InteropServices;

internal sealed class EngineRay
{
    const int RVA_RAY_TERRAIN = 0x976260;   // 0x180976260 - image base
    const int RVA_RAY_SCENE = 0x975EE0;     // 0x180975EE0 - image base
    const int RVA_SPACE_RAY = 0xA5E4C0;     // 0x180A5E4C0 - the space manager
                                            // ray the scene's RayIntersection
                                            // calls (0x1809760EC)

    [DllImport("kernel32.dll", CharSet = CharSet.Ansi)]
    static extern IntPtr GetModuleHandleA(string name);

    [DllImport("kernel32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
    static extern IntPtr GetProcAddress(IntPtr module, string name);

    // KG3D_GetEngine2 simply returns g_pEngine (no arguments)
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    delegate IntPtr GetEngine2Fn();

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    delegate IntPtr EngineMethodFn(IntPtr self);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    delegate int RayTerrainFn(IntPtr scene, float[] pos, float[] dir, float maxDist,
                              IntPtr unused, out float retDist, out int retIntersect);

    // terrain object / space-manager ray: fn(this, pos, dir, maxDist, float* out)
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    delegate int TerrainVtFn(IntPtr terrain, float[] pos, float[] dir, float maxDist, out float outDist);

    // Nearest terrain hit of the segment A->B; -1 when nothing is hit.
    public float RayTerrain(float ax, float ay, float az, float bx, float by, float bz)
    {
        if (!EnsureReady() || _terrainVt == null) return -1f;
        return Cast(_terrainVt, ax, ay, az, bx, by, bz);
    }

    float Cast(TerrainVtFn fn, float ax, float ay, float az, float bx, float by, float bz)
    {
        if (fn == null || _terrainObj == IntPtr.Zero) return -1f;
        float dx = bx - ax, dy = by - ay, dz = bz - az;
        float len = (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
        if (len < 1e-3f) return -1f;
        var pos = new float[] { ax, ay, az };
        var dir = new float[] { dx / len, dy / len, dz / len };
        float dist;
        try
        {
            LastHr = fn(_terrainObj, pos, dir, len, out dist);
            LastDist = dist;
            LastHit = LastHr != 0 ? 1 : 0;
            if (LastHr == 0) return -1f;
            if (dist < 0f || dist > len + 1f) return -1f;
            return dist;
        }
        catch
        {
            return -1f;
        }
    }

    // General scene ray (entities/scene nodes). The scene-level
    // RayIntersection is guarded, so call the space manager it forwards to
    // (0x180A5E4C0) directly - the same call the engine makes.
    public float RayScene(float ax, float ay, float az, float bx, float by, float bz)
    {
        if (!EnsureReady()) return -1f;
        if (_spaceRay == null || _spaceObj == IntPtr.Zero)
            return Cast(_sceneRay, ax, ay, az, bx, by, bz);
        float dx = bx - ax, dy = by - ay, dz = bz - az;
        float len = (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
        if (len < 1e-3f) return -1f;
        var pos = new float[] { ax, ay, az };
        var dir = new float[] { dx / len, dy / len, dz / len };
        float dist;
        try
        {
            int hr = _spaceRay(_spaceObj, pos, dir, len, out dist);
            LastHr = hr;
            LastDist = dist;
            LastHit = hr != 0 ? 1 : 0;
            if (hr == 0) return -1f;
            if (dist < 0f || dist > len + 1f) return -1f;
            return dist;
        }
        catch
        {
            return -1f;
        }
    }
}
