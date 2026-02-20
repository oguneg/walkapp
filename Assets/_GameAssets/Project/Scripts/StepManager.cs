using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
#if UNITY_ANDROID
using UnityEngine.Android;
#endif

public class StepManager : MonoSingleton<StepManager>
{
    [SerializeField] private TextMeshProUGUI totalStepText, offlineStepText, sessionStepText, readStepText;
    public int totalSteps { get; private set; }
    public int readSteps { get; private set; }
    public int offlineSteps { get; private set; }
    public int sessionSteps { get; private set; }
    private int sessionStepsAnchor;
    private int startupHardwareSteps;

    private const string LastHardwareStepsKey = "LastHardwareSteps";
    private const string TotalStepsKey = "TotalSteps";

    private IEnumerator Start()
    {
#if UNITY_ANDROID
        if (!Permission.HasUserAuthorizedPermission("android.permission.ACTIVITY_RECOGNITION"))
            Permission.RequestUserPermission("android.permission.ACTIVITY_RECOGNITION");

        while (!Permission.HasUserAuthorizedPermission("android.permission.ACTIVITY_RECOGNITION"))
        {
            yield return new WaitForSeconds(1f);
        }
#endif
        while (StepCounter.current == null)
        {
            Debug.Log("permission waiting");
            yield return null;
        }

        Debug.Log("permission granted");
        InputSystem.EnableDevice(StepCounter.current);
        
        // --- THE DETERMINISTIC WAIT ---
        float timeoutLimit = 10.0f; // Max wait time before giving up
        float timer = 0f;

        // Poll every frame until Android hands us real data (or we time out)
        while (StepCounter.current.stepCounter.ReadValue() == 0 && timer < timeoutLimit)
        {
            timer += Time.deltaTime;
            yield return null; 
        }

        if (timer >= timeoutLimit)
        {
            Debug.LogWarning("Sensor initialization timed out or is legitimately 0. Proceeding.");
        }
        else
        {
            Debug.Log($"Sensor woke up in {timer:F2} seconds!");
        }
        
        LoadSteps();
        yield return null;
        StartCoroutine(UpdateRoutine());
    }

    private void LoadSteps()
    {
        if (StepCounter.current == null) return;

        startupHardwareSteps = StepCounter.current.stepCounter.ReadValue();
        readSteps = startupHardwareSteps;

        // --- THE FIRST RUN FIX ---
        // If this key doesn't exist, this is a 100% fresh install or wiped data.
        if (!PlayerPrefs.HasKey(LastHardwareStepsKey))
        {
            Debug.Log("First run detected! Setting offline steps to 0.");
            offlineSteps = 0;
            totalSteps = 0;
            
            // Immediately lock in the current hardware count as the baseline
            PlayerPrefs.SetInt(LastHardwareStepsKey, startupHardwareSteps);
            PlayerPrefs.SetInt(TotalStepsKey, 0);
            PlayerPrefs.Save();
        }
        else
        {
            // Normal loading logic for returning players
            int lastSavedSteps = PlayerPrefs.GetInt(LastHardwareStepsKey);
            totalSteps = PlayerPrefs.GetInt(TotalStepsKey, 0);

            if (startupHardwareSteps < lastSavedSteps)
                offlineSteps = startupHardwareSteps; // Reboot safety net
            else
                offlineSteps = startupHardwareSteps - lastSavedSteps;
        }
        
        RegisterSteps(offlineSteps);
        sessionSteps = 0;
        sessionStepsAnchor = 0;

        UpdateGUI();
    }

    private void RegisterSteps(int amount)
    {
        totalSteps += amount;
        JobManager.instance.RegisterSteps(amount);
    }

    private IEnumerator UpdateRoutine()
    {
        WaitForSeconds wfs = new WaitForSeconds(1f);

        while (true)
        {
            yield return wfs;
            FetchLiveSteps();
        }
    }

    void FetchLiveSteps()
    {
        Debug.Log("fetching live steps");
        readSteps = StepCounter.current.stepCounter.ReadValue();
        sessionSteps = StepCounter.current.stepCounter.ReadValue() - startupHardwareSteps;
        RegisterSteps(sessionSteps - sessionStepsAnchor);
        sessionStepsAnchor = sessionSteps;
        UpdateGUI();
    }

    void OnApplicationPause(bool isPaused)
    {
        if (!PlayerPrefs.HasKey(LastHardwareStepsKey)) return;
        
        if (isPaused) SaveState();
        else
        {
            LoadSteps();
        }
    }

    private void UpdateGUI()
    {
        totalStepText.text = $"{totalSteps:N0}";
        sessionStepText.text = $"{sessionSteps:N0}";
        readStepText.text = $"{readSteps:N0}";
        offlineStepText.text = $"{offlineSteps:N0}";
    }

    void OnApplicationQuit() => SaveState();

    private void SaveState()
    {
        if (StepCounter.current != null)
        {
            PlayerPrefs.SetInt(LastHardwareStepsKey, StepCounter.current.stepCounter.ReadValue());
        }

        PlayerPrefs.SetInt(TotalStepsKey, totalSteps);
        PlayerPrefs.Save();
    }

    void OnDestroy()
    {
        if (StepCounter.current != null)
            InputSystem.DisableDevice(StepCounter.current);
    }
}