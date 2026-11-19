using UnityEngine;

/// <summary>One zombie in a host → client snapshot.</summary>
public struct ZombieNetState
{
    public ushort id;
    public ZombieKind kind;
    public bool runner, rising;
    public Vector3 pos;      // y ignored (ground)
    public float yaw;        // degrees
    public float gait;       // 0 idle · 1 walk · 2 run
    public float hp01;
    public float maxHp;
}

/// <summary>Wave state riding on every snapshot (clients don't run their own waves).</summary>
public struct WaveNetState
{
    public int wave;
    public bool inBreak;
    public float timeLeft;
}

/// <summary>
/// Pure, allocation-free snapshot codec (EditMode-tested). Layout: header (wave u16, flags u8, timeLeft u16 ×0.1 s,
/// count u16) then per zombie: id u16, kind/flags u8, x f32, z f32, yaw u8, gait u8, hp u8, maxHp u16 = 16 bytes.
/// 70 zombies ≈ 1.1 KB → sent fragmented-reliable-sequenced at 15 Hz by <see cref="NetZombies"/>.
/// </summary>
public static class ZombieSnapshot
{
    public const int HeaderBytes = 7;
    public const int ZombieBytes = 16;
    public static int Size(int count) => HeaderBytes + count * ZombieBytes;

    public static int Write(byte[] buf, WaveNetState w, ZombieNetState[] zs, int count)
    {
        int o = 0;
        U16(buf, ref o, (ushort)Mathf.Clamp(w.wave, 0, ushort.MaxValue));
        buf[o++] = (byte)(w.inBreak ? 1 : 0);
        U16(buf, ref o, (ushort)Mathf.Clamp(Mathf.RoundToInt(w.timeLeft * 10f), 0, ushort.MaxValue));
        U16(buf, ref o, (ushort)count);
        for (int i = 0; i < count; i++)
        {
            var z = zs[i];
            U16(buf, ref o, z.id);
            buf[o++] = (byte)(((int)z.kind & 0x0f) | (z.runner ? 0x10 : 0) | (z.rising ? 0x20 : 0));
            F32(buf, ref o, z.pos.x);
            F32(buf, ref o, z.pos.z);
            buf[o++] = (byte)Mathf.RoundToInt(Mathf.Repeat(z.yaw, 360f) / 360f * 255f);
            buf[o++] = (byte)Mathf.Clamp(Mathf.RoundToInt(z.gait * 100f), 0, 255);
            buf[o++] = (byte)Mathf.Clamp(Mathf.RoundToInt(z.hp01 * 255f), 0, 255);
            U16(buf, ref o, (ushort)Mathf.Clamp(Mathf.RoundToInt(z.maxHp), 0, ushort.MaxValue));
        }
        return o;
    }

    /// <summary>Reads into <paramref name="zs"/> (grown if needed); returns the zombie count, -1 on a malformed buffer.</summary>
    public static int Read(byte[] buf, int len, out WaveNetState w, ref ZombieNetState[] zs)
    {
        w = default;
        if (len < HeaderBytes) return -1;
        int o = 0;
        w.wave = R16(buf, ref o);
        w.inBreak = buf[o++] != 0;
        w.timeLeft = R16(buf, ref o) / 10f;
        int count = R16(buf, ref o);
        if (len < Size(count)) return -1;
        if (zs == null || zs.Length < count) zs = new ZombieNetState[Mathf.Max(count, 16)];
        for (int i = 0; i < count; i++)
        {
            ZombieNetState z;
            z.id = R16(buf, ref o);
            int f = buf[o++];
            z.kind = (ZombieKind)(f & 0x0f);
            z.runner = (f & 0x10) != 0;
            z.rising = (f & 0x20) != 0;
            float x = RF32(buf, ref o), zz = RF32(buf, ref o);
            z.pos = new Vector3(x, 0f, zz);
            z.yaw = buf[o++] / 255f * 360f;
            z.gait = buf[o++] / 100f;
            z.hp01 = buf[o++] / 255f;
            z.maxHp = R16(buf, ref o);
            zs[i] = z;
        }
        return count;
    }

    static void U16(byte[] b, ref int o, ushort v) { b[o++] = (byte)v; b[o++] = (byte)(v >> 8); }
    static ushort R16(byte[] b, ref int o) { ushort v = (ushort)(b[o] | (b[o + 1] << 8)); o += 2; return v; }

    static void F32(byte[] b, ref int o, float f)
    {
        uint v = (uint)System.BitConverter.SingleToInt32Bits(f);
        b[o++] = (byte)v; b[o++] = (byte)(v >> 8); b[o++] = (byte)(v >> 16); b[o++] = (byte)(v >> 24);
    }

    static float RF32(byte[] b, ref int o)
    {
        int v = b[o] | (b[o + 1] << 8) | (b[o + 2] << 16) | (b[o + 3] << 24);
        o += 4;
        return System.BitConverter.Int32BitsToSingle(v);
    }
}
