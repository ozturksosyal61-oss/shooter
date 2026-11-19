using TMPro;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Scene switch with a loading screen (Menu ↔ Game): the prefab (Resources/LoadingScreen, built by the builder)
/// survives the load, shows a random tip and a progress bar, stays up at least <see cref="minSeconds"/>, then fades.
/// </summary>
public class LoadingScreen : MonoBehaviour
{
    public const string MenuScene = "Menu", GameScene = "Game";
    public const int TipCount = 10;

    public CanvasGroup group;
    public TMP_Text tip;
    public RectTransform barFill;
    public float minSeconds = 1.1f;

    public static bool IsLoading { get; private set; }

    /// <summary>Loads <paramref name="scene"/> behind the loading screen (falls back to a plain load without the prefab).</summary>
    public static void Load(string scene)
    {
        if (IsLoading) return;
        Time.timeScale = GameManager.TimeScale;
        AudioListener.pause = false;
        var prefab = Resources.Load<LoadingScreen>("LoadingScreen");
        if (!prefab) { SceneManager.LoadScene(scene); return; }
        var ls = Instantiate(prefab);
        DontDestroyOnLoad(ls.gameObject);
        ls.StartCoroutine(ls.Run(scene));
    }

    IEnumerator Run(string scene)
    {
        IsLoading = true;
        tip.text = Loc.T("tip." + Random.Range(1, TipCount + 1));
        group.alpha = 0f;
        barFill.anchorMax = new Vector2(0f, 1f);
        float t0 = Time.unscaledTime;
        while (group.alpha < 1f) { group.alpha = Mathf.MoveTowards(group.alpha, 1f, Time.unscaledDeltaTime * 6f); yield return null; }
        var op = SceneManager.LoadSceneAsync(scene);
        op.allowSceneActivation = false;
        while (op.progress < 0.9f || Time.unscaledTime - t0 < minSeconds)
        {
            float shown = Mathf.Min(op.progress / 0.9f, (Time.unscaledTime - t0) / minSeconds);
            barFill.anchorMax = new Vector2(Mathf.Max(barFill.anchorMax.x, shown), 1f);
            yield return null;
        }
        barFill.anchorMax = Vector2.one;
        op.allowSceneActivation = true;
        while (!op.isDone) yield return null;
        yield return null;
        IsLoading = false;
        while (group.alpha > 0f) { group.alpha = Mathf.MoveTowards(group.alpha, 0f, Time.unscaledDeltaTime * 4f); yield return null; }
        Destroy(gameObject);
    }

    void OnDestroy() => IsLoading = false;
}
