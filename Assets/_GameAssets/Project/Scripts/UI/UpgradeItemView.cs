using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UpgradeItemView : MonoBehaviour
{
    [SerializeField] private Button buyButton;
    [SerializeField] private TextMeshProUGUI upgradeNameText, upgradeDescriptionText, upgradeCostText;
    private int upgradeLevel;
    private UpgradeData assignedUpgrade;
    private long upgradeCost;
    public void AssignUpgrade(UpgradeData upgrade)
    {
        assignedUpgrade = upgrade;
        upgradeNameText.text = upgrade.upgradeName;
        upgradeDescriptionText.text = $"{upgrade.upgradeEffects[0].type} {(upgrade.upgradeEffects[0].isMultiplicative?'x':'+')}{upgrade.upgradeEffects[0].increaseValue}";
        LoadUpgradeLevel();
        CalculateCost();
    }

    private void LoadUpgradeLevel()
    {
        upgradeLevel = PlayerPrefs.GetInt(assignedUpgrade.upgradeSaveKey, 0);
        for (int i = 0; i < upgradeLevel; i++)
        {
            foreach (var effect in assignedUpgrade.upgradeEffects)
            {
                UpgradeManager.instance.UpdateMultiplier(effect.type, effect.increaseValue, effect.isMultiplicative);
            }
        }
    }

    private void CalculateCost()
    {
        upgradeCost = (long)(Math.Pow(assignedUpgrade.costExponent, upgradeLevel) * assignedUpgrade.baseCost);
        upgradeCostText.text = $"<sprite=0>{upgradeCost:N0}";
    }

    public void OnBuyButtonClick()
    {
        if (CurrencyManager.instance.CanAfford(CurrencyType.Coin, upgradeCost))
        {
            CurrencyManager.instance.AddCurrency(CurrencyType.Coin, -upgradeCost);
            upgradeLevel++;
            PlayerPrefs.SetInt(assignedUpgrade.upgradeSaveKey, upgradeLevel);
            var upgradeEffect = assignedUpgrade.upgradeEffects[0];
            UpgradeManager.instance.UpdateMultiplier(upgradeEffect.type, upgradeEffect.increaseValue, upgradeEffect.isMultiplicative);
            CalculateCost();
        }
    }
}
