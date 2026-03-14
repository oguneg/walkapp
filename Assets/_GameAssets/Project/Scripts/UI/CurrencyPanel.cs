using System;
using TMPro;
using UnityEngine;

public class CurrencyPanel : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI fuelText, currencyText, bankedStepText;
    private long fuelCap = 100;
    private long bankedStepCap = 1000;
    private void Awake()
    {
        CurrencyManager.instance.OnCurrencyAmountChanged += OnCurrencyAmountChanged;
        fuelCap = CurrencyManager.instance.GetFuelCap;
    }

    private void OnCurrencyAmountChanged(CurrencyType currencyType, long currencyAmount)
    {
        switch (currencyType)
        {
            case CurrencyType.Fuel: fuelText.text = $"<sprite=4>{currencyAmount/1000}/{fuelCap/1000}"; break;
            case CurrencyType.BankedStep: bankedStepText.text = $"Banked Steps{Environment.NewLine}<sprite=1>{currencyAmount:N0}/{CurrencyManager.instance.GetCurrencyCap(CurrencyType.BankedStep):N0}"; break;
            case CurrencyType.Coin: currencyText.text = $"<sprite=0>{currencyAmount:N0}"; break;
            default: break;
        }
    }
}
