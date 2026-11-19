using System.Text;
using UnityEngine;

/// <summary>Co-op event types (one byte in front of every <see cref="CoopNet"/> message).</summary>
public enum CoopEv : byte
{
    None,
    Kill,           // host → all: zombie died (killer, drop)
    Explosion,      // host → all: blast to show (damage is the host's)
    PickupSpawn,    // host → all
    PickupTaken,    // anyone → host → all
    BarrelHit,      // client → host
    BossThrow,      // host → all: debris / acid glob in flight
    BossIntro,      // host → all: the boss arrived
    ObjIssue,       // host → all
    ObjProgress,    // host → all (5 Hz)
    ObjComplete,    // host → all
    Radio,          // host → all
    TeamOver,       // host → all: nobody is standing
    Respawn,        // host → all: wave break — the fallen come back
}

/// <summary>Little-endian byte writer over a reusable buffer (pure, EditMode-tested).</summary>
public struct NetWriter
{
    public byte[] buf;
    public int len;

    public NetWriter(byte[] buffer) { buf = buffer; len = 0; }

    void Need(int n) { if (len + n > buf.Length) System.Array.Resize(ref buf, Mathf.Max(buf.Length * 2, len + n)); }

    public void U8(byte v) { Need(1); buf[len++] = v; }
    public void Bool(bool v) => U8((byte)(v ? 1 : 0));
    public void U16(ushort v) { Need(2); buf[len++] = (byte)v; buf[len++] = (byte)(v >> 8); }
    public void I32(int v) { Need(4); buf[len++] = (byte)v; buf[len++] = (byte)(v >> 8); buf[len++] = (byte)(v >> 16); buf[len++] = (byte)(v >> 24); }
    public void U64(ulong v) { I32((int)(v & 0xffffffff)); I32((int)(v >> 32)); }
    public void F32(float f) => I32(System.BitConverter.SingleToInt32Bits(f));
    public void XZ(Vector3 v) { F32(v.x); F32(v.z); }
    public void V3(Vector3 v) { F32(v.x); F32(v.y); F32(v.z); }

    public void Str(string s)
    {
        s = s ?? "";
        int n = Encoding.UTF8.GetByteCount(s);
        n = Mathf.Min(n, 255);
        U8((byte)n);
        Need(n);
        Encoding.UTF8.GetBytes(s, 0, Mathf.Min(s.Length, n), buf, len);
        len += n;
    }
}

/// <summary>Reader for <see cref="NetWriter"/> data; <see cref="Ok"/> turns false on a short buffer (values read as 0).</summary>
public struct NetReader
{
    readonly byte[] buf;
    readonly int len;
    int pos;
    public bool Ok { get; private set; }

    public NetReader(byte[] buffer, int length) { buf = buffer; len = length; pos = 0; Ok = buffer != null; }

    bool Has(int n) { if (!Ok || pos + n > len) { Ok = false; return false; } return true; }

    public byte U8() => Has(1) ? buf[pos++] : (byte)0;
    public bool Bool() => U8() != 0;
    public ushort U16() { if (!Has(2)) return 0; ushort v = (ushort)(buf[pos] | (buf[pos + 1] << 8)); pos += 2; return v; }
    public int I32() { if (!Has(4)) return 0; int v = buf[pos] | (buf[pos + 1] << 8) | (buf[pos + 2] << 16) | (buf[pos + 3] << 24); pos += 4; return v; }
    public ulong U64() { uint lo = (uint)I32(); uint hi = (uint)I32(); return lo | ((ulong)hi << 32); }
    public float F32() => System.BitConverter.Int32BitsToSingle(I32());
    public Vector3 XZ() { float x = F32(), z = F32(); return new Vector3(x, 0f, z); }
    public Vector3 V3() { float x = F32(), y = F32(), z = F32(); return new Vector3(x, y, z); }

    public string Str()
    {
        int n = U8();
        if (!Has(n)) return "";
        string s = Encoding.UTF8.GetString(buf, pos, n);
        pos += n;
        return s;
    }
}
