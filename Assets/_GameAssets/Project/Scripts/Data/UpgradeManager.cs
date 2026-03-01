using System;
using TMPro;
using UnityEngine;

public class UpgradeManager : MonoSingleton<UpgradeManager>
{
    public UpgradeData[] upgrades;
    public UpgradeItemView[] upgradeItemViews;

    public float[] globalMultipliers;
    [SerializeField] private TextMeshProUGUI[] globalMultiplierTexts;

    public void Start()
    {
        for (int i = 0; i < upgrades.Length; i++)
        {
            upgradeItemViews[i].AssignUpgrade(upgrades[i]);
        }
    }

    public void UpdateMultiplier(UpgradeType type, float multiplier, bool isMultiplicative)
    {
        if (isMultiplicative)
        {
            globalMultipliers[(int)type] *= multiplier;
            globalMultiplierTexts[(int)type].text = $"{type}{Environment.NewLine} x{globalMultipliers[(int)type]:F2}";
        }
        else
        {
            globalMultipliers[(int)type] += multiplier;
            globalMultiplierTexts[(int)type].text = $"{type}{Environment.NewLine} +{globalMultipliers[(int)type]:F2}";
        }
    }
}