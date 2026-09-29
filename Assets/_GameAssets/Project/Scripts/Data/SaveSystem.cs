using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Autosave. Anything that changes progress calls MarkDirty; a few seconds later every manager writes its state
/// together (OnSave) and PlayerPrefs is flushed to disk. Before this, coins, fuel and the bank were only written on
/// pause/quit, so a killed app (or a new build installed over a running one) lost everything since the last pause.
///
/// Saving all managers at once keeps them consistent with the step sync: the pedometer baseline is saved together
/// with the bank and jobs its steps paid for. It never saves while a step sync is crediting (StepsSettled), so a
/// crash can't keep the new baseline without the steps it credited.
/// </summary>
public static class SaveSystem
{
    /// <summary>Each manager writes its state into PlayerPrefs here (no PlayerPrefs.Save needed).</summary>
    public static event Action OnSave;

    private const float Delay = 3f;
    private static bool dirty;

    public static void MarkDirty() => dirty = true;

    /// <summary>Write everything now (pause, quit, or an important moment like a purchase).</summary>
    public static void SaveNow()
    {
        dirty = false;
        OnSave?.Invoke();
        PlayerPrefs.Save();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void StartRunner()
    {
        var go = new GameObject("SaveSystem");
        UnityEngine.Object.DontDestroyOnLoad(go);
        go.AddComponent<Runner>();
    }

    private class Runner : MonoBehaviour
    {
        private IEnumerator Start()
        {
            var wait = new WaitForSecondsRealtime(Delay);
            while (true)
            {
                yield return wait;
                if (!dirty) continue;

                var steps = StepManager.instance;
                if (steps == null || !steps.IsReady || !steps.StepsSettled) continue;
                SaveNow();
            }
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused) dirty = true; // the managers still save on pause themselves; this just flags it
        }
    }
}
