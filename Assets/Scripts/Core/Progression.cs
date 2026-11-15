using System;
using UnityEngine;

/// <summary>
/// Run XP: every kill gives the zombie's <see cref="Zombie.xp"/> (specials give much more). Each level-up queues a
/// card pick (<see cref="CardPicker"/>), during which the game slows almost to a stop.
/// </summary>
public class Progression : MonoBehaviour
{
    public static Progression I { get; private set; }

    [Tooltip("XP for level L→L+1 = baseXp + linear·(L−1) + quadratic·(L−1)².")]
    public int baseXp = 10;
    public int linear = 6;
    public float quadratic = 1f;
    public int runnerXp = 2;

    public int Level { get; private set; } = 1;
    public int Xp { get; private set; }
    public int XpToNext => Need(Level);
    public float Progress01 => Mathf.Clamp01((float)Xp / XpToNext);
    public int PendingPicks { get; private set; }

    public event Action<int> LeveledUp;

    void Awake() => I = this;

    void Start()
    {
        if (GameManager.I) GameManager.I.Killed += OnKilled;
    }

    void OnDestroy()
    {
        if (GameManager.I) GameManager.I.Killed -= OnKilled;
    }

    public int Need(int level) => baseXp + linear * (level - 1) + Mathf.RoundToInt(quadratic * (level - 1) * (level - 1));

    void OnKilled(Zombie z) => AddXp(z.IsRunner ? runnerXp : z.xp);

    public void AddXp(int amount)
    {
        if (GameManager.I && GameManager.I.IsGameOver) return;
        Xp += amount;
        while (Xp >= XpToNext)
        {
            Xp -= XpToNext;
            Level++;
            PendingPicks++;
            LeveledUp?.Invoke(Level);
        }
    }

    /// <summary>Called by the card picker when a pick has been consumed.</summary>
    public void ConsumePick() => PendingPicks = Mathf.Max(0, PendingPicks - 1);
}
