using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class CurrencyManager : MonoSingleton<CurrencyManager>
{
    public Currency[] currencies;

    // Data containers
    private Dictionary<CurrencyType, long> currencyAmounts = new Dictionary<CurrencyType, long>();
    private Dictionary<CurrencyType, long> currencyCaps = new Dictionary<CurrencyType, long>();
    private Dictionary<CurrencyType, Currency> currencyMap = new Dictionary<CurrencyType, Currency>();

    // NEW: Timers for active runtime regeneration
    private Dictionary<CurrencyType, float> activeRegenTimers = new Dictionary<CurrencyType, float>();

    public UnityAction<CurrencyType, long> OnCurrencyAmountChanged;

    public long GetFuelCap => currencies[(int)CurrencyType.Fuel].initialCap;

    private const string LAST_SESSION_TIME_KEY = "LastSessionTime_UTC";
    
    private UpgradeManager upgradeManager;

    public long GetCurrencyCap(CurrencyType currencyType)
    {
        return currencyCaps[currencyType];
    }

    private void Awake()
    {
        upgradeManager = UpgradeManager.instance;
        foreach (Currency currency in currencies)
        {
            currencyAmounts.Add(currency.CurrencyType, 0);
            currencyCaps.Add(currency.CurrencyType, currency.initialCap);
            if (!currencyMap.ContainsKey(currency.CurrencyType))
            {
                currencyMap.Add(currency.CurrencyType, currency);
            }

            if (currency.regenerateOffline)
            {
                activeRegenTimers.Add(currency.CurrencyType, 0f);
            }
        }
    }

    private void Start()
    {
        LoadCurrencies();
        StartCoroutine(UpdateTimers());
    }

    private IEnumerator UpdateTimers()
    {
        var wfs = new WaitForSeconds(1f);
        while (true)
        {
            foreach (Currency currency in currencies)
            {
                if (currency.regenerateOffline &&
                    currencyAmounts[currency.CurrencyType] < currency.initialCap)
                {
                    activeRegenTimers[currency.CurrencyType] += 1;

                    if (activeRegenTimers[currency.CurrencyType] >= currency.regenIntervalInSeconds)
                    {
                        AddCurrency(currency.CurrencyType, currency.regenRate);
                        activeRegenTimers[currency.CurrencyType] -= currency.regenIntervalInSeconds;
                    }
                }
            }
            yield return wfs;
        }
    }

    public bool CanAfford(CurrencyType currencyType, long amount)
    {
        return currencyAmounts.ContainsKey(currencyType) && currencyAmounts[currencyType] >= amount;
    }

    public void AddCurrency(CurrencyType currencyType, long amount)
    {
        if (!currencyAmounts.ContainsKey(currencyType)) return;

        currencyAmounts[currencyType] += amount;

        if (currencyMap[currencyType].hasCap)
        {
            currencyAmounts[currencyType] = Math.Clamp(currencyAmounts[currencyType], 0, currencyCaps[currencyType]);
        }

        OnCurrencyAmountChanged?.Invoke(currencyType, currencyAmounts[currencyType]);
    }

    private void SaveCurrencies()
    {
        foreach (Currency currency in currencies)
        {
            PlayerPrefsX.SetLong(currency.SaveKey, currencyAmounts[currency.CurrencyType]);
        }

        long quitTime = DateTime.UtcNow.ToBinary();
        PlayerPrefs.SetString(LAST_SESSION_TIME_KEY, quitTime.ToString());

        PlayerPrefs.Save();
    }

    private void LoadCurrencies()
    {
        foreach (Currency currency in currencies)
        {
            long defaultValue = currency.hasCap && currency.regenerateOffline ? currency.initialCap : 0;
            currencyAmounts[currency.CurrencyType] = PlayerPrefsX.GetLong(currency.SaveKey, defaultValue);
            OnCurrencyAmountChanged?.Invoke(currency.CurrencyType, currencyAmounts[currency.CurrencyType]);
        }

        // Calculate what happened while we were asleep
        ProcessOfflineRegeneration();
    }

    private void ProcessOfflineRegeneration()
    {
        if (!PlayerPrefs.HasKey(LAST_SESSION_TIME_KEY)) return;

        string timeStr = PlayerPrefs.GetString(LAST_SESSION_TIME_KEY);
        if (long.TryParse(timeStr, out long binaryTime))
        {
            DateTime lastSessionTime = DateTime.FromBinary(binaryTime);
            TimeSpan timeAway = DateTime.UtcNow - lastSessionTime;

            // Safety check: Prevent negative time (clock changes)
            if (timeAway.TotalSeconds < 0) return;

            foreach (Currency currency in currencies)
            {
                if (currency.regenerateOffline && currency.regenIntervalInSeconds > 0)
                {
                    // If full, skip
                    if (currencyAmounts[currency.CurrencyType] >= currency.initialCap) continue;

                    long intervals = (long)(timeAway.TotalSeconds / currency.regenIntervalInSeconds);

                    if (intervals > 0)
                    {
                        AddCurrency(currency.CurrencyType, intervals * currency.regenRate);

                        float remainder = (float)(timeAway.TotalSeconds % currency.regenIntervalInSeconds);
                        activeRegenTimers[currency.CurrencyType] = remainder;
                    }
                }
            }
        }
    }

    void OnApplicationPause(bool paused)
    {
        if (paused)
        {
            SaveCurrencies();
        }
        else
        {
            ProcessOfflineRegeneration();
        }
    }

    void OnApplicationQuit()
    {
        SaveCurrencies();
    }

    public void CheckCaps()
    {
        currencyCaps[CurrencyType.BankedStep] = currencyMap[CurrencyType.BankedStep].initialCap +
                                                (long)upgradeManager.globalMultipliers[2];

        AddCurrency(CurrencyType.BankedStep, 0);
    }

    public long GetCurrencyAmount(CurrencyType type)
    {
        return currencyAmounts[type];
    }
}