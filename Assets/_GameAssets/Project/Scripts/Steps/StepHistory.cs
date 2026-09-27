using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

/// <summary>
/// Timestamped step history kept by the OS: "how many steps between A and B", including time the game wasn't running.
///   Android: Recording API (RecordingApiManager), minute buckets, ~10 days.
///   iOS:     CMPedometer.queryPedometerData (Plugins/iOS/WalkappPedometer.mm), ~7 days.
///   Editor:  a simulated log fed by StepManager's debug walking, so express jobs can be tested without a phone.
/// Callbacks always run on the Unity main thread. A null result means "unknown" (unsupported, denied or failed),
/// which is different from a real 0.
/// </summary>
public static class StepHistory
{
    private static SynchronizationContext mainThread;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void CaptureMainThread() => mainThread = SynchronizationContext.Current;

    public static bool IsSupported
    {
        get
        {
#if UNITY_EDITOR
            return EditorSimulateHistory;
#elif UNITY_ANDROID
            return RecordingApiManager.instance.IsAvailable;
#elif UNITY_IOS
            return IOSPedometer.IsAvailable;
#else
            return false;
#endif
        }
    }

    public static void QuerySteps(DateTime fromUtc, DateTime toUtc, Action<long?> onResult)
    {
        if (toUtc <= fromUtc)
        {
            onResult?.Invoke(0);
            return;
        }

        Action<long?> deliver = result => RunOnMainThread(() => onResult?.Invoke(result));
#if UNITY_EDITOR
        deliver(EditorSimulateHistory ? EditorSum(fromUtc, toUtc) : (long?)null);
#elif UNITY_ANDROID
        RecordingApiManager.instance.QueryTotalSteps(GameClock.ToUnix(fromUtc), GameClock.ToUnix(toUtc), deliver);
#elif UNITY_IOS
        IOSPedometer.Query(fromUtc, toUtc, deliver);
#else
        deliver(null);
#endif
    }

    internal static void RunOnMainThread(Action action)
    {
        if (mainThread != null && SynchronizationContext.Current != mainThread)
            mainThread.Post(_ => action(), null);
        else
            action();
    }

    // ---------------------------------------------------------------- Editor simulation

    /// <summary>Editor only: when false, gameplay behaves like a device without step history (fallback path).</summary>
    public static bool EditorSimulateHistory = true;

    private struct Segment
    {
        public DateTime from, to;
        public long steps;
    }

    private static readonly List<Segment> editorLog = new List<Segment>();

    /// <summary>Editor only: pretend the OS recorded <paramref name="steps"/> spread evenly over [from, to].</summary>
    public static void EditorRecord(DateTime fromUtc, DateTime toUtc, long steps)
    {
        if (steps <= 0) return;
        if (toUtc <= fromUtc) fromUtc = toUtc.AddSeconds(-1);
        editorLog.Add(new Segment { from = fromUtc, to = toUtc, steps = steps });
    }

    private static long EditorSum(DateTime fromUtc, DateTime toUtc)
    {
        double total = 0;
        foreach (var s in editorLog)
        {
            var start = s.from > fromUtc ? s.from : fromUtc;
            var end = s.to < toUtc ? s.to : toUtc;
            if (end <= start) continue;
            total += s.steps * (end - start).TotalSeconds / (s.to - s.from).TotalSeconds;
        }

        return (long)Math.Round(total);
    }

    // ---------------------------------------------------------------- iOS bridge

#if UNITY_IOS && !UNITY_EDITOR
    private static class IOSPedometer
    {
        private delegate void QueryCallback(int requestId, long steps, int ok);

        [DllImport("__Internal")] private static extern int _WalkappPedometerIsAvailable();
        [DllImport("__Internal")] private static extern void _WalkappPedometerQuery(double fromUnix, double toUnix, int requestId, QueryCallback callback);

        private static readonly Dictionary<int, Action<long?>> pending = new Dictionary<int, Action<long?>>();
        private static int nextRequestId;
        private static int available = -1;

        public static bool IsAvailable
        {
            get
            {
                if (available < 0) available = _WalkappPedometerIsAvailable();
                return available == 1;
            }
        }

        public static void Query(DateTime fromUtc, DateTime toUtc, Action<long?> onResult)
        {
            int id = ++nextRequestId;
            pending[id] = onResult;
            _WalkappPedometerQuery(ToUnixDouble(fromUtc), ToUnixDouble(toUtc), id, OnQueryResult);
        }

        [AOT.MonoPInvokeCallback(typeof(QueryCallback))]
        private static void OnQueryResult(int requestId, long steps, int ok)
        {
            if (pending.Remove(requestId, out var callback))
                callback(ok != 0 ? steps : (long?)null);
        }

        private static double ToUnixDouble(DateTime utc) =>
            (DateTime.SpecifyKind(utc, DateTimeKind.Utc) - DateTime.UnixEpoch).TotalSeconds;
    }
#endif
}
