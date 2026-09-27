using System;
using DG.Tweening;
using TMPro;
using UnityEngine;

public class CurrencyPanel : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI fuelText, currencyText, bankedStepText;
    [Tooltip("Tapping the fuel readout opens the fuel station.")]
    [SerializeField] private UnityEngine.UI.Button fuelButton;

    private long shownCoins = -1;
    private Tween coinTween;

    private void Awake()
    {
        CurrencyManager.instance.OnCurrencyAmountChanged += OnCurrencyAmountChanged;
        if (fuelButton) fuelButton.onClick.AddListener(() => UIManager.instance.ShowRefuel(0, null));
    }

    private void OnCurrencyAmountChanged(CurrencyType currencyType, long currencyAmount)
    {
        var manager = CurrencyManager.instance;
        switch (currencyType)
        {
            case CurrencyType.Fuel:
                long cap = manager.GetFuelCap;
                fuelText.text = $"<sprite=4>{currencyAmount / CurrencyManager.FuelUnit}/{cap / CurrencyManager.FuelUnit}";
                break;
            case CurrencyType.BankedStep:
                bankedStepText.text = $"<size=62%><color=#FFFFFFB0>BANKED STEPS</color></size>{Environment.NewLine}<sprite=1>{currencyAmount:N0}/{manager.GetCurrencyCap(CurrencyType.BankedStep):N0}";
                break;
            case CurrencyType.Coin:
                AnimateCoins(currencyAmount);
                break;
        }
    }

    // Coins count up/down to the new value so earning and spending are visible, with a pop on gains.
    private void AnimateCoins(long target)
    {
        if (shownCoins < 0 || !isActiveAndEnabled)
        {
            SetCoins(target);
            return;
        }

        long from = shownCoins;
        coinTween?.Kill();
        coinTween = DOVirtual.Float(0f, 1f, 0.45f, t => SetCoins(from + (long)Math.Round((target - from) * t)))
            .SetEase(Ease.OutCubic)
            .OnComplete(() => SetCoins(target));

        if (target > from)
        {
            currencyText.transform.DOKill(true);
            currencyText.transform.DOPunchScale(Vector3.one * 0.12f, 0.35f, 6);
        }
    }

    private void SetCoins(long value)
    {
        shownCoins = value;
        currencyText.text = $"<sprite=0>{value:N0}";
    }
}
