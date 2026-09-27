using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace OgunWorks.UI
{
    /// <summary>Fleet tab: the truck you drive, the dealership ladder and the fuel station.</summary>
    public class FleetView : MonoBehaviour
    {
        [Header("Current truck")]
        [SerializeField] private TextMeshProUGUI currentTierText, currentNameText, currentStatsText, currentDescriptionText;
        [SerializeField] private UnityEngine.UI.Image currentTruckImage;

        [Header("Dealership")]
        [SerializeField] private TruckRowView rowTemplate;
        [SerializeField] private Transform rowParent;

        [Header("Fuel station")]
        [SerializeField] private TextMeshProUGUI fuelText;
        [SerializeField] private UnityEngine.UI.Button refuelButton;
        [SerializeField] private TextMeshProUGUI refuelButtonText;

        private readonly List<TruckRowView> rows = new List<TruckRowView>();

        private void Start()
        {
            var fleet = FleetManager.instance;
            rowTemplate.gameObject.SetActive(false);
            for (int i = 0; i < fleet.TruckCount; i++)
            {
                var row = Instantiate(rowTemplate, rowParent);
                row.name = $"TruckRow_T{i}";
                row.gameObject.SetActive(true);
                row.Bind(i);
                rows.Add(row);
            }

            refuelButton.onClick.AddListener(() => UIManager.instance.ShowRefuel(0, null));
            fleet.OnChanged += Refresh;
            CurrencyManager.instance.OnCurrencyAmountChanged += OnCurrencyChanged;
            Refresh();
        }

        private void OnEnable()
        {
            if (rows.Count > 0) Refresh();
        }

        private void OnCurrencyChanged(CurrencyType type, long amount)
        {
            if (isActiveAndEnabled) Refresh();
        }

        private void Refresh()
        {
            var fleet = FleetManager.instance;
            TruckData truck = fleet.CurrentTruck;
            currentTierText.text = $"T{fleet.OwnedTier}";
            currentNameText.text = truck.truckName;
            currentStatsText.text = Stats(truck);
            currentDescriptionText.text = truck.description;
            if (currentTruckImage)
            {
                currentTruckImage.sprite = truck.sprite;
                currentTruckImage.gameObject.SetActive(truck.sprite != null);
            }

            foreach (var row in rows) row.Refresh();

            var currency = CurrencyManager.instance;
            long unit = CurrencyManager.FuelUnit;
            long missing = fleet.MissingFuel;
            fuelText.text = $"<sprite=4>{currency.GetCurrencyAmount(CurrencyType.Fuel) / unit:N0} / {currency.GetFuelCap / unit:N0}";
            refuelButtonText.text = missing > 0 ? $"FILL UP <sprite=0>{NumberFormat.Compact(fleet.RefuelCost(missing))}" : "TANK FULL";
            refuelButton.interactable = missing > 0 && fleet.AffordableFuel() > 0;
        }

        /// <summary>"Pay x1.35  Fuel x0.90  Tank +250"</summary>
        public static string Stats(TruckData truck)
        {
            string tank = truck.fuelTankBonus > 0 ? $"   Tank <b>+{truck.fuelTankBonus:N0}</b>" : "";
            return $"Pay <b>x{truck.rewardMultiplier:0.00}</b>   Fuel <b>x{truck.fuelMultiplier:0.00}</b>{tank}";
        }
    }
}
