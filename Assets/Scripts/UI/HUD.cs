using UnityEngine;
using UnityEngine.UI;

public class HUD : MonoBehaviour
{
    public Text scoreText;
    public Text multiplierText;
    public Text comboText;
    public RectTransform comboRoot;
    public RectTransform comboBar;
    public RectTransform healthFill;
    public Image healthFillImage;
    public Image damageVignette;
    public Text fpsText;
    public Text ammoText;
    public GameObject reloadRoot;
    public RectTransform reloadBar;
    public GameObject gameOverPanel;
    public Text gameOverScoreText;

    int shownScore = -1, lastCombo = -1, lastMult = -1, lastFps = -1, lastAmmo = -1;
    float ammoPunch;
    string[] ammoStrings;
    float displayScore, healthShown = 1f, vignette, comboPunch;
    float fpsTimer;
    int fpsFrames;

    void Start()
    {
        if (gameOverPanel) gameOverPanel.SetActive(false);
    }

    void Update()
    {
        var gm = GameManager.I;
        if (!gm) return;
        float dt = Time.unscaledDeltaTime;

        // Score (counts up)
        displayScore = Mathf.MoveTowards(displayScore, gm.Score, Mathf.Max(400f, (gm.Score - displayScore) * 8f) * dt);
        int s = Mathf.RoundToInt(displayScore);
        if (s != shownScore) { shownScore = s; scoreText.text = s.ToString(); }

        // Combo
        int combo = gm.Combo, mult = gm.Multiplier;
        if (combo != lastCombo)
        {
            if (combo > lastCombo) comboPunch = 1f;
            lastCombo = combo;
            comboRoot.gameObject.SetActive(combo > 0);
            if (combo > 0) comboText.text = combo + " KOMBO";
        }
        if (mult != lastMult)
        {
            lastMult = mult;
            multiplierText.text = "x" + mult;
            comboPunch = 1.6f;
        }
        if (combo > 0)
        {
            comboPunch = Mathf.MoveTowards(comboPunch, 0f, dt * 6f);
            comboRoot.localScale = Vector3.one * (1f + 0.25f * comboPunch);
            comboBar.anchorMax = new Vector2(Mathf.Clamp01(gm.ComboTimeLeft / GameManager.ComboWindow), 1f);
        }

        // Health
        var p = gm.player;
        float h = p ? p.Health / p.maxHealth : 0f;
        healthShown = Mathf.MoveTowards(healthShown, h, dt * 2f);
        healthFill.anchorMax = new Vector2(healthShown, 1f);
        healthFillImage.color = Color.Lerp(new Color(0.9f, 0.2f, 0.15f), new Color(0.3f, 0.9f, 0.3f), healthShown);

        // Ammo + reload
        var w = p ? p.weapon : null;
        if (w && ammoText)
        {
            if (w.Ammo != lastAmmo)
            {
                if (w.Ammo > lastAmmo) ammoPunch = 1f;
                lastAmmo = w.Ammo;
                if (ammoStrings == null || ammoStrings.Length != w.magazineSize + 1)
                {
                    ammoStrings = new string[w.magazineSize + 1];
                    for (int i = 0; i <= w.magazineSize; i++) ammoStrings[i] = i + "<size=30> / " + w.magazineSize + "</size>";
                }
                ammoText.text = ammoStrings[Mathf.Clamp(w.Ammo, 0, w.magazineSize)];
                ammoText.color = w.Ammo <= w.magazineSize / 5 ? new Color(1f, 0.35f, 0.3f) : Color.white;
            }
            ammoPunch = Mathf.MoveTowards(ammoPunch, 0f, dt * 4f);
            ammoText.transform.localScale = Vector3.one * (1f + 0.25f * ammoPunch);
            reloadRoot.SetActive(w.IsReloading);
            if (w.IsReloading) reloadBar.anchorMax = new Vector2(w.ReloadProgress, 1f);
        }

        // Damage vignette
        vignette = Mathf.MoveTowards(vignette, 0f, dt * 1.6f);
        var vc = damageVignette.color; vc.a = vignette; damageVignette.color = vc;

        // FPS
        fpsFrames++;
        fpsTimer += dt;
        if (fpsTimer >= 0.5f)
        {
            int fps = Mathf.RoundToInt(fpsFrames / fpsTimer);
            fpsFrames = 0; fpsTimer = 0f;
            if (fps != lastFps) { lastFps = fps; fpsText.text = fps + " FPS"; }
        }
    }

    public void FlashDamage() => vignette = 0.75f;

    public void ShowGameOver(int score, int best)
    {
        gameOverPanel.SetActive(true);
        gameOverScoreText.text = "SKOR  " + score + "\nEN İYİ  " + best + "\n\n<size=34>Yeniden başlamak için dokun</size>";
    }
}
