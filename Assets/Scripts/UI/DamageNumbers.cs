using UnityEngine;
using UnityEngine.UI;

/// <summary>Pooled floating damage numbers drawn on the overlay canvas.</summary>
public class DamageNumbers : MonoBehaviour
{
    public static DamageNumbers I { get; private set; }

    public Text template;
    public int poolSize = 32;
    public float lifetime = 0.7f;
    public Color normalColor = Color.white;
    public Color critColor = new Color(1f, 0.85f, 0.15f);

    Text[] texts;
    Vector3[] world;
    Vector2[] drift;
    float[] born;
    bool[] crit;
    int next;
    Camera cam;
    Canvas canvas;
    static readonly string[] Cache = new string[1000];

    void Awake()
    {
        I = this;
        canvas = GetComponentInParent<Canvas>();
        texts = new Text[poolSize];
        world = new Vector3[poolSize];
        drift = new Vector2[poolSize];
        born = new float[poolSize];
        crit = new bool[poolSize];
        for (int i = 0; i < poolSize; i++)
        {
            texts[i] = Instantiate(template, transform);
            texts[i].gameObject.SetActive(false);
        }
        template.gameObject.SetActive(false);
    }

    void OnEnable() => FloatingOrigin.OnShift += OnShift;
    void OnDisable() => FloatingOrigin.OnShift -= OnShift;
    void OnShift(Vector3 d) { for (int i = 0; i < world.Length; i++) world[i] += d; }

    static string ToText(int v)
    {
        if (v < 0 || v >= Cache.Length) return v.ToString();
        return Cache[v] ??= v.ToString();
    }

    public void Show(Vector3 worldPos, int value, bool isCrit)
    {
        int i = next;
        next = (next + 1) % texts.Length;
        world[i] = worldPos;
        drift[i] = new Vector2(Random.Range(-40f, 40f), 0f);
        born[i] = Time.unscaledTime;
        crit[i] = isCrit;
        var t = texts[i];
        t.text = isCrit ? ToText(value) + "!" : ToText(value);
        t.color = isCrit ? critColor : normalColor;
        t.fontSize = isCrit ? 54 : 38;
        t.gameObject.SetActive(true);
        t.transform.SetAsLastSibling();
    }

    /// <summary>Free-form popup (e.g. "+25" health).</summary>
    public void ShowText(Vector3 worldPos, string text, Color color)
    {
        int i = next;
        next = (next + 1) % texts.Length;
        world[i] = worldPos;
        drift[i] = Vector2.zero;
        born[i] = Time.unscaledTime;
        crit[i] = true;
        var t = texts[i];
        t.text = text;
        t.color = color;
        t.fontSize = 50;
        t.gameObject.SetActive(true);
        t.transform.SetAsLastSibling();
    }

    void LateUpdate()
    {
        if (!cam) cam = Camera.main;
        float scale = canvas ? canvas.scaleFactor : 1f;
        float now = Time.unscaledTime;
        for (int i = 0; i < texts.Length; i++)
        {
            var t = texts[i];
            if (!t.gameObject.activeSelf) continue;
            float age = (now - born[i]) / lifetime;
            if (age >= 1f) { t.gameObject.SetActive(false); continue; }
            Vector3 sp = cam.WorldToScreenPoint(world[i]);
            if (sp.z < 0f) { t.gameObject.SetActive(false); continue; }
            float rise = (1f - (1f - age) * (1f - age)) * 90f;
            float pop = age < 0.15f ? Mathf.Lerp(1.6f, 1f, age / 0.15f) : 1f;
            ((RectTransform)t.transform).anchoredPosition = new Vector2(sp.x / scale + drift[i].x * age, sp.y / scale + 40f + rise);
            t.transform.localScale = Vector3.one * pop * (crit[i] ? 1.15f : 1f);
            var c = t.color; c.a = age < 0.6f ? 1f : 1f - (age - 0.6f) / 0.4f; t.color = c;
        }
    }
}
