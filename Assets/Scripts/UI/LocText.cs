using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Static UI label whose text comes from the <see cref="Loc"/> table (refreshes on language change).</summary>
[RequireComponent(typeof(TMP_Text))]
public class LocText : MonoBehaviour
{
    public string key;

    void OnEnable()
    {
        Apply();
        Loc.Changed += Apply;
    }

    void OnDisable() => Loc.Changed -= Apply;

    public void Apply()
    {
        if (!string.IsNullOrEmpty(key)) GetComponent<TMP_Text>().text = Loc.T(key);
    }
}
