using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;

/// <summary>Simple stack-based component pool. Objects are deactivated when released.</summary>
public class Pool<T> where T : Component
{
    readonly T prefab;
    readonly Transform parent;
    readonly Stack<T> free = new Stack<T>();
    static readonly ProfilerMarker GrowMarker = new ProfilerMarker(ProfilerCategory.Scripts, "Game.PoolGrow");

    public Pool(T prefab, Transform parent, int prewarm)
    {
        this.prefab = prefab;
        this.parent = parent;
        for (int i = 0; i < prewarm; i++)
        {
            var t = Object.Instantiate(prefab, parent);
            t.gameObject.SetActive(false);
            free.Push(t);
        }
    }

    public T Get()
    {
        T t;
        if (free.Count > 0) t = free.Pop();
        else using (GrowMarker.Auto()) t = Object.Instantiate(prefab, parent);
        t.gameObject.SetActive(true);
        return t;
    }

    public void Release(T t)
    {
        t.gameObject.SetActive(false);
        free.Push(t);
    }
}
