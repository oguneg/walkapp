using System;
using TMPro;
using UnityEngine;

public class CurrencyPanel : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI fuelText, currencyText;
    private int fuelCap = 100;
    private void Awake()
    {
        CurrencyManager.instance.OnCurrencyAmountChanged += OnCurrencyAmountChanged;
    }

    private void OnCurrencyAmountChanged(CurrencyType currencyType, long currencyAmount)
    {
        switch (currencyType)
        {
            case CurrencyType.Fuel: fuelText.text = $"fuel {currencyAmount/1000:N0}/100"; break;
            case CurrencyType.Coin: currencyText.text = $"cash ${currencyAmount:N0}"; break;
            default: break;
        }
    }
}
