using UnityEngine;

/// <summary>
/// 1.1.0 weather visuals (cosmetic, reads <see cref="DistrictAtmosphere.Sky"/>): at dusk / night a warm flashlight
/// cone on the ground ahead of the soldier + a soft pool of light around them (additive quads — no real-time light,
/// no extra heat), and rain: a world-space streak particle system that follows the soldier + a rain loop.
/// </summary>
public class WeatherFx : MonoBehaviour
{
    public Material flashlightMat, glowMat, rainMat;
    public AudioClip rainLoop;
    public float rainRate = 420f;
    [Range(0f, 1f)] public float rainVolume = 0.35f;

    Transform cone, glow;
    Renderer coneR, glowR;
    ParticleSystem rain;
    AudioSource rainAudio;
    MaterialPropertyBlock mpb;
    float night, wet;
    static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

    public float Night => night;
    public bool Raining => wet > 0.5f;

    void Start()
    {
        mpb = new MaterialPropertyBlock();
        cone = MakeQuad("FlashlightCone", flashlightMat, out coneR);
        glow = MakeQuad("NightGlow", glowMat, out glowR);
        rain = MakeRain();
        if (rainLoop)
        {
            rainAudio = gameObject.AddComponent<AudioSource>();
            rainAudio.clip = rainLoop; rainAudio.loop = true; rainAudio.playOnAwake = false; rainAudio.spatialBlend = 0f; rainAudio.volume = 0f;
            if (GameAudio.I && GameAudio.I.mixer)
            {
                var g = GameAudio.I.mixer.FindMatchingGroups("SFX");
                if (g.Length > 0) rainAudio.outputAudioMixerGroup = g[0];
            }
        }
    }

    Transform MakeQuad(string name, Material m, out Renderer r)
    {
        var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Destroy(q.GetComponent<Collider>());
        q.name = name;
        q.transform.SetParent(transform, false);
        r = q.GetComponent<Renderer>();
        r.sharedMaterial = m;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        q.SetActive(false);
        return q.transform;
    }

    ParticleSystem MakeRain()
    {
        var go = new GameObject("Rain");
        go.transform.SetParent(transform, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.loop = true; main.playOnAwake = false;
        main.startLifetime = 0.9f; main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.08f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 600;
        main.startColor = new Color(0.75f, 0.85f, 1f, 0.55f);
        var vel = ps.velocityOverLifetime;
        vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World;
        vel.x = new ParticleSystem.MinMaxCurve(-2.5f, -2.5f); vel.y = new ParticleSystem.MinMaxCurve(-22f, -22f); vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(34f, 1f, 26f);
        var em = ps.emission; em.rateOverTime = 0f;
        var pr = go.GetComponent<ParticleSystemRenderer>();
        pr.sharedMaterial = rainMat;
        pr.renderMode = ParticleSystemRenderMode.Stretch;
        pr.velocityScale = 0.045f; pr.lengthScale = 1f;
        pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; pr.receiveShadows = false;
        ps.Play();
        return ps;
    }

    void LateUpdate()
    {
        var atm = DistrictAtmosphere.I;
        var gm = GameManager.I;
        var p = gm && gm.player ? gm.player.transform : null;
        if (!atm || !p) return;
        var sky = atm.Sky;
        float dt = Time.unscaledDeltaTime;
        float nightT = sky.phase == DayPhase.Night ? 1f : sky.phase == DayPhase.Dusk ? 0.35f : 0f;
        night = Mathf.MoveTowards(night, nightT, dt * 0.35f);
        wet = Mathf.MoveTowards(wet, sky.rain ? 1f : 0f, dt * 0.4f);

        // Flashlight: a cone on the ground ahead of the soldier, a soft pool around them.
        bool lit = night > 0.02f && !gm.player.IsDead;
        cone.gameObject.SetActive(lit); glow.gameObject.SetActive(lit);
        if (lit)
        {
            Vector3 fwd = p.forward; fwd.y = 0f; fwd = fwd.sqrMagnitude > 0.01f ? fwd.normalized : Vector3.forward;
            cone.SetPositionAndRotation(p.position + fwd * 3.4f + Vector3.up * 0.04f, Quaternion.LookRotation(Vector3.down, fwd));
            cone.localScale = new Vector3(3.6f, 6.4f, 1f);
            glow.SetPositionAndRotation(p.position + Vector3.up * 0.035f, Quaternion.LookRotation(Vector3.down, Vector3.forward));
            glow.localScale = Vector3.one * 7f;
            Tint(coneR, new Color(1f, 0.92f, 0.7f, 0.55f * night));
            Tint(glowR, new Color(1f, 0.9f, 0.7f, 0.35f * night));
        }

        // Rain follows the soldier (spawn box above them; the streaks are in world space).
        if (rain)
        {
            rain.transform.position = p.position + new Vector3(2f, 13f, 1f);
            var em = rain.emission;
            em.rateOverTime = rainRate * wet * Settings.ParticleScale;
        }
        if (rainAudio)
        {
            rainAudio.volume = rainVolume * wet;
            if (wet > 0.01f && !rainAudio.isPlaying) rainAudio.Play();
            else if (wet <= 0.01f && rainAudio.isPlaying) rainAudio.Stop();
        }
    }

    void Tint(Renderer r, Color c)
    {
        r.GetPropertyBlock(mpb);
        mpb.SetColor(BaseColor, c);
        r.SetPropertyBlock(mpb);
    }
}
