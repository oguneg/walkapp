using System;
using UnityEngine;

/// <summary>Player settings from the settings popup, saved in PlayerPrefs.</summary>
public static class GameSettings
{
    private const string SoundKey = "Settings.Sound";
    private const string NotificationsKey = "Settings.Notifications";

    public static event Action OnChanged;

    public static bool SoundOn
    {
        get => PlayerPrefs.GetInt(SoundKey, 1) == 1;
        set => Set(SoundKey, value);
    }

    public static bool NotificationsOn
    {
        get => PlayerPrefs.GetInt(NotificationsKey, 1) == 1;
        set => Set(NotificationsKey, value);
    }

    private static void Set(string key, bool value)
    {
        PlayerPrefs.SetInt(key, value ? 1 : 0);
        PlayerPrefs.Save();
        OnChanged?.Invoke();
    }
}
