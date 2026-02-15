using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class CurrencyManager : MonoSingleton<CurrencyManager>
{
    public Currency[] currencies;
    private Dictionary<CurrencyType, long> currencyAmounts = new Dictionary<CurrencyType, long>();
    public UnityAction<CurrencyType, long> OnCurrencyAmountChanged;
    
    private void Awake()
    {
        foreach (Currency currency in currencies)
        {
            currencyAmounts.Add(currency.CurrencyType, 0);
        }
    }

    private void Start()
    {
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
        OnCurrencyAmountChanged?.Invoke(currencyType, currencyAmounts[currencyType]);
    }

    private void SaveCurrencies()
    {
        foreach (Currency currency in currencies)
        {
            PlayerPrefsX.SetLong(currency.SaveKey, currencyAmounts[currency.CurrencyType]);
        }
    }

    private void LoadCurrencies()
    {
        foreach (Currency currency in currencies)
        {
            currencyAmounts[currency.CurrencyType] = PlayerPrefsX.GetLong(currency.SaveKey, 0);
            OnCurrencyAmountChanged?.Invoke(currency.CurrencyType, currencyAmounts[currency.CurrencyType]);
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
