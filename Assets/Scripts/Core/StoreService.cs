using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Purchasing;

/// <summary>In-app purchases behind one interface: Unity IAP (App Store / Google Play) on devices, a fake store in the editor and tests.</summary>
public interface IStoreBackend
{
    bool Ready { get; }
    void Init();
    string PriceOf(string productId);
    void Buy(string productId, Action<bool> done);
    void Restore(Action<bool> done);
}

public static class Store
{
    static IStoreBackend backend;

    /// <summary>Force the fake store (editor default; tests).</summary>
    public static bool UseFake = Application.isEditor;

    public static IStoreBackend Backend
    {
        get
        {
            if (backend == null)
            {
                backend = UseFake ? new FakeStoreBackend() : new UnityIapBackend();
                backend.Init();
            }
            return backend;
        }
        set => backend = value;
    }

    public static bool Ready => Backend.Ready;
    public static string Price(string id) => Backend.PriceOf(id);

    /// <summary>Buys a product; delivery to <see cref="Profile"/> happens in the backend (also for purchases finished later).</summary>
    public static void Buy(string id, Action<bool> done) => Backend.Buy(id, ok => done?.Invoke(ok));

    public static void Restore(Action<bool> done) => Backend.Restore(done);
}

/// <summary>Editor / test store: every purchase succeeds after a short delay; prices from the catalogue.</summary>
public class FakeStoreBackend : IStoreBackend
{
    public bool Ready => true;
    public bool fail;
    int txn;

    public void Init() { }

    public string PriceOf(string id)
    {
        var p = Economy.Product(id);
        return p != null ? Economy.PriceLabel(p.Value) : "-";
    }

    public void Buy(string id, Action<bool> done)
    {
        ServiceRunner.Run(After(0.3f, () =>
        {
            bool ok = !fail && Profile.Deliver(id, "fake-" + (++txn) + "-" + DateTime.UtcNow.Ticks, false);
            done?.Invoke(ok);
        }));
    }

    public void Restore(Action<bool> done) => ServiceRunner.Run(After(0.3f, () => done?.Invoke(true)));

    static IEnumerator After(float s, Action a)
    {
        yield return new WaitForSecondsRealtime(s);
        a();
    }
}

/// <summary>Unity IAP 5 (StoreController): connect, fetch products, purchase → pending → deliver → confirm; restore.</summary>
public class UnityIapBackend : IStoreBackend
{
    StoreController controller;
    readonly Dictionary<string, Action<bool>> waiting = new Dictionary<string, Action<bool>>();
    bool ready;

    public bool Ready => ready;

    public async void Init()
    {
        try
        {
            await Unity.Services.Core.UnityServices.InitializeAsync();
            controller = UnityIAPServices.StoreController();
            controller.OnPurchasePending += OnPending;
            controller.OnPurchaseFailed += OnFailed;
            controller.OnProductsFetched += _ => { ready = true; controller.FetchPurchases(); };
            controller.OnProductsFetchFailed += f => Debug.LogWarning("IAP: products fetch failed " + f.FailureReason);
            controller.OnPurchasesFetched += OnPurchasesFetched;
            controller.OnStoreDisconnected += d => { ready = false; Debug.LogWarning("IAP: store disconnected"); };
            await controller.Connect();
            var defs = new List<ProductDefinition>();
            foreach (var p in Economy.Products)
                defs.Add(new ProductDefinition(p.id, p.kind == ProductKind.Consumable ? ProductType.Consumable : ProductType.NonConsumable));
            controller.FetchProducts(defs);
        }
        catch (Exception e) { Debug.LogWarning("IAP: init failed: " + e.Message); }
    }

    public string PriceOf(string id)
    {
        var p = controller?.GetProductById(id);
        if (p != null && p.metadata != null && !string.IsNullOrEmpty(p.metadata.localizedPriceString)) return p.metadata.localizedPriceString;
        var d = Economy.Product(id);
        return d != null ? Economy.PriceLabel(d.Value) : "-";
    }

    public void Buy(string id, Action<bool> done)
    {
        if (!ready || controller == null) { done?.Invoke(false); return; }
        waiting[id] = done;
        controller.PurchaseProduct(id);
    }

    void OnPending(PendingOrder order)
    {
        foreach (var item in order.CartOrdered.Items())
        {
            string id = item.Product.definition.id;
            Profile.Deliver(id, order.Info.TransactionID, false);
            Finish(id, true);
        }
        controller.ConfirmPurchase(order);
    }

    void OnFailed(FailedOrder order)
    {
        foreach (var item in order.CartOrdered.Items()) Finish(item.Product.definition.id, false);
        Debug.LogWarning("IAP: purchase failed " + order.FailureReason + " " + order.Details);
    }

    void OnPurchasesFetched(Orders orders)
    {
        // Restored / owned non-consumables (VIP, starter pack): entitlements only, never currency twice.
        foreach (var o in orders.ConfirmedOrders)
            foreach (var item in o.CartOrdered.Items())
            {
                var d = Economy.Product(item.Product.definition.id);
                if (d != null && d.Value.kind == ProductKind.NonConsumable) Profile.Deliver(d.Value.id, null, true);
            }
    }

    void Finish(string id, bool ok)
    {
        if (waiting.TryGetValue(id, out var cb)) { waiting.Remove(id); cb?.Invoke(ok); }
    }

    public void Restore(Action<bool> done)
    {
        if (controller == null) { done?.Invoke(false); return; }
        controller.RestoreTransactions((ok, err) =>
        {
            if (!ok) Debug.LogWarning("IAP: restore failed " + err);
            controller.FetchPurchases();
            done?.Invoke(ok);
        });
    }
}

/// <summary>Hidden runner for service coroutines (survives scene loads).</summary>
public class ServiceRunner : MonoBehaviour
{
    static ServiceRunner inst;

    public static void Run(IEnumerator co)
    {
        if (!inst)
        {
            var go = new GameObject("Services");
            DontDestroyOnLoad(go);
            inst = go.AddComponent<ServiceRunner>();
        }
        inst.StartCoroutine(co);
    }
}
