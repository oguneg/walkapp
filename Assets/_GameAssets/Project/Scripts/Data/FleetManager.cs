using System;
using UnityEngine;

/// <summary>
/// The garage and the fuel station, both paid in coins.
/// Trucks are a ladder of one-off purchases: you always drive your best one, and it raises the pay of new
/// offers, cuts their fuel cost and enlarges the tank. Refuelling turns coins into fuel on the spot.
/// </summary>
public class FleetManager : MonoSingleton<FleetManager>
{
    [SerializeField] private TruckData[] trucks;
    [Tooltip("Coins per displayed fuel unit at the fuel station.")]
    [SerializeField] private float refuelPricePerUnit = 5f;

    /// <summary>Truck bought or fuel bought.</summary>
    public event Action OnChanged;

    private const string TierKey = "FleetTier";

    public int OwnedTier { get; private set; }
    public int TruckCount => trucks.Length;
    public TruckData GetTruck(int tier) => trucks[tier];
    public TruckData CurrentTruck => trucks.Length > 0 ? trucks[Mathf.Clamp(OwnedTier, 0, trucks.Length - 1)] : null;
    public TruckData NextTruck => OwnedTier + 1 < trucks.Length ? trucks[OwnedTier + 1] : null;

    public float RewardMultiplier => CurrentTruck ? CurrentTruck.rewardMultiplier : 1f;
    public float FuelMultiplier => CurrentTruck ? CurrentTruck.fuelMultiplier : 1f;
    public int FuelTankBonus => CurrentTruck ? CurrentTruck.fuelTankBonus : 0;

    // Loaded in Awake so CurrencyManager sees the right tank size when it computes caps.
    public override void Init()
    {
        OwnedTier = Mathf.Clamp(PlayerPrefs.GetInt(TierKey, 0), 0, Mathf.Max(0, trucks.Length - 1));
    }

    public bool CanAffordNextTruck =>
        NextTruck != null && CurrencyManager.instance.CanAfford(CurrencyType.Coin, NextTruck.price);

    public bool BuyNextTruck()
    {
        if (!CanAffordNextTruck) return false;

        CurrencyManager.instance.AddCurrency(CurrencyType.Coin, -NextTruck.price);
        OwnedTier++;
        PlayerPrefs.SetInt(TierKey, OwnedTier);
        PlayerPrefs.Save();
        CurrencyManager.instance.CheckCaps(); // bigger tank
        OnChanged?.Invoke();
        return true;
    }

    // ---------------------------------------------------------------- fuel station

    /// <summary>Fuel (internal units) needed to fill the tank.</summary>
    public long MissingFuel =>
        Math.Max(0, CurrencyManager.instance.GetCurrencyCap(CurrencyType.Fuel) -
                    CurrencyManager.instance.GetCurrencyAmount(CurrencyType.Fuel));

    public long RefuelCost(long fuel) =>
        (long)Math.Ceiling(fuel / (double)CurrencyManager.FuelUnit * refuelPricePerUnit);

    /// <summary>Most fuel (internal units, whole displayed units) the player's coins can buy, up to a full tank.</summary>
    public long AffordableFuel()
    {
        long coins = CurrencyManager.instance.GetCurrencyAmount(CurrencyType.Coin);
        long units = (long)Math.Floor(coins / refuelPricePerUnit);
        return Math.Min(MissingFuel, units * CurrencyManager.FuelUnit);
    }

    public bool Refuel(long fuel)
    {
        fuel = Math.Min(fuel, MissingFuel);
        if (fuel <= 0) return false;

        long cost = RefuelCost(fuel);
        if (!CurrencyManager.instance.CanAfford(CurrencyType.Coin, cost)) return false;

        CurrencyManager.instance.AddCurrency(CurrencyType.Coin, -cost);
        CurrencyManager.instance.AddCurrency(CurrencyType.Fuel, fuel);
        OnChanged?.Invoke();
        return true;
    }

    public void DebugResetFleet()
    {
        OwnedTier = 0;
        PlayerPrefs.SetInt(TierKey, 0);
        CurrencyManager.instance.CheckCaps();
        OnChanged?.Invoke();
    }
}
