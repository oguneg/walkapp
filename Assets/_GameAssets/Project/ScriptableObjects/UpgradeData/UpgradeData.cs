using System;
using UnityEngine;

[CreateAssetMenu(fileName = "UpgradeData", menuName = "Scriptable Object/Upgrade Data")]
public class UpgradeData : ScriptableObject
{
    public UpgradeEffect[] upgradeEffects;
    public long baseCost;
    public float costExponent;
    public string upgradeName;
    public string upgradeSaveKey;
    [Tooltip("Shown under the name, e.g. \"More coins from every job\".")]
    public string description;
    [Tooltip("0 = no limit.")]
    public int maxLevel;
    [Tooltip("Optional display: shown value = offset + value (additive) or offset * value (multiplicative), " +
             "formatted with displayFormat, e.g. offset 3 and \"{0:0.0}x\" shows the max burn rate.")]
    public float displayOffset;
    public string displayFormat;

    public long CostAtLevel(int level) => (long)(Math.Pow(costExponent, level) * baseCost);
    public bool IsMaxed(int level) => maxLevel > 0 && level >= maxLevel;
}

[Serializable]
public struct UpgradeEffect
{
    public UpgradeType type;
    public float increaseValue;
    public bool isMultiplicative;
}

public enum UpgradeType
{
    FuelEfficiency, IncomeMultiplier, BankedStepCap, FuelTank, FuelRegen, ExpressIncome, ExpressFrequency,
    BurnRateMax, BurnLoss
}
