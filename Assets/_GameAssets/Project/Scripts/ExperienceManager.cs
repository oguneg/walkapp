using System;
using TMPro;
using UnityEngine;

public class ExperienceManager : MonoSingleton<ExperienceManager>
{
    private const string LevelSaveKey = "LevelKey";
    private const string ExpSaveKey = "ExpKey";

    private int level;
    private long exp, requiredExpForLevelUp;

    [SerializeField] private TextMeshProUGUI levelText, expText;

    /// <summary>The level the HUD shows: a new player is level 1.</summary>
    public int Level => level + 1;

    /// <summary>(old level, new level), both as shown in the HUD.</summary>
    public event Action<int, int> OnLevelChanged;

    // Loaded in Awake so unlock checks (ProgressionManager) see the real level from the first frame.
    public override void Init()
    {
        LoadPlayerStats();
        CalculateRequiredExp();
    }

    private void Start()
    {
        UpdateGUI();
    }

    public void AddExperience(long amount)
    {
        int before = Level;
        exp += amount;
        CheckForLevelUp();
        UpdateGUI();
        if (Level != before)
        {
            SavePlayerStats();
            OnLevelChanged?.Invoke(before, Level);
        }
    }

    private void CheckForLevelUp()
    {
        while (exp >= requiredExpForLevelUp)
        {
            exp -= requiredExpForLevelUp;
            level++;
            CalculateRequiredExp();
        }
    }

    private void CalculateRequiredExp()
    {
        requiredExpForLevelUp = (long)(1000 * Math.Pow(1.2, level));
    }

    private void UpdateGUI()
    {
        if (levelText) levelText.text = $"<sprite=2>{Level}";
        if (expText) expText.text = $"<sprite=3>{NumberFormat.Compact(exp)}/{NumberFormat.Compact(requiredExpForLevelUp)}";
    }

    /// <summary>SRDebugger: jump to a level (as shown in the HUD) with no XP towards the next.</summary>
    public void DebugSetLevel(int displayLevel)
    {
        int before = Level;
        level = Mathf.Max(0, displayLevel - 1);
        exp = 0;
        CalculateRequiredExp();
        UpdateGUI();
        SavePlayerStats();
        if (Level != before) OnLevelChanged?.Invoke(before, Level);
    }

    void OnApplicationPause(bool paused)
    {
        if (paused)
        {
            SavePlayerStats();
        }
    }

    void OnApplicationQuit()
    {
        SavePlayerStats();
    }

    private void LoadPlayerStats()
    {
        level = PlayerPrefs.GetInt(LevelSaveKey);
        exp = PlayerPrefsX.GetLong(ExpSaveKey);
    }

    private void SavePlayerStats()
    {
        PlayerPrefs.SetInt(LevelSaveKey, level);
        PlayerPrefsX.SetLong(ExpSaveKey, exp);

        PlayerPrefs.Save();
    }
}
