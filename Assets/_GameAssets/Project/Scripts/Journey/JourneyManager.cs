using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// The road trip: every walked step (live and while away, never banked steps) moves the player along the current
/// route. Each stop reached gives a postcard (a short fact about the place) and coins; the last stop gives a route
/// bonus and the next route begins with the steps left over. Rewards and their popups wait until the journey is
/// unlocked (with the Stats tab), so a new player gets a burst of postcards the first time they see it.
/// </summary>
public class JourneyManager : MonoSingleton<JourneyManager>
{
    [SerializeField] private JourneyData data;

    [Serializable]
    private class SaveData
    {
        public int routeIndex;
        public long stepsOnRoute;
        public int reached;                                        // stops reached on the current route
        public List<string> postcards = new List<string>();        // "routeId:stopIndex", in the order collected
        public List<string> pending = new List<string>();          // collected but not shown (or paid) yet
        public List<string> pendingRoutes = new List<string>();    // finished routes whose bonus wasn't paid yet
        public long extraSteps;                                    // walked after the last route (more routes coming)
    }

    private const string SaveKey = "JourneyState";
    private SaveData save;

    public event Action OnChanged;

    public JourneyData Data => data;
    public bool Unlocked => ProgressionManager.instance.IsUnlocked(Feature.DailyStars);
    public bool AllRoutesDone => data == null || save.routeIndex >= data.routes.Length;
    public JourneyData.Route Route => AllRoutesDone ? null : data.routes[save.routeIndex];
    public int RouteIndex => save.routeIndex;
    public long StepsOnRoute => save.stepsOnRoute;
    public int StopsReached => save.reached;
    public IReadOnlyList<string> Postcards => save.postcards;
    public int TotalStops
    {
        get
        {
            int n = 0;
            if (data != null) foreach (var r in data.routes) n += r.stops.Length;
            return n;
        }
    }

    /// <summary>The next stop on the current route, or null at the end.</summary>
    public JourneyData.Stop NextStop => Route != null && save.reached < Route.stops.Length ? Route.stops[save.reached] : null;
    public long StepsToNextStop => NextStop == null ? 0 : Math.Max(0, Route.StepsTo(save.reached) - save.stepsOnRoute);
    public float RouteProgress01 => Route == null ? 1f : Mathf.Clamp01(save.stepsOnRoute / (float)Route.stepsToComplete);

    public override void Init()
    {
        if (data == null) data = Resources.Load<JourneyData>("Journey");
        save = null;
        if (PlayerPrefs.HasKey(SaveKey))
        {
            try { save = JsonUtility.FromJson<SaveData>(PlayerPrefs.GetString(SaveKey)); }
            catch (Exception e) { Debug.LogError("Journey: failed to load. " + e.Message); }
        }
        if (save == null) save = new SaveData();
        // The first stop of every route is its starting point: collected the moment the route begins.
        if (save.reached == 0 && Route != null) Reach(0);
        SaveSystem.OnSave += Write;
    }

    private void Start()
    {
        ProgressionManager.instance.OnUnlocksChanged += ShowPending;
    }

    /// <summary>Walked steps from the pedometer (StepManager, live and offline). Banked steps never count.</summary>
    public void OnWalked(long steps)
    {
        if (steps <= 0 || data == null) return;

        long left = steps;
        while (left > 0)
        {
            var route = Route;
            if (route == null)
            {
                save.extraSteps += left;
                break;
            }

            long toEnd = route.stepsToComplete - save.stepsOnRoute;
            long take = Math.Min(left, Math.Max(0, toEnd));
            save.stepsOnRoute += take;
            left -= take;

            while (save.reached < route.stops.Length && save.stepsOnRoute >= route.StepsTo(save.reached))
                Reach(save.reached);

            if (save.reached >= route.stops.Length)
            {
                // Route done: bonus, then the next route starts with the steps left over.
                save.pendingRoutes.Add(route.id);
                save.routeIndex++;
                save.stepsOnRoute = 0;
                save.reached = 0;
                if (Route != null) Reach(0);
            }
            else if (take == 0) break;
        }

        Write();
        SaveSystem.MarkDirty();
        OnChanged?.Invoke();
        ShowPending();
    }

    private void Reach(int stop)
    {
        string id = $"{Route.id}:{stop}";
        save.reached = stop + 1;
        if (save.postcards.Contains(id)) return;
        save.postcards.Add(id);
        save.pending.Add(id);
    }

    /// <summary>Pays and announces what was collected, once the journey is unlocked.</summary>
    public void ShowPending()
    {
        if (!Unlocked || data == null || (save.pending.Count == 0 && save.pendingRoutes.Count == 0)) return;

        long coins = 0;
        var names = new List<string>();
        JourneyData.Stop single = null;
        foreach (string id in save.pending)
        {
            var (route, index) = Find(id);
            if (route == null) continue;
            coins += StopReward(route, index);
            names.Add(route.stops[index].name);
            single = route.stops[index];
        }

        var finished = new List<string>();
        foreach (string routeId in save.pendingRoutes)
        {
            var route = Array.Find(data.routes, r => r.id == routeId);
            if (route == null) continue;
            coins += route.completionReward;
            finished.Add(route.name);
        }

        save.pending.Clear();
        save.pendingRoutes.Clear();
        if (coins > 0) CurrencyManager.instance.AddCurrency(CurrencyType.Coin, coins);
        Write();
        SaveSystem.SaveNow();
        OnChanged?.Invoke();

        var text = new StringBuilder();
        string title;
        if (finished.Count > 0)
        {
            title = "ROUTE COMPLETE!";
            text.Append($"You drove the whole of <b>{string.Join(", ", finished)}</b>.");
            if (names.Count > 0) text.Append($"\n\nNew postcards: {string.Join(", ", names)}.");
        }
        else if (names.Count == 1)
        {
            title = $"POSTCARD FROM {single.name.ToUpperInvariant()}";
            text.Append(single.fact);
        }
        else
        {
            title = $"{names.Count} NEW POSTCARDS";
            text.Append($"Your truck rolled through <b>{string.Join(", ", names)}</b>.");
        }
        text.Append($"\n\n<b>+<sprite=0>{coins:N0}</b>");
        if (NextStop != null) text.Append($"\n<size=85%>Next stop: {NextStop.name}, {StepsToNextStop:N0} steps away</size>");
        UIManager.instance.ShowMessage(title, text.ToString(), "NICE!");
    }

    public long StopReward(JourneyData.Route route, int index)
    {
        if (index == 0) return 0; // the starting point is free
        long leg = route.StepsTo(index) - route.StepsTo(index - 1);
        return (long)Math.Round(leg * data.coinsPerLegStep / 50.0) * 50;
    }

    public (JourneyData.Route route, int index) Find(string postcardId)
    {
        int colon = postcardId.LastIndexOf(':');
        if (colon < 0 || data == null) return (null, -1);
        var route = Array.Find(data.routes, r => r.id == postcardId.Substring(0, colon));
        if (route == null || !int.TryParse(postcardId.Substring(colon + 1), out int i) || i < 0 || i >= route.stops.Length)
            return (null, -1);
        return (route, i);
    }

    private void Write() => PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(save));

    // ---------------------------------------------------------------- debug (SRDebugger)

    public void DebugReset()
    {
        save = new SaveData();
        if (Route != null) Reach(0);
        Write();
        OnChanged?.Invoke();
    }
}
