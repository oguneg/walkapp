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
    
    private void Start()
    {
        LoadPlayerStats();
        CalculateRequiredExp();
        UpdateGUI();
    }

    public void AddExperience(long amount)
    {
        exp += amount;
        CheckForLevelUp();
        UpdateGUI();
    }

    private void CheckForLevelUp()
    {
        if (exp > requiredExpForLevelUp)
        {
            exp -= requiredExpForLevelUp;
            level++;
            CalculateRequiredExp();
            CheckForLevelUp();
        }
    }

    private void CalculateRequiredExp()
    {
        requiredExpForLevelUp = 1000 * (long)Math.Pow(1.2f, level);
    }

    private void UpdateGUI()
    {
        levelText.text = $"Lv. {level + 1}";
        expText.text = $"XP {exp}/{requiredExpForLevelUp}";
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