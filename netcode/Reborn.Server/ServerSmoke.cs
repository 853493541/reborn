using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Reborn.Rules;

namespace Reborn.Server
{
    /// <summary>
    /// End-to-end smoke for the server contract (the C# twin of the reference smoke in
    /// tools/netcode/reference/jx3_model.py): handshake/join, movement + state convergence,
    /// AOI both ways, ping/pong, disconnect + session resume.
    /// </summary>
    internal static class ServerSmoke
    {
        static int pass;
        static int fail;

        public static int Run(int port)
        {
            try
            {
                SmokeClient c1 = new SmokeClient("c1");
                byte[] key1 = new byte[16];
                for (int i = 0; i < 16; i++) key1[i] = (byte)'k';
                c1.Connect(port, key1, false);
                Check("handshake new session", c1.WaitFor("handshake_result", 3000) != null, "eid=" + c1.Eid);
                object join = c1.WaitFor("join", 3000);
                Check("join world spawns entity", c1.Eid > 0 && join != null, "");

                for (int i = 0; i < 30; i++)
                {
                    c1.Move(Movement.KFwd);
                    Thread.Sleep(33);
                }
                c1.Move(0);
                Thread.Sleep(400);
                double drift = Movement.Dist(c1.Pos, c1.ServerPos);
                Check("prediction converges after reconciling", drift < 0.6, string.Format("drift={0:F3}", drift));
                Check("moved forward", c1.ServerPos[2] > 3.0, string.Format("z={0:F2}", c1.ServerPos[2]));

                SmokeClient c2 = new SmokeClient("c2");
                byte[] key2 = new byte[16];
                for (int i = 0; i < 16; i++) key2[i] = (byte)'j';
                c2.Connect(port, key2, false);
                c2.WaitFor("handshake_result", 3000);
                c2.WaitFor("join", 3000);
                object add1 = c1.WaitFor("entity_add", 2000);
                object add2 = c2.WaitFor("entity_add", 2000);
                Check("aoi entity add both ways", add1 != null && add2 != null, "");

                c1.Ping(12345);
                object pong = c1.WaitFor("pong", 2000);
                Check("ping/pong", pong != null, "");

                double[] posBefore = c1.ServerPos;
                int resumeSeq = c1.Channel.RecvAck;
                c1.Close();
                Thread.Sleep(300);
                SmokeClient c1b = new SmokeClient("c1b");
                c1b.ResumeSeq = resumeSeq;
                c1b.Connect(port, key1, true);
                object hr = c1b.WaitFor("handshake_result", 3000);
                c1b.WaitFor("join", 3000);
                bool recovered = hr != null && hr.ToString() == "1";
                Check("reconnect recovers session", recovered, "recovered=" + (hr == null ? "?" : hr.ToString()));
                Check("resumed entity keeps position", Movement.Dist(c1b.ServerPos, posBefore) < 0.01,
                    "pos=(" + c1b.ServerPos[0].ToString("F2") + "," + c1b.ServerPos[2].ToString("F2") + ")");

                c1b.Close();
                c2.Close();
            }
            catch (Exception e)
            {
                fail++;
                Console.WriteLine("FAIL: smoke exception - " + e.Message);
            }

            Console.WriteLine("server smoke: {0} PASS, {1} FAIL", pass, fail);
            return fail == 0 ? 0 : 1;
        }

        static void Check(string name, bool ok, string detail)
        {
            if (ok) { pass++; Console.WriteLine("PASS: " + name + (detail.Length > 0 ? " - " + detail : "")); }
            else { fail++; Console.WriteLine("FAIL: " + name + (detail.Length > 0 ? " - " + detail : "")); }
        }
    }

    internal sealed class SmokeClient
    {
        public int Eid;
        public double[] Pos = new double[] { 0, 0, 0 };
        public double[] ServerPos = new double[] { 0, 0, 0 };
        public int ResumeSeq;
        public readonly ReliableChannel Channel = new ReliableChannel();

        readonly string name;
        readonly List<Tuple<int, int, double>> history = new List<Tuple<int, int, double>>();
        readonly ConcurrentQueue<KeyValuePair<string, string>> events = new ConcurrentQueue<KeyValuePair<string, string>>();
        readonly object sync = new object();
        TcpClient client;
        NetworkStream stream;
        int inputSeq;
        int keys;
        volatile bool running;
        Thread reader;

        public SmokeClient(string name) { this.name = name; }

        public void Connect(int port, byte[] key, bool resume)
        {
            client = new TcpClient("127.0.0.1", port);
            stream = client.GetStream();
            running = true;
            reader = new Thread(ReadLoop);
            reader.IsBackground = true;
            reader.Start();
            byte[] payload = new byte[19];
            Buffer.BlockCopy(key, 0, payload, 0, 16);
            Protocol.WriteU16(payload, 16, (ushort)ResumeSeq);
            payload[18] = resume ? (byte)1 : (byte)0;
            SendFrame(Protocol.OpHandshake, 0, payload);
        }

        public void Close()
        {
            running = false;
            try { client.Close(); } catch (Exception) { }
        }

        void SendFrame(ushort op, uint param, byte[] payload)
        {
            lock (sync)
            {
                byte[] frame = Channel.Build(op, param, payload ?? Protocol.Empty, 0);
                if (frame != null) stream.Write(frame, 0, frame.Length);
            }
        }

        public void Move(int newKeys)
        {
            keys = newKeys;
            inputSeq++;
            history.Add(Tuple.Create(inputSeq, keys, 1.0 / Protocol.TickHz));
            Pos = Movement.ApplyInput(Pos, keys, 1.0 / Protocol.TickHz);
            byte[] payload = JsonSerializer.SerializeToUtf8Bytes(new { keys = keys, facing = 0 });
            SendFrame(Protocol.OpMoveInput, (uint)inputSeq, payload);
        }

        public void Ping(int token)
        {
            SendFrame(Protocol.OpPing, (uint)token, Protocol.Empty);
        }

        public object WaitFor(string kind, int timeoutMs)
        {
            int waited = 0;
            while (waited < timeoutMs)
            {
                KeyValuePair<string, string> ev;
                if (events.TryDequeue(out ev) && ev.Key == kind) return ev.Value;
                Thread.Sleep(10);
                waited += 10;
            }
            return null;
        }

        void ReadLoop()
        {
            try
            {
                byte[] head = new byte[Protocol.HeaderSize + 2];
                while (running)
                {
                    if (!ReadExact(head, 0, Protocol.HeaderSize + 2)) return;
                    int size = Protocol.ReadU16(head, Protocol.HeaderSize);
                    byte[] body = new byte[size];
                    if (size > 0 && !ReadExact(body, 0, size)) return;
                    byte[] whole = new byte[Protocol.HeaderSize + 2 + size];
                    Buffer.BlockCopy(head, 0, whole, 0, head.Length);
                    Buffer.BlockCopy(body, 0, whole, head.Length, size);
                    Protocol.Frame f = Protocol.Decode(whole);
                    bool fresh;
                    lock (sync) fresh = Channel.OnFrame(f.Seq, f.Ack);
                    if (!fresh) continue;
                    Handle(f);
                    lock (sync)
                    {
                        List<byte[]> due = Channel.Due();
                        for (int i = 0; i < due.Count; i++) stream.Write(due[i], 0, due[i].Length);
                    }
                }
            }
            catch (Exception) { }
        }

        bool ReadExact(byte[] buf, int off, int count)
        {
            int got = 0;
            while (got < count)
            {
                int n;
                try { n = stream.Read(buf, off + got, count - got); }
                catch (Exception) { return false; }
                if (n <= 0) return false;
                got += n;
            }
            return true;
        }

        void Handle(Protocol.Frame f)
        {
            switch (f.Op)
            {
                case Protocol.OpHandshakeResult:
                    events.Enqueue(new KeyValuePair<string, string>("handshake_result", f.Param.ToString()));
                    break;
                case Protocol.OpJoinWorld:
                    Eid = (int)f.Param;
                    using (JsonDocument doc = JsonDocument.Parse(f.Payload))
                    {
                        Pos = ReadVec(doc.RootElement, "pos");
                        ServerPos = Pos;
                    }
                    events.Enqueue(new KeyValuePair<string, string>("join", "1"));
                    break;
                case Protocol.OpMoveState:
                    using (JsonDocument doc = JsonDocument.Parse(f.Payload))
                    {
                        ServerPos = ReadVec(doc.RootElement, "pos");
                    }
                    int last = (int)f.Param;
                    List<Tuple<int, int, double>> remaining = new List<Tuple<int, int, double>>();
                    for (int i = 0; i < history.Count; i++) if (history[i].Item1 > last) remaining.Add(history[i]);
                    double[] pos = new double[] { ServerPos[0], ServerPos[1], ServerPos[2] };
                    for (int i = 0; i < remaining.Count; i++)
                        pos = Movement.ApplyInput(pos, remaining[i].Item2, remaining[i].Item3);
                    history.Clear();
                    history.AddRange(remaining);
                    Pos = pos;
                    break;
                case Protocol.OpEntityAdd:
                    events.Enqueue(new KeyValuePair<string, string>("entity_add", f.Param.ToString()));
                    break;
                case Protocol.OpEntityRemove:
                    events.Enqueue(new KeyValuePair<string, string>("entity_remove", f.Param.ToString()));
                    break;
                case Protocol.OpPong:
                    events.Enqueue(new KeyValuePair<string, string>("pong", f.Param.ToString()));
                    break;
            }
        }

        static double[] ReadVec(JsonElement root, string key)
        {
            JsonElement arr = root.GetProperty(key);
            double[] v = new double[3];
            int i = 0;
            foreach (JsonElement e in arr.EnumerateArray()) v[i++] = e.GetDouble();
            return v;
        }
    }
}

