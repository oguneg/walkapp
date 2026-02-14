using System;
using System.Collections.Generic;
using UnityEngine;

public class CurrencyManager : MonoBehaviour
{
    public Currency[] currencies;
    private Dictionary<CurrencyType, long> currencyAmounts = new Dictionary<CurrencyType, long>();

    private void Awake()
    {
        foreach (Currency currency in currencies)
        {
            currencyAmounts.Add(currency.CurrencyType, 0);
        }
        LoadCurrencies();
    }

    public bool CanAfford(CurrencyType currencyType, long amount)
    {
        return currencyAmounts[currencyType] >= amount;
    }

    public void AddCurrency(CurrencyType currencyType, long amount)
    {
        currencyAmounts[currencyType] += amount;
        if (currencies[(int)currencyType].hasCap)
        {
            currencyAmounts[currencyType] =
                Math.Clamp(currencyAmounts[currencyType], 0, currencies[(int)currencyType].initialCap);
        }
    }

    private void SaveCurrencies()
    {
        foreach (Currency currency in currencies)
        {
            currencyAmounts.Add(currency.CurrencyType, 0);
            PlayerPrefsX.SetLong(currency.SaveKey, currencyAmounts[currency.CurrencyType]);
        }
    }

    private void LoadCurrencies()
    {
        foreach (Currency currency in currencies)
        {
            currencyAmounts.Add(currency.CurrencyType, 0);
            currencyAmounts[currency.CurrencyType] = PlayerPrefsX.GetLong(currency.SaveKey, 0);
        }
    }
    
    void OnApplicationPause(bool paused)
    {
        if (paused)
        {
            SaveCurrencies();
        }
    }

    void OnApplicationQuit()
    {
        SaveCurrencies();
    }
}
