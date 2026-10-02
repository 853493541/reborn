using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Reborn.Rules;

namespace Reborn.Server
{
    /// <summary>
    /// Minimal authoritative server for the reborn netcode contract
    /// (docs/netcode/REBORN_SERVER_SPEC.md, reference tools/netcode/reference/jx3_model.py):
    /// TCP + framed protocol, handshake/session resume, 30 Hz tick, 10 Hz move state +
    /// distance-based AOI. Payloads are JSON with the reference's keys.
    ///
    /// Usage: Reborn.Server [--port N] [--selftest]
    /// </summary>
    internal static class Program
    {
        static int Main(string[] args)
        {
            int port = 0;
            bool selftest = false;
            double[] spawn = null;
            string heightmap = null;
            double speed = 0.0;
            double aoi = 0.0;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--port" && i + 1 < args.Length) port = int.Parse(args[++i]);
                else if (args[i] == "--selftest") selftest = true;
                else if (args[i] == "--aoi" && i + 1 < args.Length)
                    aoi = double.Parse(args[++i], System.Globalization.CultureInfo.InvariantCulture);
                else if (args[i] == "--heightmap" && i + 1 < args.Length) heightmap = args[++i];
                else if (args[i] == "--speed" && i + 1 < args.Length)
                    speed = double.Parse(args[++i], System.Globalization.CultureInfo.InvariantCulture);
                else if (args[i] == "--spawn" && i + 1 < args.Length)
                {
                    string[] p = args[++i].Split(',');
                    if (p.Length == 3)
                        spawn = new double[] { double.Parse(p[0], System.Globalization.CultureInfo.InvariantCulture),
                                               double.Parse(p[1], System.Globalization.CultureInfo.InvariantCulture),
                                               double.Parse(p[2], System.Globalization.CultureInfo.InvariantCulture) };
                }
            }

            GameServer server = new GameServer();
            if (spawn != null) server.State.SpawnPos = spawn;
            if (speed > 0.0) server.State.MoveSpeed = speed;
            if (aoi > 0.0) server.State.AoiRange = aoi;
            if (heightmap != null)
            {
                string hmErr;
                Heightmap hm = Heightmap.Load(heightmap, out hmErr);
                if (hm == null) Console.WriteLine("heightmap load failed: " + hmErr);
                else
                {
                    server.State.Ground = hm.Sample;
                    Console.WriteLine(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                        "heightmap {0} nx={1} nz={2} step={3:F0} (ground follow on)", heightmap, hm.Nx, hm.Nz, hm.Step));
                }
            }
            int bound = server.Start(port);
            Console.WriteLine("reborn server listening on 127.0.0.1:" + bound);
            if (selftest)
            {
                int rc = ServerSmoke.Run(bound);
                server.Stop();
                return rc;
            }
            Console.WriteLine("Ctrl+C to stop");
            Thread.Sleep(Timeout.Infinite);
            return 0;
        }
    }

    /// <summary>Sessions, entities and the authoritative tick.</summary>
    internal sealed class GameServer
    {
        sealed class SessionState
        {
            public byte[] Key;
            public Entity Entity;
            public double Expires;
        }

        readonly Dictionary<byte[], SessionState> sessions = new Dictionary<byte[], SessionState>(ByteArrayComparer.Instance);
        readonly List<Peer> peers = new List<Peer>();
        readonly GameState state = new GameState();
        TcpListener listener;
        Task acceptTask;
        Task tickTask;
        volatile bool running;

        public int Start(int port)
        {
            listener = new TcpListener(IPAddress.Loopback, port);
            listener.Start();
            running = true;
            acceptTask = Task.Run(AcceptLoop);
            tickTask = Task.Run(TickLoop);
            return ((IPEndPoint)listener.LocalEndpoint).Port;
        }

        public void Stop()
        {
            running = false;
            if (listener != null) listener.Stop();
        }

        async Task AcceptLoop()
        {
            while (running)
            {
                TcpClient client;
                try { client = await listener.AcceptTcpClientAsync(); }
                catch (Exception) { return; }
                Peer peer = new Peer(this, client);
                lock (peers) peers.Add(peer);
                _ = Task.Run(peer.Run);
            }
        }

        async Task TickLoop()
        {
            double dt = 1.0 / Protocol.TickHz;
            int n = 0;
            while (running)
            {
                await Task.Delay((int)(dt * 1000.0));
                double nowS = ReliableChannel.DefaultClockMs() / 1000.0;
                state.Step(nowS, dt);
                n++;
                if (n % Protocol.SnapshotEvery != 0) continue;
                List<Peer> snapshot;
                lock (peers) snapshot = new List<Peer>(peers);
                foreach (Peer p in snapshot) await p.PushState(nowS);
            }
        }

        public GameState State { get { return state; } }

        public Entity Handshake(byte[] key, bool resume, out int recovered)
        {
            SessionState st;
            if (resume && sessions.TryGetValue(key, out st) && ReliableChannel.DefaultClockMs() / 1000.0 < st.Expires)
            {
                recovered = 1;
                st.Expires = ReliableChannel.DefaultClockMs() / 1000.0 + Protocol.ReconnectMs / 1000.0;
                return st.Entity;
            }
            Entity ent = state.Spawn(Hex(key).Substring(0, 8));
            sessions[key] = new SessionState { Key = key, Entity = ent, Expires = ReliableChannel.DefaultClockMs() / 1000.0 + Protocol.ReconnectMs / 1000.0 };
            recovered = 0;
            return ent;
        }

        public void Expire(Peer peer)
        {
            lock (peers) peers.Remove(peer);
            if (peer.Entity != null && peer.SessionKey != null)
            {
                SessionState st;
                if (sessions.TryGetValue(peer.SessionKey, out st))
                    st.Expires = ReliableChannel.DefaultClockMs() / 1000.0 + Protocol.ReconnectMs / 1000.0;
            }
        }

        static string Hex(byte[] b)
        {
            char[] c = new char[b.Length * 2];
            const string h = "0123456789abcdef";
            for (int i = 0; i < b.Length; i++) { c[i * 2] = h[b[i] >> 4]; c[i * 2 + 1] = h[b[i] & 0xF]; }
            return new string(c);
        }
    }

    internal sealed class ByteArrayComparer : IEqualityComparer<byte[]>
    {
        public static readonly ByteArrayComparer Instance = new ByteArrayComparer();
        public bool Equals(byte[] a, byte[] b)
        {
            if (a == null || b == null) return ReferenceEquals(a, b);
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }
        public int GetHashCode(byte[] a)
        {
            int h = 17;
            for (int i = 0; i < a.Length; i++) h = h * 31 + a[i];
            return h;
        }
    }

    internal sealed class Peer
    {
        readonly GameServer server;
        readonly TcpClient client;
        readonly NetworkStream stream;
        readonly ReliableChannel channel = new ReliableChannel();
        readonly object sync = new object();
        readonly HashSet<int> known = new HashSet<int>();

        public Entity Entity;
        public byte[] SessionKey;
        double lastRecvS;

        public Peer(GameServer server, TcpClient client)
        {
            this.server = server;
            this.client = client;
            this.stream = client.GetStream();
            lastRecvS = ReliableChannel.DefaultClockMs() / 1000.0;
        }

        public async Task Run()
        {
            try
            {
                byte[] header = new byte[Protocol.HeaderSize + 2];
                while (true)
                {
                    await ReadExact(header, 0, Protocol.HeaderSize + 2);
                    int size = Protocol.ReadU16(header, Protocol.HeaderSize);
                    byte[] payload = new byte[size];
                    if (size > 0) await ReadExact(payload, 0, size);
                    byte[] whole = new byte[Protocol.HeaderSize + 2 + size];
                    Buffer.BlockCopy(header, 0, whole, 0, header.Length);
                    Buffer.BlockCopy(payload, 0, whole, header.Length, size);
                    Protocol.Frame f = Protocol.Decode(whole);
                    lastRecvS = ReliableChannel.DefaultClockMs() / 1000.0;
                    bool fresh;
                    lock (sync) fresh = channel.OnFrame(f.Seq, f.Ack);
                    if (!fresh) continue;
                    await Dispatch(f);
                    FlushDue();
                }
            }
            catch (Exception) { }
            finally
            {
                server.Expire(this);
                try { client.Close(); } catch (Exception) { }
            }
        }

        async Task ReadExact(byte[] buf, int off, int count)
        {
            int got = 0;
            while (got < count)
            {
                int n = await stream.ReadAsync(buf, off + got, count - got);
                if (n <= 0) throw new IOException("closed");
                got += n;
            }
        }

        void SendFrame(ushort op, uint param, byte[] payload)
        {
            lock (sync)
            {
                byte[] frame = channel.Build(op, param, payload ?? Protocol.Empty, 0);
                if (frame == null) return;
                stream.Write(frame, 0, frame.Length);
            }
        }

        void SendJson(ushort op, uint param, object msg)
        {
            byte[] payload = msg == null ? Protocol.Empty : JsonSerializer.SerializeToUtf8Bytes(msg);
            SendFrame(op, param, payload);
        }

        void FlushDue()
        {
            lock (sync)
            {
                List<byte[]> due = channel.Due();
                for (int i = 0; i < due.Count; i++) stream.Write(due[i], 0, due[i].Length);
            }
        }

        async Task Dispatch(Protocol.Frame f)
        {
            switch (f.Op)
            {
                case Protocol.OpHandshake:
                {
                    byte[] key = new byte[16];
                    Buffer.BlockCopy(f.Payload, 0, key, 0, 16);
                    int resumeSeq = Protocol.ReadU16(f.Payload, 16);
                    bool resume = f.Payload[18] != 0;
                    SessionKey = key;
                    int recovered;
                    Entity = server.Handshake(key, resume, out recovered);
                    channel.OnFrame(channel.RecvAck, resumeSeq); // accept the client's resume ack floor
                    SendFrame(Protocol.OpHandshakeResult, (uint)recovered, Protocol.Empty);
                    SendJson(Protocol.OpJoinWorld, (uint)Entity.Eid, new
                    {
                        eid = Entity.Eid,
                        pos = Entity.Pos,
                        hp = Entity.Hp,
                        server_tick = server.State.ServerTick,
                    });
                    known.Clear();
                    break;
                }
                case Protocol.OpPing:
                    SendFrame(Protocol.OpPong, f.Param, Protocol.Empty);
                    break;
                case Protocol.OpMoveInput:
                {
                    if (Entity == null) break;
                    using (JsonDocument doc = JsonDocument.Parse(f.Payload))
                    {
                        JsonElement root = doc.RootElement;
                        Entity.Keys = root.TryGetProperty("keys", out JsonElement k) ? k.GetInt32() : 0;
                        Entity.Facing = root.TryGetProperty("facing", out JsonElement fa) ? fa.GetInt32() : 0;
                    }
                    Entity.LastInputSeq = (int)f.Param;
                    break;
                }
                case Protocol.OpAck:
                    break;
            }
            await Task.CompletedTask;
        }

        public async Task PushState(double nowS)
        {
            Entity ent = Entity;
            if (ent == null) return;
            if (nowS - lastRecvS > Protocol.DeadTimeoutMs / 1000.0)
            {
                try { client.Close(); } catch (Exception) { }
                return;
            }
            SendJson(Protocol.OpMoveState, (uint)ent.LastInputSeq, new
            {
                pos = ent.Pos,
                vel = ent.Vel,
                flags = ent.Locked(nowS) ? 1 : 0,
                server_tick = server.State.ServerTick,
            });
            List<Entity> visible = server.State.Visible(ent);
            HashSet<int> current = new HashSet<int>();
            for (int i = 0; i < visible.Count; i++) current.Add(visible[i].Eid);
            foreach (int eid in known)
            {
                if (!current.Contains(eid))
                    SendJson(Protocol.OpEntityRemove, (uint)eid, new { eid = eid });
            }
            for (int i = 0; i < visible.Count; i++)
            {
                Entity e = visible[i];
                if (!known.Contains(e.Eid))
                    SendJson(Protocol.OpEntityAdd, (uint)e.Eid, new { eid = e.Eid, name = e.Name, pos = e.Pos, hp = e.Hp });
            }
            known.Clear();
            foreach (int eid in current) known.Add(eid);
            if (visible.Count > 0)
            {
                List<object> recs = new List<object>();
                for (int i = 0; i < visible.Count; i++)
                {
                    Entity e = visible[i];
                    recs.Add(new { eid = e.Eid, pos = e.Pos, facing = e.Facing, hp = e.Hp });
                }
                SendJson(Protocol.OpEntitySnapshot, 0, new { entities = recs });
            }
            FlushDue();
            await Task.CompletedTask;
        }
    }
}

