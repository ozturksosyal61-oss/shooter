using System;
using UnityEngine;

/// <summary>
/// Co-op team rules (C3), on every game for its own soldier: going down instead of dying (a teammate standing next to
/// you for <see cref="reviveTime"/> s brings you back with half health; alone you bleed out in <see cref="bleedOut"/> s and
/// watch a teammate until the next wave break), the team score, the host's "nobody is standing → run over" check,
/// the wave-break respawn and the toasts (teammate left, connection lost). Pure rules are static (<see cref="Rules"/>).
/// State lives here; <see cref="CoopHUD"/> only reads it; the owner's <see cref="NetPlayer"/> publishes it.
/// </summary>
public class CoopTeam : MonoBehaviour
{
    public static CoopTeam I { get; private set; }

    public float bleedOut = 30f;
    public float reviveTime = 3f;
    public float reviveRadius = 2.4f;
    [Range(0.1f, 1f)] public float reviveHealth = 0.5f;

    /// <summary>Down and revivable.</summary>
    public bool Downed { get; private set; }
    /// <summary>Bled out: watching a teammate until the wave break (or the end).</summary>
    public bool Out { get; private set; }
    public float Revive01 { get; private set; }
    public float Bleed01 => Downed ? Mathf.Clamp01(bleedT / Mathf.Max(0.01f, bleedOut)) : 0f;
    public float BleedLeft => Downed ? Mathf.Max(0f, bleedT) : 0f;
    /// <summary>The downed teammate I'm standing next to (their revive progress is on their state).</summary>
    public NetPlayer Helping { get; private set; }
    public Transform Spectating { get; private set; }
    public int Revives { get; private set; }
    public bool TeamIsOver { get; private set; }

    /// <summary>HUD toasts: a teammate left, revived, connection lost…</summary>
    public event Action<string> Toast;

    float bleedT, overT;

    /// <summary>Pure co-op rules (EditMode-tested).</summary>
    public static class Rules
    {
        /// <summary>Revive progress step: up while a teammate helps, slowly back down otherwise.</summary>
        public static float StepRevive(float progress, bool helped, float dt, float reviveTime) =>
            Mathf.Clamp01(progress + (helped ? dt / reviveTime : -0.5f * dt / reviveTime));

        /// <summary>The bleed-out clock pauses while someone is reviving.</summary>
        public static float StepBleed(float left, bool helped, float dt) => helped ? left : left - dt;

        /// <summary>The run ends when nobody is standing (downed and bled-out soldiers don't count).</summary>
        public static bool TeamDown(bool localDead, bool[] othersDead, int others)
        {
            if (!localDead) return false;
            for (int i = 0; i < others; i++) if (!othersDead[i]) return false;
            return true;
        }

        /// <summary>Team score = every soldier's own score (bonuses are on the host's).</summary>
        public static int TeamScore(int local, int[] others, int count)
        {
            int s = local;
            for (int i = 0; i < count; i++) s += others[i];
            return s;
        }
    }

    readonly bool[] othersDead = new bool[8];
    readonly int[] othersScore = new int[8];

    void Awake() => I = this;

    void Start()
    {
        if (!GameMode.IsCoop) { enabled = false; return; }
        if (GameManager.I) GameManager.I.LocalDowned += OnDowned;
        NetPlayer.Left += OnLeft;
        CoopSession.Disconnected += OnDisconnected;
    }

    void OnDestroy()
    {
        if (GameManager.I) GameManager.I.LocalDowned -= OnDowned;
        NetPlayer.Left -= OnLeft;
        CoopSession.Disconnected -= OnDisconnected;
        if (I == this) I = null;
    }

    public static string NameOf(NetPlayer p) =>
        p == null ? "" : p == NetPlayer.Local ? Loc.T("coop.you") : Loc.F("coop.player", p.Slot + 1);

    /// <summary>Everyone's score added up (shown instead of the solo score in co-op).</summary>
    public int TeamScore
    {
        get
        {
            var gm = GameManager.I;
            int n = 0;
            foreach (var p in NetPlayer.All)
                if (p != NetPlayer.Local && n < othersScore.Length) othersScore[n++] = p.State.Value.score;
            return Rules.TeamScore(gm ? gm.Score : 0, othersScore, n);
        }
    }

    void OnDowned()
    {
        if (TeamIsOver) return;
        Downed = true;
        Out = false;
        bleedT = bleedOut;
        Revive01 = 0f;
        Toast?.Invoke(Loc.T("coop.downed_toast"));
    }

    void Update()
    {
        var gm = GameManager.I;
        if (!gm || !gm.player || TeamIsOver) return;
        var p = gm.player;
        float dt = Time.deltaTime;
        Vector3 me = p.transform.position;

        if (Downed)
        {
            bool helped = false;
            foreach (var np in NetPlayer.All)
            {
                if (np == NetPlayer.Local || np.State.Value.Dead) continue;
                Vector3 d = np.State.Value.pos - me; d.y = 0f;
                if (d.sqrMagnitude < reviveRadius * reviveRadius) { helped = true; break; }
            }
            Revive01 = Rules.StepRevive(Revive01, helped, dt, reviveTime);
            bleedT = Rules.StepBleed(bleedT, helped, dt);
            if (Revive01 >= 1f) StandUp(Loc.T("coop.revived"));
            else if (bleedT <= 0f) { Downed = false; Out = true; Revive01 = 0f; Toast?.Invoke(Loc.T("coop.out_toast")); }
        }

        // Am I reviving someone? (their game counts; this is for my HUD.)
        Helping = null;
        if (!p.IsDead)
            foreach (var np in NetPlayer.All)
            {
                if (np == NetPlayer.Local || !np.State.Value.Downed) continue;
                Vector3 d = np.State.Value.pos - me; d.y = 0f;
                if (d.sqrMagnitude < reviveRadius * reviveRadius) { Helping = np; break; }
            }

        UpdateSpectate(gm);

        // The host (or an offline test) ends the run when nobody stands.
        if (!CoopNet.IsClient)
        {
            int n = 0;
            foreach (var np in NetPlayer.All)
                if (np != NetPlayer.Local && n < othersDead.Length) othersDead[n++] = np.State.Value.Dead;
            if (Rules.TeamDown(p.IsDead, othersDead, n))
            {
                overT += Time.unscaledDeltaTime;
                if (overT > 0.6f)
                {
                    if (CoopNet.I) CoopNet.I.SendTeamOver();
                    TeamOver();
                }
            }
            else overT = 0f;
        }
    }

    void UpdateSpectate(GameManager gm)
    {
        var rig = gm.cameraRig;
        if (!rig) return;
        Transform want = gm.player.transform;
        if (Out)
        {
            // Watch the nearest teammate still standing.
            float best = float.MaxValue;
            foreach (var np in NetPlayer.All)
            {
                if (np == NetPlayer.Local || np.State.Value.Dead || !np.Avatar) continue;
                float d = (np.Avatar.position - gm.player.transform.position).sqrMagnitude;
                if (d < best) { best = d; want = np.Avatar; }
            }
        }
        Spectating = want != gm.player.transform ? want : null;
        if (rig.target != want) rig.target = want;
    }

    void StandUp(string toast)
    {
        var p = GameManager.I.player;
        Downed = false;
        Out = false;
        Revive01 = 0f;
        p.Revive(reviveHealth);
        Revives++;
        GameAudio.Play(SfxId.LevelUp, p.transform.position);
        if (Fx.I) Fx.I.Dust(p.transform.position);
        if (!string.IsNullOrEmpty(toast)) Toast?.Invoke(toast);
    }

    /// <summary>Wave break: the fallen come back next to a teammate.</summary>
    public void Respawn()
    {
        var gm = GameManager.I;
        if (!gm || !gm.player || TeamIsOver || !gm.player.IsDead) return;
        foreach (var np in NetPlayer.All)
        {
            if (np == NetPlayer.Local || np.State.Value.Dead) continue;
            gm.player.Warp(np.State.Value.pos + new Vector3(1.5f, 0f, -1f));
            break;
        }
        StandUp(Loc.T("coop.respawn"));
    }

    /// <summary>Everyone is down (host's call): the run ends here too.</summary>
    public void TeamOver()
    {
        if (TeamIsOver) return;
        TeamIsOver = true;
        Downed = false;
        var gm = GameManager.I;
        if (!gm) return;
        if (gm.cameraRig && gm.player) gm.cameraRig.target = gm.player.transform;
        gm.TriggerGameOver();
    }

    void OnLeft(NetPlayer p) => Toast?.Invoke(Loc.F("coop.left", NameOf(p)));

    void OnDisconnected()
    {
        Toast?.Invoke(Loc.T("coop.lost"));
        TeamOver();
    }
}
