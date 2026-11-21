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
    /// <summary>Special-zombie phase (bomb swelling, tongue aim / out / hold…), 0 = nothing special.</summary>
    public byte act;
    /// <summary>Tongue: target player slot (255 = none). Boss: <see cref="BossType"/>.</summary>
    public byte aux;
}

/// <summary>Wave state riding on every snapshot (clients don't run their own waves).</summary>
public struct WaveNetState
{
    public int wave;
    public bool inBreak;
    public float timeLeft;
}

/// <summary>The boss's fight state (C2): phase + the numbers its telegraphs are drawn from.</summary>
public struct BossNetState
{
    public ushort id;
    public byte phase;            // ButcherZombie.Phase
    public float phaseT;
    public float chargeYaw, chargeLen, windTime;
    public Vector3 leapTo, shockPos;
    public float shockT;
    public bool enraged;

    public bool Equals(BossNetState o) => id == o.id && phase == o.phase;
}

/// <summary>
/// Pure, allocation-free snapshot codec (EditMode-tested). Layout: header (wave u16, flags u8, timeLeft u16 ×0.1 s,
/// count u16) then per zombie: id u16, kind/flags u8, x f32, z f32, yaw u8, gait u8, hp u8, maxHp u16, act u8, aux u8
/// = 18 bytes; then a boss flag u8 + optional boss block (26 bytes).
/// <see cref="NetZombies"/> sends it unreliable at 15 Hz, split into parts of ≤ PerPart zombies (each under the MTU).
/// </summary>
public static class ZombieSnapshot
{
    public const int HeaderBytes = 7;
    public const int ZombieBytes = 18;
    public const int BossBytes = 27; // flag + 26
    public static int Size(int count) => HeaderBytes + count * ZombieBytes + BossBytes;

    public static int Write(byte[] buf, WaveNetState w, ZombieNetState[] zs, int count) => Write(buf, w, zs, count, null);

    public static int Write(byte[] buf, WaveNetState w, ZombieNetState[] zs, int count, BossNetState? boss) => Write(buf, w, zs, 0, count, boss);

    /// <summary>Writes zombies <paramref name="start"/> .. start+count (one part of a split snapshot).</summary>
    public static int Write(byte[] buf, WaveNetState w, ZombieNetState[] zs, int start, int count, BossNetState? boss)
    {
        int o = 0;
        U16(buf, ref o, (ushort)Mathf.Clamp(w.wave, 0, ushort.MaxValue));
        buf[o++] = (byte)(w.inBreak ? 1 : 0);
        U16(buf, ref o, (ushort)Mathf.Clamp(Mathf.RoundToInt(w.timeLeft * 10f), 0, ushort.MaxValue));
        U16(buf, ref o, (ushort)count);
        for (int i = 0; i < count; i++)
        {
            var z = zs[start + i];
            U16(buf, ref o, z.id);
            buf[o++] = (byte)(((int)z.kind & 0x0f) | (z.runner ? 0x10 : 0) | (z.rising ? 0x20 : 0));
            F32(buf, ref o, z.pos.x);
            F32(buf, ref o, z.pos.z);
            buf[o++] = Yaw(z.yaw);
            buf[o++] = (byte)Mathf.Clamp(Mathf.RoundToInt(z.gait * 100f), 0, 255);
            buf[o++] = (byte)Mathf.Clamp(Mathf.RoundToInt(z.hp01 * 255f), 0, 255);
            U16(buf, ref o, (ushort)Mathf.Clamp(Mathf.RoundToInt(z.maxHp), 0, ushort.MaxValue));
            buf[o++] = z.act;
            buf[o++] = z.aux;
        }
        buf[o++] = (byte)(boss.HasValue ? 1 : 0);
        if (boss.HasValue)
        {
            var b = boss.Value;
            U16(buf, ref o, b.id);
            buf[o++] = b.phase;
            U16(buf, ref o, (ushort)Mathf.Clamp(Mathf.RoundToInt(b.phaseT * 100f), 0, ushort.MaxValue));
            buf[o++] = Yaw(b.chargeYaw);
            buf[o++] = (byte)Mathf.Clamp(Mathf.RoundToInt(b.chargeLen * 5f), 0, 255);
            buf[o++] = (byte)Mathf.Clamp(Mathf.RoundToInt(b.windTime * 50f), 0, 255);
            F32(buf, ref o, b.leapTo.x); F32(buf, ref o, b.leapTo.z);
            F32(buf, ref o, b.shockPos.x); F32(buf, ref o, b.shockPos.z);
            buf[o++] = (byte)Mathf.Clamp(Mathf.RoundToInt(b.shockT * 50f), 0, 255);
            buf[o++] = (byte)(b.enraged ? 1 : 0);
        }
        return o;
    }

    public static int Read(byte[] buf, int len, out WaveNetState w, ref ZombieNetState[] zs) => Read(buf, len, out w, ref zs, out _);

    /// <summary>Reads into <paramref name="zs"/> (grown if needed); returns the zombie count, -1 on a malformed buffer.</summary>
    public static int Read(byte[] buf, int len, out WaveNetState w, ref ZombieNetState[] zs, out BossNetState? boss)
    {
        w = default;
        boss = null;
        if (len < HeaderBytes) return -1;
        int o = 0;
        w.wave = R16(buf, ref o);
        w.inBreak = buf[o++] != 0;
        w.timeLeft = R16(buf, ref o) / 10f;
        int count = R16(buf, ref o);
        if (len < HeaderBytes + count * ZombieBytes + 1) return -1;
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
            z.act = buf[o++];
            z.aux = buf[o++];
            zs[i] = z;
        }
        bool hasBoss = buf[o++] != 0;
        if (hasBoss)
        {
            if (len < o + BossBytes - 1) return -1;
            BossNetState b;
            b.id = R16(buf, ref o);
            b.phase = buf[o++];
            b.phaseT = R16(buf, ref o) / 100f;
            b.chargeYaw = buf[o++] / 255f * 360f;
            b.chargeLen = buf[o++] / 5f;
            b.windTime = buf[o++] / 50f;
            float lx = RF32(buf, ref o), lz = RF32(buf, ref o);
            float sx = RF32(buf, ref o), sz = RF32(buf, ref o);
            b.leapTo = new Vector3(lx, 0f, lz);
            b.shockPos = new Vector3(sx, 0f, sz);
            b.shockT = buf[o++] / 50f;
            b.enraged = buf[o++] != 0;
            boss = b;
        }
        return count;
    }

    static byte Yaw(float deg) => (byte)Mathf.RoundToInt(Mathf.Repeat(deg, 360f) / 360f * 255f);

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
