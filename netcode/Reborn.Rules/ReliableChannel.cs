using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Reborn.Rules
{
    /// <summary>
    /// Serial/ack window with retransmit and duplicate suppression - the C# twin of
    /// <c>ReliableChannel</c> in tools/netcode/reference/jx3_model.py. The clock is
    /// injectable so parity vectors can replay deterministic timing.
    /// </summary>
    public sealed class ReliableChannel
    {
        sealed class Unacked
        {
            public byte[] Frame;
            public double T;
            public int Tries;
        }

        static readonly Stopwatch Clock = Stopwatch.StartNew();

        readonly Func<double> nowMs;
        readonly Dictionary<int, Unacked> unacked = new Dictionary<int, Unacked>();
        readonly HashSet<int> seen = new HashSet<int>();

        public readonly HashSet<ushort> DropOnce = new HashSet<ushort>();
        public int Retransmits;

        int sendSeq;
        int recvAck;

        public ReliableChannel() : this(null) { }

        public ReliableChannel(Func<double> clockMs)
        {
            nowMs = clockMs ?? DefaultClockMs;
        }

        public static double DefaultClockMs()
        {
            return Clock.Elapsed.TotalMilliseconds;
        }

        public int SendSeq { get { return sendSeq; } }
        public int RecvAck { get { return recvAck; } }
        public int UnackedCount { get { return unacked.Count; } }

        /// <summary>Build a frame, register it unacked, honour a one-shot drop request. Returns null when dropped.</summary>
        public byte[] Build(ushort op, uint param, byte[] payload, uint sid)
        {
            int seq = sendSeq;
            sendSeq = (sendSeq + 1) & 0xFFFF;
            byte flags = Protocol.FlagHasAck;
            byte[] frame = Protocol.Encode(op, seq, recvAck, param, payload, flags, sid);
            unacked[seq] = new Unacked { Frame = frame, T = nowMs(), Tries = 0 };
            if (DropOnce.Contains(op))
            {
                DropOnce.Remove(op);
                return null;
            }
            return frame;
        }

        public byte[] Build(ushort op, uint param, byte[] payload)
        {
            return Build(op, param, payload, 0);
        }

        /// <summary>Ack handling + duplicate suppression; false = duplicate (skip dispatch).</summary>
        public bool OnFrame(int seq, int ack)
        {
            recvAck = seq;
            List<int> done = null;
            foreach (int s in unacked.Keys)
            {
                if (s <= ack)
                {
                    if (done == null) done = new List<int>();
                    done.Add(s);
                }
            }
            if (done != null)
            {
                for (int i = 0; i < done.Count; i++) unacked.Remove(done[i]);
            }
            if (seen.Contains(seq)) return false;
            seen.Add(seq);
            if (seen.Count > 4096)
            {
                HashSet<int> keep = new HashSet<int>();
                foreach (int s in seen)
                {
                    if (s > seq - 2048) keep.Add(s);
                }
                seen.Clear();
                foreach (int s in keep) seen.Add(s);
            }
            return true;
        }

        /// <summary>Frames past RTO with tries left; each returned frame is a retransmit (FLAG_RETRANSMIT).</summary>
        public List<byte[]> Due()
        {
            List<byte[]> outFrames = new List<byte[]>();
            double t = nowMs();
            List<int> keys = new List<int>(unacked.Keys);
            for (int i = 0; i < keys.Count; i++)
            {
                int seq = keys[i];
                Unacked rec = unacked[seq];
                if ((t - rec.T) >= Protocol.RtoMs && rec.Tries < Protocol.MaxTries)
                {
                    rec.T = t;
                    rec.Tries++;
                    Protocol.Frame f = Protocol.Decode(rec.Frame);
                    outFrames.Add(Protocol.Encode(f.Op, f.Seq, recvAck, f.Param, f.Payload,
                        (byte)(f.Flags | Protocol.FlagRetransmit), f.Sid));
                    Retransmits++;
                }
            }
            return outFrames;
        }
    }
}
