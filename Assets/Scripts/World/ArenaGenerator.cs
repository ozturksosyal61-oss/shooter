using System.Collections.Generic;
using UnityEngine;

/// <summary>Scatters crates randomly inside the arena at startup.</summary>
public class ArenaGenerator : MonoBehaviour
{
    public GameObject cratePrefab;
    public int crateCount = 14;
    public float minSpacing = 3.4f;
    public float centerClearRadius = 4f;
    public float edgeMargin = 3f;

    void Awake()
    {
        var placed = new List<Vector3>(crateCount);
        for (int tries = 0; placed.Count < crateCount && tries < 500; tries++)
        {
            var p = new Vector3(
                Random.Range(-Arena.HalfX + edgeMargin, Arena.HalfX - edgeMargin), 0f,
                Random.Range(-Arena.HalfZ + edgeMargin, Arena.HalfZ - edgeMargin));
            if (p.magnitude < centerClearRadius) continue;
            bool ok = true;
            foreach (var q in placed)
                if ((q - p).sqrMagnitude < minSpacing * minSpacing) { ok = false; break; }
            if (!ok) continue;

            placed.Add(p);
            float yaw = Random.Range(0, 4) * 90f + Random.Range(-12f, 12f);
            var c = Instantiate(cratePrefab, p, Quaternion.Euler(0f, yaw, 0f), transform);
            c.transform.localScale = new Vector3(Random.Range(1f, 1.5f), 1f, Random.Range(1f, 1.6f));
        }
        Physics.SyncTransforms();
    }
}
