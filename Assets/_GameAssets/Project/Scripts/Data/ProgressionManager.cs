using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>Content that opens up with the player's level. Short jobs and the Jobs/Active tabs are always there.</summary>
public enum Feature
{
    UpgradesTab,
    MediumJobs,
    DailyStars, // Stats tab: daily milestones and the momentum coin bonus
    Express,
    BurnRate,
    FleetTab,
    LongJobs
}

/// <summary>
/// Introduces the game one piece at a time: each feature (and each upgrade, via UpgradeData.unlockLevel) has a
/// player level it appears at. Levels here are the ones the HUD shows (a new player is level 1).
/// Views check IsUnlocked and listen to OnUnlocksChanged; a level-up popup says what just opened and what's next.
/// </summary>
public class ProgressionManager : MonoSingleton<ProgressionManager>
{
    [Serializable]
    public class Unlock
    {
        public Feature feature;
        [Min(1)] public int level = 1;
        [Tooltip("Shown in the level-up popup.")]
        public string title;
        public string description;
    }

    [SerializeField] private Unlock[] unlocks =
    {
        new Unlock { feature = Feature.UpgradesTab, level = 2, title = "Upgrades", description = "Spend coins to earn more from every job." },
        new Unlock { feature = Feature.MediumJobs, level = 3, title = "Medium jobs", description = "Bigger hauls, bigger pay, more time." },
        new Unlock { feature = Feature.DailyStars, level = 4, title = "Daily stars", description = "Hit step milestones every day for a coin bonus on every job." },
        new Unlock { feature = Feature.Express, level = 5, title = "Express deliveries", description = "Rush jobs with a tight deadline that pay 3-5x." },
        new Unlock { feature = Feature.BurnRate, level = 6, title = "Burn rate", description = "Choose how fast your banked steps push your job." },
        new Unlock { feature = Feature.FleetTab, level = 7, title = "Fleet", description = "Buy bigger trucks for better pay and cheaper fuel." },
        new Unlock { feature = Feature.LongJobs, level = 9, title = "Long hauls", description = "Week-long jobs with big pay and a relaxed pace." },
    };

    /// <summary>Something was unlocked (level up) or locked again (debug level reset).</summary>
    public event Action OnUnlocksChanged;

    public int Level => ExperienceManager.instance.Level;

    public int UnlockLevel(Feature feature) => unlocks.FirstOrDefault(u => u.feature == feature)?.level ?? 1;
    public bool IsUnlocked(Feature feature) => Level >= UnlockLevel(feature);
    public bool IsUnlocked(UpgradeData upgrade) => Level >= Mathf.Max(1, upgrade.unlockLevel);

    public bool IsUnlocked(JobType type) =>
        type == JobType.Short ||
        IsUnlocked(type == JobType.Medium ? Feature.MediumJobs : Feature.LongJobs);

    private void Start()
    {
        ExperienceManager.instance.OnLevelChanged += OnLevelChanged;
    }

    private void OnLevelChanged(int from, int to)
    {
        OnUnlocksChanged?.Invoke();
        if (to > from) ShowLevelUp(from, to);
    }

    private void ShowLevelUp(int from, int to)
    {
        var lines = new List<string>();
        foreach (var unlock in unlocks.Where(u => u.level > from && u.level <= to).OrderBy(u => u.level))
            lines.Add($"<b>{unlock.title}</b>: {unlock.description}");

        var upgrades = UpgradeManager.instance.upgrades
            .Where(u => u.unlockLevel > from && u.unlockLevel <= to)
            .Select(u => u.upgradeName)
            .ToList();
        if (upgrades.Count > 0) lines.Add($"New upgrades: <b>{string.Join(", ", upgrades)}</b>");

        if (lines.Count == 0) lines.Add("Keep on trucking!");

        var next = unlocks.Where(u => u.level > to).OrderBy(u => u.level).FirstOrDefault();
        if (next != null) lines.Add($"\n<size=85%>Next at level {next.level}: {next.title}</size>");

        UIManager.instance.ShowMessage($"LEVEL {to}!", string.Join("\n", lines), "GREAT!");
    }
}
