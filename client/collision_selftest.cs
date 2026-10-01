// Offline deterministic self-test for client/FoliageCollision.cs.
// No engine, no game assets: writes synthetic FCOL v2 bins into %TEMP% and
// exercises the exact code path the client uses (Resolve, MoveResolved,
// SupportHeight, Raycast). Run: bin64\collision_selftest.exe; exit 0 = pass.
using System;
using System.Collections.Generic;
using System.IO;

internal static class CollisionSelfTest
{
    static int _pass, _fail;
    static readonly List<string> _failed = new List<string>();

    static void Check(string name, bool ok, string detail)
    {
        if (ok) { _pass++; Console.WriteLine("PASS  " + name + "  " + detail); }
        else { _fail++; _failed.Add(name); Console.WriteLine("FAIL  " + name + "  " + detail); }
    }

    sealed class MeshBuilder
    {
        public readonly List<float> V = new List<float>();
        public readonly List<int> T = new List<int>();

        public int AddVertex(float x, float y, float z)
        {
            V.Add(x); V.Add(y); V.Add(z);
            return V.Count / 3 - 1;
        }

        public void AddTri(int a, int b, int c) { T.Add(a); T.Add(b); T.Add(c); }

        public void AddQuad(float ax, float ay, float az, float bx, float by, float bz,
                            float cx, float cy, float cz, float dx, float dy, float dz)
        {
            int a = AddVertex(ax, ay, az), b = AddVertex(bx, by, bz);
            int c = AddVertex(cx, cy, cz), d = AddVertex(dx, dy, dz);
            AddTri(a, b, c); AddTri(a, c, d);
        }

        public void AddBox(float x0, float y0, float z0, float x1, float y1, float z1)
        {
            AddQuad(x0, y0, z0, x0, y0, z1, x0, y1, z1, x0, y1, z0);
            AddQuad(x1, y0, z1, x1, y0, z0, x1, y1, z0, x1, y1, z1);
            AddQuad(x0, y0, z1, x0, y0, z0, x1, y0, z0, x1, y0, z1);
            AddQuad(x0, y1, z0, x0, y1, z1, x1, y1, z1, x1, y1, z0);
            AddQuad(x0, y0, z0, x1, y0, z0, x1, y1, z0, x0, y1, z0);
            AddQuad(x1, y0, z1, x0, y0, z1, x0, y1, z1, x1, y1, z1);
        }
    }

    static float[] M(float x, float y, float z)
    {
        return new float[] { 1f, 0f, 0f, 0f, 0f, 1f, 0f, 0f, 0f, 0f, 1f, 0f, x, y, z, 1f };
    }

    static string WriteBin(string name, MeshBuilder[] meshes, float[][] matrices)
    {
        string dir = Path.Combine(Path.GetTempPath(), "reborn_colselftest");
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, name + ".bin");
        using (BinaryWriter w = new BinaryWriter(File.Create(path)))
        {
            w.Write(0x4C4F4346u);
            w.Write(2);
            w.Write(meshes.Length);
            w.Write(matrices.Length);
            for (int mi = 0; mi < meshes.Length; mi++)
            {
                MeshBuilder m = meshes[mi];
                w.Write(m.V.Count / 3);
                w.Write(m.T.Count / 3);
                for (int i = 0; i < m.V.Count; i++) w.Write(m.V[i]);
                for (int i = 0; i < m.T.Count; i++) w.Write(m.T[i]);
            }
            for (int mi = 0; mi < matrices.Length; mi++)
            {
                w.Write(mi);                 // instance -> mesh index
                for (int k = 0; k < 16; k++) w.Write(matrices[mi][k]);
                w.Write(0);
                for (int k = 0; k < 6; k++) w.Write(0f); // AABB derived from mesh
            }
        }
        return path;
    }

    static void WriteMeshSidecar(string binPath, string[] meshPaths)
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        for (int i = 0; i < meshPaths.Length; i++)
            sb.Append(i).Append('\t').Append(meshPaths[i]).Append('\n');
        File.WriteAllText(binPath + ".meshes.txt", sb.ToString());
    }

    static void WriteOflags(string binPath, byte[] flags)
    {
        byte[] raw = new byte[8 + flags.Length];
        BitConverter.GetBytes(0x474C464Fu).CopyTo(raw, 0);
        BitConverter.GetBytes((uint)flags.Length).CopyTo(raw, 4);
        flags.CopyTo(raw, 8);
        File.WriteAllBytes(binPath + ".oflags", raw);
    }

    static MeshBuilder Wall()
    {
        MeshBuilder m = new MeshBuilder();
        m.AddQuad(0f, 0f, -200f, 0f, 0f, 200f, 0f, 200f, 200f, 0f, 200f, -200f);
        return m;
    }

    static void Main()
    {
        string wallPath = WriteBin("wall", new MeshBuilder[] { Wall() }, new float[][] { M(0f, 0f, 0f) });

        // 1. flat wall push-out
        {
            FoliageCollision col = new FoliageCollision(null, wallPath);
            float px = 5f, py = 0f, pz = 0f, ground = 0f;
            bool grounded = false;
            bool blocked = col.Resolve(ref px, ref py, ref pz, 17f, 116f, ref ground, ref grounded);
            Check("wall_pushout", blocked && Math.Abs(px - 17f) < 0.05f,
                string.Format("blocked={0} px={1:F2}", blocked, px));
            Check("tall_wall_no_step", Math.Abs(ground) < 0.01f,
                string.Format("ground={0:F2}", ground));
        }

        // 2. far from the wall: no contact
        {
            FoliageCollision col = new FoliageCollision(null, wallPath);
            float px = -100f, py = 0f, pz = 0f, ground = 0f;
            bool grounded = false;
            bool blocked = col.Resolve(ref px, ref py, ref pz, 17f, 116f, ref ground, ref grounded);
            Check("far_no_contact", !blocked && Math.Abs(px + 100f) < 0.01f,
                string.Format("blocked={0} px={1:F2}", blocked, px));
        }

        // 3. thin rail at a former 6-sample gap (y 52..58, x 15..17):
        //    only the exact segment/triangle pair finds it.
        {
            MeshBuilder m = new MeshBuilder();
            m.AddBox(15f, 52f, -200f, 17f, 58f, 200f);
            string p = WriteBin("rail", new MeshBuilder[] { m }, new float[][] { M(0f, 0f, 0f) });
            FoliageCollision col = new FoliageCollision(null, p);
            float px = 0f, py = 0f, pz = 0f, ground = 0f;
            bool grounded = false;
            bool blocked = col.Resolve(ref px, ref py, ref pz, 17f, 116f, ref ground, ref grounded, 50f);
            Check("rail_between_samples", blocked && px <= -1.9f,
                string.Format("blocked={0} px={1:F2}", blocked, px));
        }

        // 4. support probe over a low box (top 35, climbable)
        {
            MeshBuilder m = new MeshBuilder();
            m.AddBox(30f, -10f, -200f, 300f, 35f, 200f);
            string p = WriteBin("box", new MeshBuilder[] { m }, new float[][] { M(0f, 0f, 0f) });
            FoliageCollision col = new FoliageCollision(null, p);
            float top = col.SupportHeight(40f, 0f, -50f, 60f);
            float none = col.SupportHeight(0f, 0f, -50f, 60f);
            Check("support_box_top", Math.Abs(top - 35f) < 0.05f, string.Format("top={0:F2}", top));
            Check("support_outside", none == float.MinValue, string.Format("none={0}", none));
        }

        // 5. raycast through the rail (hit at x=15 => 115 from x=-100, miss away)
        {
            MeshBuilder m = new MeshBuilder();
            m.AddBox(15f, 52f, -200f, 17f, 58f, 200f);
            string p = WriteBin("ray", new MeshBuilder[] { m }, new float[][] { M(0f, 0f, 0f) });
            FoliageCollision col = new FoliageCollision(null, p);
            float hit = col.Raycast(-100f, 55f, 0f, 100f, 55f, 0f);
            float miss = col.Raycast(-100f, 100f, 0f, -100f, 100f, 200f);
            Check("raycast_hit", Math.Abs(hit - 115f) < 0.1f, string.Format("hit={0:F2}", hit));
            Check("raycast_miss", miss < 0f, string.Format("miss={0:F2}", miss));
        }

        // 6. substepped move must not tunnel a thin wall
        {
            FoliageCollision col = new FoliageCollision(null, wallPath);
            float px = -50f, py = 0f, pz = 0f, ground = 0f;
            bool grounded = false;
            bool blocked = col.MoveResolved(ref px, ref py, ref pz, 100f, 0f,
                17f, 116f, 10f, ref ground, ref grounded);
            Check("substep_no_tunnel", blocked && px <= -16.9f,
                string.Format("blocked={0} px={1:F2}", blocked, px));
        }

        // 7. step budget (recovered engine stepOffset, passed by the client):
        //    a 35 u step is climbed with a 50 u budget and blocks below it
        {
            MeshBuilder m = new MeshBuilder();
            m.AddBox(30f, -10f, -200f, 300f, 35f, 200f);
            string p = WriteBin("step", new MeshBuilder[] { m }, new float[][] { M(0f, 0f, 0f) });
            FoliageCollision col = new FoliageCollision(null, p);
            float px = 31f, py = 0f, pz = 0f, ground = 0f;
            bool grounded = true;
            bool blocked = col.Resolve(ref px, ref py, ref pz, 17f, 116f, ref ground, ref grounded, 50f);
            Check("step_up_50u_budget", !blocked && grounded && Math.Abs(ground - 35f) < 0.05f,
                string.Format("blocked={0} ground={1:F2}", blocked, ground));
            px = 29.9f; py = 0f; pz = 0f; ground = 0f; grounded = false;
            blocked = col.Resolve(ref px, ref py, ref pz, 17f, 116f, ref ground, ref grounded, 30f);
            Check("step_blocks_over_budget", blocked && ground <= 0.05f,
                string.Format("blocked={0} ground={1:F2}", blocked, ground));
            px = 29.9f; py = 0f; pz = 0f; ground = 0f; grounded = true;
            blocked = col.Resolve(ref px, ref py, ref pz, 17f, 116f, ref ground, ref grounded, 50f);
            Check("step_onto_low_edge", !blocked && grounded && Math.Abs(ground - 35f) < 0.05f,
                string.Format("blocked={0} ground={1:F2}", blocked, ground));
        }

        // 7b. 64 u ground tolerance: a 51 u floor edge (the field case) climbs
        {
            MeshBuilder m = new MeshBuilder();
            m.AddBox(30f, -10f, -200f, 300f, 51f, 200f);
            string p = WriteBin("step51", new MeshBuilder[] { m }, new float[][] { M(0f, 0f, 0f) });
            FoliageCollision col = new FoliageCollision(null, p);
            float px = 29.9f, py = 0f, pz = 0f, ground = 0f;
            bool grounded = true;
            bool blocked = col.Resolve(ref px, ref py, ref pz, 17f, 116f, ref ground, ref grounded, 64f);
            Check("step_51u_with_64_budget", !blocked && Math.Abs(ground - 51f) < 0.05f,
                string.Format("blocked={0} ground={1:F2}", blocked, ground));
        }

        // 8. CCT top rule on face-only geometry: a thin plate with no up-facing
        //    top below the step budget must not block; a tall one must block.
        {
            MeshBuilder low = new MeshBuilder();
            low.AddQuad(30f, 0f, -200f, 30f, 0f, 200f, 30f, 20f, 200f, 30f, 20f, -200f);
            string pl = WriteBin("plate_low", new MeshBuilder[] { low }, new float[][] { M(0f, 0f, 0f) });
            FoliageCollision col = new FoliageCollision(null, pl);
            float px = 25f, py = 0f, pz = 0f, ground = 0f;
            bool grounded = true;
            bool blocked = col.Resolve(ref px, ref py, ref pz, 17f, 116f, ref ground, ref grounded, 50f);
            Check("thin_low_plate_passes", !blocked && Math.Abs(ground - 20f) < 0.05f,
                string.Format("blocked={0} ground={1:F2}", blocked, ground));

            MeshBuilder tall = new MeshBuilder();
            tall.AddQuad(30f, 0f, -200f, 30f, 0f, 200f, 30f, 200f, 200f, 30f, 200f, -200f);
            string pt = WriteBin("plate_tall", new MeshBuilder[] { tall }, new float[][] { M(0f, 0f, 0f) });
            col = new FoliageCollision(null, pt);
            px = 25f; py = 0f; pz = 0f; ground = 0f; grounded = false;
            blocked = col.Resolve(ref px, ref py, ref pz, 17f, 116f, ref ground, ref grounded, 50f);
            Check("thin_tall_plate_blocks", blocked, string.Format("blocked={0}", blocked));

            // thin wall crossed in a long substep: the capsule centre passes
            // the plane inside one substep and the degenerate normal points
            // along the motion - the push must oppose the motion (field case:
            // the Yumen building 001_002 wall z~33870 walked through).
            MeshBuilder twall = new MeshBuilder();
            twall.AddQuad(30f, 0f, -200f, 30f, 0f, 200f, 30f, 200f, 200f, 30f, 200f, -200f);
            string ptw = WriteBin("thin_wall", new MeshBuilder[] { twall }, new float[][] { M(0f, 0f, 0f) });
            col = new FoliageCollision(null, ptw);
            px = 5f; py = 0f; pz = 0f; ground = 0f; grounded = true;
            blocked = col.MoveResolved(ref px, ref py, ref pz, 60f, 0f,
                17f, 116f, 20f, ref ground, ref grounded, 64f);
            Check("thin_wall_no_popthrough", blocked && px <= 13.5f,
                string.Format("blocked={0} px={1:F2}", blocked, px));

            // tall wall behind + low plank in front: the low touching face wins
            // the step test when the raised capsule clears the blocker (open
            // space above the plank). If the wall continues at the plank's
            // plane the raise cannot clear - CCT up-sweep; field case: the
            // Yumen building back wall ledge ladder y 975/990/1030/1040
            // (2026-09-30) ratcheted the player up the wall.
            MeshBuilder mixed = new MeshBuilder();
            mixed.AddQuad(50f, 0f, -200f, 50f, 0f, 200f, 50f, 200f, 200f, 50f, 200f, -200f);
            mixed.AddQuad(27f, 0f, -200f, 27f, 0f, 200f, 27f, 20f, 200f, 27f, 20f, -200f);
            string pm = WriteBin("plate_mixed", new MeshBuilder[] { mixed }, new float[][] { M(0f, 0f, 0f) });
            col = new FoliageCollision(null, pm);
            px = 25f; py = 0f; pz = 0f; ground = 0f; grounded = true;
            blocked = col.Resolve(ref px, ref py, ref pz, 17f, 116f, ref ground, ref grounded, 50f);
            Check("low_plank_open_top_passes", !blocked && Math.Abs(ground - 20f) < 0.05f,
                string.Format("blocked={0} ground={1:F2}", blocked, ground));

            // step face with the wall continuing above it: not a step, it is a
            // wall ledge. The raise must not clear the blocker, so the move
            // stays blocked and the capsule is pushed out (no embedding, no
            // ratchet up the wall).
            MeshBuilder ladder = new MeshBuilder();
            ladder.AddBox(30f, 0f, -200f, 40f, 30f, 200f);
            ladder.AddBox(40f, 0f, -200f, 60f, 300f, 200f);
            string pl2 = WriteBin("wall_ledge", new MeshBuilder[] { ladder }, new float[][] { M(0f, 0f, 0f) });
            col = new FoliageCollision(null, pl2);
            px = 25f; py = 0f; pz = 0f; ground = 0f; grounded = true;
            blocked = col.Resolve(ref px, ref py, ref pz, 17f, 116f, ref ground, ref grounded, 64f);
            Check("wall_ledge_step_rejected", blocked && ground <= 0.05f && py <= 0.05f,
                string.Format("blocked={0} ground={1:F2} py={2:F2}", blocked, ground, py));
            Check("capsule_fit_probe",
                col.CapsuleBlocked(35f, 0f, 0f, 17f, 116f) && !col.CapsuleBlocked(120f, 0f, 0f, 17f, 116f),
                string.Format("inside={0} away={1}",
                    col.CapsuleBlocked(35f, 0f, 0f, 17f, 116f), col.CapsuleBlocked(120f, 0f, 0f, 17f, 116f)));

            // face whose top is below the feet: must never push out (the
            // user-log case: top=929.9 < feet=933.9 was blocking)
            MeshBuilder below = new MeshBuilder();
            below.AddQuad(30f, -100f, -200f, 30f, -100f, 200f, 30f, -10f, 200f, 30f, -10f, -200f);
            string pb = WriteBin("plate_below", new MeshBuilder[] { below }, new float[][] { M(0f, 0f, 0f) });
            col = new FoliageCollision(null, pb);
            px = 25f; py = 0f; pz = 0f; ground = 0f; grounded = true;
            blocked = col.Resolve(ref px, ref py, ref pz, 17f, 116f, ref ground, ref grounded, 50f);
            Check("face_below_feet_passes", !blocked,
                string.Format("blocked={0} px={1:F2}", blocked, px));
        }

        // 9. inverted floor plate (flipped winding, field case: the 龙门寻宝
        //    house rug wj_erg地毯001_hd, inst 485, ny=-1): the floor query must
        //    ignore winding, otherwise the client steps onto terrain below and
        //    the 64 u drop-tolerance snaps the player back down every frame.
        {
            MeshBuilder m = new MeshBuilder();
            int a = m.AddVertex(-200f, 3f, -200f), b = m.AddVertex(-200f, 3f, 200f);
            int c = m.AddVertex(200f, 3f, 200f), d = m.AddVertex(200f, 3f, -200f);
            m.AddTri(a, c, b); m.AddTri(a, d, c);   // reversed -> normal down
            string p = WriteBin("floor_inverted", new MeshBuilder[] { m }, new float[][] { M(0f, 0f, 0f) });
            FoliageCollision col = new FoliageCollision(null, p);
            float top = col.SupportHeight(40f, 40f, -20f, 60f);
            Check("inverted_floor_support", Math.Abs(top - 3f) < 0.05f,
                string.Format("top={0:F2}", top));
            float px = 0f, py = 3f, pz = 0f, ground = 3f;
            bool grounded = true;
            bool blocked = col.Resolve(ref px, ref py, ref pz, 17f, 116f, ref ground, ref grounded, 64f);
            Check("inverted_floor_stand", !blocked && Math.Abs(py - 3f) < 0.05f,
                string.Format("blocked={0} py={1:F2}", blocked, py));
        }

        // 11. solid props (host proxy for the client's per-unit passability):
        //     a capsule inside a 小物件 prop is ejected through the nearest free
        //     face; a building-class mesh keeps shell collision; an exit blocked
        //     by a nearby wall is skipped for a free one.
        {
            MeshBuilder prop = new MeshBuilder();
            prop.AddBox(30f, -10f, -200f, 130f, 200f, 200f);
            string pb = WriteBin("propbox", new MeshBuilder[] { prop }, new float[][] { M(0f, 0f, 0f) });
            WriteMeshSidecar(pb, new string[] { "data/source/maps_source/小物件/木箱/test_prop.mesh" });
            FoliageCollision col = new FoliageCollision(null, pb);
            float px = 80f, py = 0f, pz = 0f;
            int ei;
            bool ej = col.SolidPropPush(ref px, ref py, ref pz, 17f, 116f, 0f, out ei);
            Check("prop_solid_eject", ej && (px < 14f || px > 146f),
                string.Format("eject={0} px={1:F1}", ej, px));

            string pb2 = WriteBin("propbox_building", new MeshBuilder[] { prop }, new float[][] { M(0f, 0f, 0f) });
            WriteMeshSidecar(pb2, new string[] { "data/source/maps_source/建筑/test_house.mesh" });
            col = new FoliageCollision(null, pb2);
            px = 80f; py = 0f; pz = 0f;
            ej = col.SolidPropPush(ref px, ref py, ref pz, 17f, 116f, 0f, out ei);
            Check("prop_solid_building_kept", !ej && Math.Abs(px - 80f) < 0.01f,
                string.Format("eject={0} px={1:F1}", ej, px));

            MeshBuilder propW = new MeshBuilder();
            propW.AddBox(30f, -10f, -200f, 130f, 200f, 200f);
            MeshBuilder wallB = new MeshBuilder();
            wallB.AddQuad(20f, 0f, -200f, 20f, 0f, 200f, 20f, 200f, 200f, 20f, 200f, -200f);
            string pw = WriteBin("propbox_wall", new MeshBuilder[] { propW, wallB },
                                 new float[][] { M(0f, 0f, 0f), M(0f, 0f, 0f) });
            WriteMeshSidecar(pw, new string[] {
                "data/source/maps_source/小物件/木箱/test_prop2.mesh",
                "data/source/maps_source/建筑/test_wall.mesh" });
            col = new FoliageCollision(null, pw);
            px = 80f; py = 0f; pz = 0f;
            ej = col.SolidPropPush(ref px, ref py, ref pz, 17f, 116f, 0f, out ei);
            Check("prop_solid_free_exit", ej && px > 146f,
                string.Format("eject={0} px={1:F1}", ej, px));
        }

        // 12. shipped obstacle flags (plan P0): a mesh whose .mesh.ini says
        //     bAutoProduceObstacle=0 gets no physics; flag=1 keeps collision.
        {
            string pb = WriteBin("wall_oflags_off", new MeshBuilder[] { Wall() }, new float[][] { M(0f, 0f, 0f) });
            WriteOflags(pb, new byte[] { 0x00 });
            FoliageCollision col = new FoliageCollision(null, pb);
            float px = 5f, py = 0f, pz = 0f, ground = 0f;
            bool grounded = false;
            bool blocked = col.Resolve(ref px, ref py, ref pz, 17f, 116f, ref ground, ref grounded);
            Check("obstacle_flag_off_walkthrough",
                !blocked && Math.Abs(px - 5f) < 0.01f && col.NoObstacleSkipped == 1,
                string.Format("blocked={0} px={1:F1} skipped={2}", blocked, px, col.NoObstacleSkipped));

            string pb2 = WriteBin("wall_oflags_on", new MeshBuilder[] { Wall() }, new float[][] { M(0f, 0f, 0f) });
            WriteOflags(pb2, new byte[] { 0x01 });
            col = new FoliageCollision(null, pb2);
            px = 5f; py = 0f; pz = 0f; ground = 0f; grounded = false;
            blocked = col.Resolve(ref px, ref py, ref pz, 17f, 116f, ref ground, ref grounded);
            Check("obstacle_flag_on_blocks", blocked && Math.Abs(px - 17f) < 0.05f,
                string.Format("blocked={0} px={1:F1}", blocked, px));

            // auto=0 but an authored CollisionMesh sibling exists (bit5): the
            // engine still creates physics from the sibling -> keep collision
            string pb3 = WriteBin("wall_oflags_off_sib", new MeshBuilder[] { Wall() }, new float[][] { M(0f, 0f, 0f) });
            WriteOflags(pb3, new byte[] { 0x20 });
            col = new FoliageCollision(null, pb3);
            px = 5f; py = 0f; pz = 0f; ground = 0f; grounded = false;
            blocked = col.Resolve(ref px, ref py, ref pz, 17f, 116f, ref ground, ref grounded);
            Check("obstacle_flag_off_with_sibling_blocks", blocked && col.NoObstacleSkipped == 0,
                string.Format("blocked={0} skipped={1}", blocked, col.NoObstacleSkipped));

            // solid-prop AABB collision: a tall prop blocks like a wall, a low
            // one is stepped onto within the budget (registered proxy)
            MeshBuilder spb = new MeshBuilder();
            spb.AddBox(30f, -10f, -200f, 130f, 200f, 200f);
            string spbPath = WriteBin("solid_prop_tall", new MeshBuilder[] { spb }, new float[][] { M(0f, 0f, 0f) });
            WriteMeshSidecar(spbPath, new string[] { "data/source/maps_source/小物件/木箱/test_solid.mesh" });
            FoliageCollision spc = new FoliageCollision(null, spbPath);
            float spx = 25f, spy = 0f, spz = 0f, spg = 0f;
            bool spGrounded = true;
            bool spBlocked = spc.Resolve(ref spx, ref spy, ref spz, 17f, 116f, ref spg, ref spGrounded, 64f);
            Check("solid_prop_blocks_tall", spBlocked && spx <= 13.1f,
                string.Format("blocked={0} px={1:F1}", spBlocked, spx));

            MeshBuilder spl = new MeshBuilder();
            spl.AddBox(30f, -10f, -200f, 130f, 35f, 200f);
            string splPath = WriteBin("solid_prop_low", new MeshBuilder[] { spl }, new float[][] { M(0f, 0f, 0f) });
            WriteMeshSidecar(splPath, new string[] { "data/source/maps_source/小物件/木箱/test_solid2.mesh" });
            spc = new FoliageCollision(null, splPath);
            spx = 29.9f; spy = 0f; spz = 0f; spg = 0f; spGrounded = true;
            spBlocked = spc.Resolve(ref spx, ref spy, ref spz, 17f, 116f, ref spg, ref spGrounded, 64f);
            Check("solid_prop_step_low", !spBlocked && Math.Abs(spg - 35f) < 0.05f,
                string.Format("blocked={0} ground={1:F1}", spBlocked, spg));
            // vertical motion: a thin slab inside the capsule must oppose the
            // motion (rising -> pushed down, never ejected through the slab)
            MeshBuilder slab = new MeshBuilder();
            slab.AddBox(-200f, 60f, -200f, 200f, 64f, 200f);
            string slabPath = WriteBin("slab", new MeshBuilder[] { slab }, new float[][] { M(0f, 0f, 0f) });
            FoliageCollision slc = new FoliageCollision(null, slabPath);
            float slx = 0f, sly = 40f, slz = 0f, slg = 0f;
            bool slGrounded = false;
            bool slBlocked = slc.Resolve(ref slx, ref sly, ref slz, 17f, 116f, ref slg, ref slGrounded, 64f, 300f);
            Check("vertical_rise_slab_blocks", sly <= 40.05f,
                string.Format("blocked={0} py={1:F1}", slBlocked, sly));
            float flx = 0f, fly = 50f, flz = 0f, flg = 0f;
            bool flGrounded = false;
            bool flBlocked = slc.Resolve(ref flx, ref fly, ref flz, 17f, 116f, ref flg, ref flGrounded, 64f, -300f);
            Check("vertical_fall_slab_supports", fly >= 63.9f,
                string.Format("blocked={0} py={1:F1}", flBlocked, fly));
        }

        Console.WriteLine("collision_selftest: " + _pass + "/" + (_pass + _fail) + " PASS"
            + (_fail > 0 ? " failed=" + string.Join(",", _failed.ToArray()) : ""));
        Environment.Exit(_fail == 0 ? 0 : 1);
    }
}
