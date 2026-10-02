using System;

namespace Reborn.Rules
{
    /// <summary>
    /// Wire contract shared by client and server. Mirrors
    /// tools/netcode/reference/jx3_model.py (10x PASS) and docs/netcode/REBORN_SERVER_SPEC.md:
    /// 15-byte header &lt;HBHHII (op, flags, seq, ack, sid, param) + 2-byte payload size +
    /// JSON payload (&lt;= MaxPayload). Little-endian throughout.
    /// </summary>
    public static class Protocol
    {
        public const ushort OpHandshake = 0x0001;
        public const ushort OpHandshakeResult = 0x0002;
        public const ushort OpPing = 0x0006;
        public const ushort OpPong = 0x0007;
        public const ushort OpAck = 0x0008;
        public const ushort OpJoinWorld = 0x0010;
        public const ushort OpMoveInput = 0x0020;
        public const ushort OpMoveState = 0x0021;
        public const ushort OpMoveCorrection = 0x0022;
        public const ushort OpMoveCtrl = 0x0023;
        public const ushort OpEntityAdd = 0x0030;
        public const ushort OpEntityRemove = 0x0031;
        public const ushort OpEntitySnapshot = 0x0032;
        public const ushort OpCastSkill = 0x0040;
        public const ushort OpSkillPrepare = 0x0041;
        public const ushort OpSkillCast = 0x0042;
        public const ushort OpSkillEffect = 0x0043;
        public const ushort OpSkillReject = 0x0044;
        public const ushort OpCooldown = 0x0045;
        public const ushort OpBuffSync = 0x0046;
        public const ushort OpRoutineSync = 0x006E;

        public const byte FlagRetransmit = 0x01;
        public const byte FlagHasAck = 0x02;

        public const int HeaderSize = 15;
        public const int MaxPayload = 0x8000;

        public const double TickHz = 30.0;
        public const int SnapshotEvery = 3;
        public const double PingMs = 3000.0;
        public const double DeadTimeoutMs = 12000.0;
        public const double ReconnectMs = 60000.0;
        public const double RtoMs = 250.0;
        public const int MaxTries = 8;
        public const int Window = 2048;
        public const double AoiRange = 100.0;

        public struct Frame
        {
            public ushort Op;
            public byte Flags;
            public ushort Seq;
            public ushort Ack;
            public uint Sid;
            public uint Param;
            public byte[] Payload;
        }

        /// <summary>Header + size + payload.</summary>
        public static byte[] Encode(ushort op, int seq, int ack, uint param, byte[] payload, byte flags, uint sid)
        {
            if (payload == null) payload = Empty;
            if (payload.Length > MaxPayload) throw new ArgumentException("payload too large", "payload");
            byte[] frame = new byte[HeaderSize + 2 + payload.Length];
            WriteU16(frame, 0, op);
            frame[2] = flags;
            WriteU16(frame, 3, (ushort)(seq & 0xFFFF));
            WriteU16(frame, 5, (ushort)(ack & 0xFFFF));
            WriteU32(frame, 7, sid);
            WriteU32(frame, 11, param);
            WriteU16(frame, 15, (ushort)payload.Length);
            Buffer.BlockCopy(payload, 0, frame, HeaderSize + 2, payload.Length);
            return frame;
        }

        /// <summary>Decode a complete frame (header + size + payload).</summary>
        public static Frame Decode(byte[] buf, int offset, int length)
        {
            if (buf == null) throw new ArgumentNullException("buf");
            if (length < HeaderSize + 2) throw new ArgumentException("short frame", "length");
            Frame f;
            f.Op = ReadU16(buf, offset);
            f.Flags = buf[offset + 2];
            f.Seq = ReadU16(buf, offset + 3);
            f.Ack = ReadU16(buf, offset + 5);
            f.Sid = ReadU32(buf, offset + 7);
            f.Param = ReadU32(buf, offset + 11);
            int size = ReadU16(buf, offset + 15);
            if (length < HeaderSize + 2 + size) throw new ArgumentException("truncated payload", "length");
            f.Payload = new byte[size];
            Buffer.BlockCopy(buf, offset + HeaderSize + 2, f.Payload, 0, size);
            return f;
        }

        public static Frame Decode(byte[] buf)
        {
            return Decode(buf, 0, buf.Length);
        }

        public static readonly byte[] Empty = new byte[0];

        public static void WriteU16(byte[] b, int o, ushort v)
        {
            b[o] = (byte)(v & 0xFF);
            b[o + 1] = (byte)((v >> 8) & 0xFF);
        }

        public static void WriteU32(byte[] b, int o, uint v)
        {
            b[o] = (byte)(v & 0xFF);
            b[o + 1] = (byte)((v >> 8) & 0xFF);
            b[o + 2] = (byte)((v >> 16) & 0xFF);
            b[o + 3] = (byte)((v >> 24) & 0xFF);
        }

        public static ushort ReadU16(byte[] b, int o)
        {
            return (ushort)(b[o] | (b[o + 1] << 8));
        }

        public static uint ReadU32(byte[] b, int o)
        {
            return (uint)(b[o] | (b[o + 1] << 8) | (b[o + 2] << 16) | (b[o + 3] << 24));
        }
    }
}
