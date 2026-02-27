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
    FuelEfficiency, IncomeMultiplier, ExpMultiplier
}