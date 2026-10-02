// Reborn netcode client (M2): framed protocol on the shared Reborn.Rules sources,
// handshake + join, input upstream, server state downstream (move state, AOI
// entities). Transport only - the game wiring lives in RebornClient.cs.
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Reborn.Rules;

internal sealed class NetRemote
{
    public int Eid;
    public string Name = "";
    public double[] Pos = new double[] { 0, 0, 0 };
    public int Facing;
    public int Hp;
}

internal sealed class NetClient
{
    public volatile bool Connected;
    public volatile bool Joined;
    public int Eid;
    public double[] ServerPos = new double[] { 0, 0, 0 };
    public double[] ServerVel = new double[] { 0, 0, 0 };
    public int ServerFlags;
    public int LastInputSeq;
    public int Corrections;
    public readonly ConcurrentQueue<string> Events = new ConcurrentQueue<string>();
    public readonly Dictionary<int, NetRemote> Remote = new Dictionary<int, NetRemote>();

    readonly ReliableChannel channel = new ReliableChannel();
    readonly object sync = new object();
    TcpClient client;
    NetworkStream stream;
    Thread reader;
    volatile bool running;
    int inputSeq;
    int pingSeq;

    public bool Connect(string host, int port, out string err)
    {
        err = null;
        try
        {
            client = new TcpClient();
            client.NoDelay = true;
            client.Connect(host, port);
            stream = client.GetStream();
            running = true;
            reader = new Thread(ReadLoop);
            reader.IsBackground = true;
            reader.Start();
            byte[] key = new byte[16];
            new Random(Environment.TickCount ^ GetHashCode()).NextBytes(key);
            byte[] payload = new byte[19];
            Buffer.BlockCopy(key, 0, payload, 0, 16);
            Protocol.WriteU16(payload, 16, 0);
            payload[18] = 0;
            SendFrame(Protocol.OpHandshake, 0, payload);
            Connected = true;
            Events.Enqueue("net connect " + host + ":" + port);
            return true;
        }
        catch (Exception e)
        {
            err = e.Message;
            return false;
        }
    }

    public bool RemoteHas(int eid)
    {
        lock (sync) return Remote.ContainsKey(eid);
    }

    public List<NetRemote> RemoteSnapshot()
    {
        lock (sync) return new List<NetRemote>(Remote.Values);
    }

    public void Close()
    {
        running = false;
        try { if (client != null) client.Close(); } catch (Exception) { }
        Connected = false;
    }

    public void SendMoveInput(int keys, int facing)
    {
        if (!Connected) return;
        inputSeq++;
        byte[] payload = Encoding.UTF8.GetBytes("{\"keys\":" + keys + ",\"facing\":" + facing + "}");
        SendFrame(Protocol.OpMoveInput, (uint)inputSeq, payload);
    }

    public void SendPing()
    {
        if (!Connected) return;
        pingSeq++;
        SendFrame(Protocol.OpPing, (uint)(Environment.TickCount & 0x7FFFFFFF), Protocol.Empty);
    }

    void SendFrame(ushort op, uint param, byte[] payload)
    {
        if (stream == null) return;
        lock (sync)
        {
            byte[] frame = channel.Build(op, param, payload, 0);
            if (frame == null) return;
            try { stream.Write(frame, 0, frame.Length); }
            catch (Exception) { running = false; Connected = false; }
        }
    }

    void ReadLoop()
    {
        try
        {
            byte[] head = new byte[Protocol.HeaderSize + 2];
            while (running)
            {
                if (!ReadExact(head, 0, Protocol.HeaderSize + 2)) break;
                int size = Protocol.ReadU16(head, Protocol.HeaderSize);
                byte[] body = new byte[size];
                if (size > 0 && !ReadExact(body, 0, size)) break;
                byte[] whole = new byte[Protocol.HeaderSize + 2 + size];
                Buffer.BlockCopy(head, 0, whole, 0, head.Length);
                Buffer.BlockCopy(body, 0, whole, head.Length, size);
                Protocol.Frame f = Protocol.Decode(whole);
                bool fresh;
                lock (sync) fresh = channel.OnFrame(f.Seq, f.Ack);
                if (!fresh) continue;
                Handle(f);
                lock (sync)
                {
                    List<byte[]> due = channel.Due();
                    for (int i = 0; i < due.Count; i++) stream.Write(due[i], 0, due[i].Length);
                }
            }
        }
        catch (Exception) { }
        finally
        {
            Connected = false;
            Events.Enqueue("net disconnected");
        }
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
                Events.Enqueue("net handshake recovered=" + f.Param);
                break;
            case Protocol.OpJoinWorld:
            {
                Dictionary<string, object> msg = MiniJson.AsObject(MiniJson.Parse(Encoding.UTF8.GetString(f.Payload)));
                Eid = (int)f.Param;
                double[] p = MiniJson.Vec3(msg, "pos");
                if (p != null) ServerPos = p;
                Joined = true;
                Events.Enqueue("net join eid=" + Eid);
                break;
            }
            case Protocol.OpMoveState:
            {
                Dictionary<string, object> msg = MiniJson.AsObject(MiniJson.Parse(Encoding.UTF8.GetString(f.Payload)));
                double[] p = MiniJson.Vec3(msg, "pos");
                double[] v = MiniJson.Vec3(msg, "vel");
                if (p != null) ServerPos = p;
                if (v != null) ServerVel = v;
                ServerFlags = MiniJson.Int(msg, "flags", 0);
                LastInputSeq = (int)f.Param;
                break;
            }
            case Protocol.OpEntityAdd:
            {
                Dictionary<string, object> msg = MiniJson.AsObject(MiniJson.Parse(Encoding.UTF8.GetString(f.Payload)));
                NetRemote r = new NetRemote();
                r.Eid = (int)f.Param;
                r.Name = MiniJson.Str(msg, "name", "");
                double[] p = MiniJson.Vec3(msg, "pos");
                if (p != null) r.Pos = p;
                r.Hp = MiniJson.Int(msg, "hp", 0);
                lock (sync) Remote[r.Eid] = r;
                Events.Enqueue("net entity add eid=" + r.Eid + " name=" + r.Name);
                break;
            }
            case Protocol.OpEntityRemove:
            {
                int eid = (int)f.Param;
                lock (sync) Remote.Remove(eid);
                Events.Enqueue("net entity remove eid=" + eid);
                break;
            }
            case Protocol.OpEntitySnapshot:
            {
                Dictionary<string, object> msg = MiniJson.AsObject(MiniJson.Parse(Encoding.UTF8.GetString(f.Payload)));
                List<object> list = MiniJson.AsArray(msg == null ? null : (msg.ContainsKey("entities") ? msg["entities"] : null));
                if (list != null)
                {
                    for (int i = 0; i < list.Count; i++)
                    {
                        Dictionary<string, object> rec = MiniJson.AsObject(list[i]);
                        if (rec == null) continue;
                        int eid = MiniJson.Int(rec, "eid", 0);
                        NetRemote r;
                        bool have;
                        lock (sync) have = Remote.TryGetValue(eid, out r);
                        if (have)
                        {
                            double[] p = MiniJson.Vec3(rec, "pos");
                            if (p != null) r.Pos = p;
                            r.Facing = MiniJson.Int(rec, "facing", 0);
                            r.Hp = MiniJson.Int(rec, "hp", 0);
                        }
                    }
                }
                break;
            }
            case Protocol.OpPong:
                Events.Enqueue("net pong token=" + f.Param);
                break;
        }
    }
}


