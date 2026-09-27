using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UpgradeItemView : MonoBehaviour
{
    [SerializeField] private Button buyButton;
    [SerializeField] private TextMeshProUGUI upgradeNameText, upgradeDescriptionText, upgradeCostText;
    [SerializeField] private TextMeshProUGUI levelText, valueText;
    private UpgradeData assignedUpgrade;

    // Views and managers live in the same scene for its whole lifetime, so no unsubscribe is needed
    // (touching a MonoSingleton's instance in OnDestroy during quit would spawn a temporary one).
    public void AssignUpgrade(UpgradeData upgrade)
    {
        if (assignedUpgrade == null)
        {
            UpgradeManager.instance.OnUpgradesChanged += Refresh;
            CurrencyManager.instance.OnCurrencyAmountChanged += OnCurrencyChanged;
        }

        assignedUpgrade = upgrade;
        Refresh();
    }

    private void OnCurrencyChanged(CurrencyType type, long amount)
    {
        if (type == CurrencyType.Coin) Refresh();
    }

    private void Refresh()
    {
        if (assignedUpgrade == null) return;

        var manager = UpgradeManager.instance;
        int level = manager.GetLevel(assignedUpgrade);
        bool maxed = assignedUpgrade.IsMaxed(level);
        long cost = assignedUpgrade.CostAtLevel(level);

        upgradeNameText.text = assignedUpgrade.upgradeName;
        upgradeDescriptionText.text = string.IsNullOrEmpty(assignedUpgrade.description)
            ? assignedUpgrade.upgradeEffects[0].type.ToString()
            : assignedUpgrade.description;
        if (levelText) levelText.text = $"Lv {level}"; // the buy button says MAX when maxed
        if (valueText) valueText.text = FormatValue(assignedUpgrade, manager, maxed);

        upgradeCostText.text = maxed ? "MAX" : $"<sprite=0>{cost:N0}";
        buyButton.interactable = !maxed && CurrencyManager.instance.CanAfford(CurrencyType.Coin, cost);
    }

    // "x1.27 > x1.30" or "+2,000 > +2,500": what you have now and what the next level gives.
    private static string FormatValue(UpgradeData upgrade, UpgradeManager manager, bool maxed)
    {
        UpgradeEffect effect = upgrade.upgradeEffects[0];
        float now = manager.Get(effect.type);
        if (!string.IsNullOrEmpty(upgrade.displayFormat))
        {
            float nextRaw = effect.isMultiplicative ? now * effect.increaseValue : now + effect.increaseValue;
            string Show(float v) => string.Format(upgrade.displayFormat,
                effect.isMultiplicative ? upgrade.displayOffset * v : upgrade.displayOffset + v);
            return maxed ? Show(now) : $"{Show(now)} <color=#FFFFFFAA>></color> <b>{Show(nextRaw)}</b>";
        }

        if (effect.isMultiplicative)
        {
            float next = now * effect.increaseValue;
            return maxed ? $"x{now:0.00}" : $"x{now:0.00} <color=#FFFFFFAA>></color> <b>x{next:0.00}</b>";
        }

        float after = now + effect.increaseValue;
        return maxed ? $"+{now:N0}" : $"+{now:N0} <color=#FFFFFFAA>></color> <b>+{after:N0}</b>";
    }

    public void OnBuyButtonClick()
    {
        if (UpgradeManager.instance.TryBuy(assignedUpgrade))
        {
            AudioManager.instance.PlaySound(SoundType.Button);
            transform.DOKill(true);
            transform.DOPunchScale(Vector3.one * 0.04f, 0.25f, 6);
        }
        else
        {
            AudioManager.instance.PlaySound(SoundType.Fail);
        }
    }
}
