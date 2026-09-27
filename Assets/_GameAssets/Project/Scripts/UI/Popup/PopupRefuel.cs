using System;
using TMPro;
using UnityEngine;

/// <summary>Fuel station: fill the tank with coins. Opened from the fuel readout or when a job needs more fuel.</summary>
public class PopupRefuel : PopupBase
{
    [SerializeField] private TextMeshProUGUI noteText, amountText, costText, refuelButtonText;
    [SerializeField] private UnityEngine.UI.Button refuelButton, cancelButton;

    private long fuelNeeded;
    private Action onRefueled;
    private long amount;

    private void Awake()
    {
        refuelButton.onClick.AddListener(OnRefuel);
        cancelButton.onClick.AddListener(HidePopup);
    }

    /// <param name="neededFuel">Fuel a job needs (internal units), or 0 when just topping up.</param>
    /// <param name="refueled">Called after a successful refuel, e.g. to take the job that needed it.</param>
    public void Initialize(long neededFuel, Action refueled)
    {
        fuelNeeded = neededFuel;
        onRefueled = refueled;

        var fleet = FleetManager.instance;
        var currency = CurrencyManager.instance;
        long have = currency.GetCurrencyAmount(CurrencyType.Fuel);
        long unit = CurrencyManager.FuelUnit;

        // Fill up if affordable, otherwise as much as the coins cover.
        amount = Math.Min(fleet.MissingFuel, fleet.AffordableFuel());
        bool coversJob = fuelNeeded <= 0 || have + amount >= fuelNeeded;
        long cost = fleet.RefuelCost(amount);

        noteText.text = fuelNeeded > 0
            ? $"This job needs <b><sprite=4>{fuelNeeded / unit:N0}</b>, your tank has <sprite=4>{have / unit:N0}."
            : $"Tank: <sprite=4>{have / unit:N0} / {currency.GetFuelCap / unit:N0}";

        if (fleet.MissingFuel <= 0)
        {
            amountText.text = "Tank is full";
            costText.text = "";
            SetButton("OK", true);
            return;
        }

        amountText.text = $"+<sprite=4>{amount / unit:N0}";
        costText.text = amount > 0 ? $"<sprite=0>{cost:N0}" : "Not enough cash";
        SetButton(onRefueled != null ? "REFUEL & GO" : "REFUEL", amount > 0 && coversJob);
    }

    private void SetButton(string label, bool interactable)
    {
        refuelButtonText.text = label;
        refuelButton.interactable = interactable;
    }

    private void OnRefuel()
    {
        if (FleetManager.instance.MissingFuel <= 0)
        {
            HidePopup();
            return;
        }

        if (!FleetManager.instance.Refuel(amount))
        {
            AudioManager.instance.PlaySound(SoundType.Fail);
            return;
        }

        AudioManager.instance.PlaySound(SoundType.Success);
        var callback = onRefueled;
        onRefueled = null;
        HidePopup();
        callback?.Invoke();
    }
}
