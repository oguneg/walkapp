using System;
using UnityEngine;

public class UpgradeManager : MonoSingleton<UpgradeManager>
{
    public UpgradeData[] upgrades;
    public UpgradeItemView[] upgradeItemViews;

    /// <summary>Current value per UpgradeType: products for multiplicative types, sums for additive ones.</summary>
    public float[] globalMultipliers;

    /// <summary>Raised after a purchase so upgrade cards can refresh levels and prices.</summary>
    public event Action OnUpgradesChanged;

    // Saved levels are applied here, in Awake, so the multipliers are final before any Start() reads them.
    // (CurrencyManager computes the bank and fuel caps from them when it loads.)
    public override void Init()
    {
        int count = Enum.GetValues(typeof(UpgradeType)).Length;
        globalMultipliers = new float[count];
        for (int i = 0; i < count; i++)
            globalMultipliers[i] = IsAdditive((UpgradeType)i) ? 0f : 1f;

        foreach (var upgrade in upgrades)
        {
            int level = GetLevel(upgrade);
            for (int l = 0; l < level; l++) Apply(upgrade);
        }
    }

    public void Start()
    {
        for (int i = 0; i < upgrades.Length && i < upgradeItemViews.Length; i++)
        {
            upgradeItemViews[i].gameObject.SetActive(true);
            upgradeItemViews[i].AssignUpgrade(upgrades[i]);
        }
    }

    public static bool IsAdditive(UpgradeType type) =>
        type == UpgradeType.BankedStepCap || type == UpgradeType.FuelTank;

    public float Get(UpgradeType type) => globalMultipliers[(int)type];

    public int GetLevel(UpgradeData upgrade) => PlayerPrefs.GetInt(upgrade.upgradeSaveKey, 0);

    public bool TryBuy(UpgradeData upgrade)
    {
        int level = GetLevel(upgrade);
        if (upgrade.IsMaxed(level)) return false;

        long cost = upgrade.CostAtLevel(level);
        if (!CurrencyManager.instance.CanAfford(CurrencyType.Coin, cost)) return false;

        CurrencyManager.instance.AddCurrency(CurrencyType.Coin, -cost);
        PlayerPrefs.SetInt(upgrade.upgradeSaveKey, level + 1);
        Apply(upgrade);
        CurrencyManager.instance.CheckCaps();
        OnUpgradesChanged?.Invoke();
        return true;
    }

    private void Apply(UpgradeData upgrade)
    {
        foreach (var effect in upgrade.upgradeEffects)
        {
            int i = (int)effect.type;
            if (effect.isMultiplicative) globalMultipliers[i] *= effect.increaseValue;
            else globalMultipliers[i] += effect.increaseValue;
        }
    }
}
