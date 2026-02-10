using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections;
using TMPro;

public class StepDisplayManager : MonoBehaviour
{
    [Header("UI References")]
    public TextMeshProUGUI todayStepsText;
    public TextMeshProUGUI totalStepsText;

    [Header("Dependencies")]
    // Assign your plugin wrapper here (or find it via Singleton)
    private AndroidJavaObject healthPlugin;

    // --- STATE VARIABLES ---
    
    // 1. The Anchors (Trusted Data from Health Connect)
    private long todayAnchor = 0;
    private long totalAnchor = 0;

    // 2. The Sensor Baseline (The sensor value at the moment we got the Anchors)
    private long sensorAtLastSync = 0;

    // 3. The Live Sensor (Raw hardware value)
    private long currentSensorVal = 0;

    // 4. The Ratchets (High-water marks to prevent dropping)
    private long displayedToday = 0;
    private long displayedTotal = 0;

    // 5. System State
    private bool isInitialized = false;
    private float syncTimer = 0;
    private const float SYNC_INTERVAL = 15f; // Fetch HC every 10 mins (battery safe)

    void Initialize()
    {
        // 1. Get the initial hardware sensor reading immediately
        FetchLiveSensor();
        
        // 2. Set the baseline to NOW (so we start with 0 delta)
        sensorAtLastSync = currentSensorVal;

        // 3. Trigger the first Health Connect fetch
        RefreshHealthConnectData();
    }
    
    public void OnConnectionEstablished()
    {
        healthPlugin = FindFirstObjectByType<HealthConnectController>().HealthPlugin;
        Initialize();
        StartCoroutine(UpdateRoutine());
    }

    IEnumerator UpdateRoutine()
    {
        WaitForSeconds wait = new WaitForSeconds(1f);
        while (true)
        {
            yield return wait;
            
            // --- A. LIVE UPDATE LOOP (Runs every frame) ---
            FetchLiveSensor();

            // Calculate the "Live Delta" (Steps walked since last HC Sync)
            long liveDelta = currentSensorVal - sensorAtLastSync;

            // Reboot Protection: If phone restarted, sensor drops to 0. 
            // We discard the delta for this session to prevent negatives.
            if (liveDelta < 0) liveDelta = 0;

            // Calculate Theoretical Totals
            long theoreticalToday = todayAnchor + liveDelta;
            long theoreticalTotal = totalAnchor + liveDelta;

            // Apply Ratchet (Never show a lower number than before)
            if (theoreticalToday > displayedToday) displayedToday = theoreticalToday;
            if (theoreticalTotal > displayedTotal) displayedTotal = theoreticalTotal;

            // Update UI
            todayStepsText.text = displayedToday.ToString("N0");
            totalStepsText.text = displayedTotal.ToString("N0");

            // --- B. BACKGROUND SYNC LOOP ---
            syncTimer += 1;
            if (syncTimer >= SYNC_INTERVAL)
            {
                syncTimer = 0;
                RefreshHealthConnectData();
            }
        }
    }

    // --- HELPER: Get Raw Sensor from Android ---
    void FetchLiveSensor()
    {
        // Assuming your plugin has this method exposed
        string raw = healthPlugin.Call<string>("getCurrentNativeSteps");
        if (long.TryParse(raw, out long val))
        {
            currentSensorVal = val;
        }
    }

    // --- HELPER: Trigger Health Connect Fetches ---
    public void RefreshHealthConnectData()
    {
        // 1. Fetch Today (Midnight to Now)
        DateTime now = DateTime.Now;
        DateTime todayStart = DateTime.Today; // 00:00:00 Local
        
        // Force explicit offset to avoid the timezone trap
        DateTimeOffset startOffset = new DateTimeOffset(todayStart, TimeZoneInfo.Local.GetUtcOffset(now));
        
        long startTime = startOffset.ToUnixTimeMilliseconds();
        long endTime = DateTimeOffset.Now.ToUnixTimeMilliseconds();

        // Call Java: Get Today
        healthPlugin.Call("getSteps", startTime, endTime, this.gameObject.name, "OnTodayStepsRecieved");

        // 2. Fetch Total (Lifetime / Long Range)
        // Note: You might want to store a "Base Total" in PlayerPrefs and only fetch 
        // "Today" to add to it, but here is how to fetch a long range (e.g., 1 year)
        long yearAgo = startOffset.AddYears(-1).ToUnixTimeMilliseconds();
        healthPlugin.Call("getSteps", yearAgo, endTime, this.gameObject.name, "OnTotalStepsRecieved");
    }

    // --- CALLBACKS: Called from Java ---

    public void OnTodayStepsRecieved(string steps)
    {
        long newTodayAnchor = long.Parse(steps);
        
        Debug.Log($"[Sync] Today Anchor Updated: {todayAnchor} -> {newTodayAnchor}");

        // CRITICAL: The Handoff
        // 1. Update the trusted Anchor
        todayAnchor = newTodayAnchor;
        
        // 2. Reset the Sensor Baseline to NOW
        // This zeroes out 'liveDelta' so we don't double count the steps 
        // we just synced.
        sensorAtLastSync = currentSensorVal;
    }

    public void OnTotalStepsRecieved(string steps)
    {
        long newTotalAnchor = long.Parse(steps);
        
        Debug.Log($"[Sync] Total Anchor Updated: {totalAnchor} -> {newTotalAnchor}");

        totalAnchor = newTotalAnchor;
        // Note: We don't reset sensorAtLastSync here because OnToday usually 
        // runs at the same time. If they run apart, you might need separate baselines.
        // But for simplicity, assuming they return close together is usually fine.
    }
}