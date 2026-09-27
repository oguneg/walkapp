using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OgunWorks.UI
{
    /// <summary>One truck in the dealership list: owned, driving, buyable next, or locked.</summary>
    public class TruckRowView : MonoBehaviour
    {
        [SerializeField] private Image background;
        [SerializeField] private TextMeshProUGUI tierText, nameText, statsText, stateText;
        [SerializeField] private Button buyButton;
        [SerializeField] private TextMeshProUGUI buyButtonText;
        [SerializeField] private Color ownedColor = new Color32(0x8D, 0xA9, 0xC4, 0xFF);
        [SerializeField] private Color drivingColor = new Color32(0x2B, 0x6C, 0xC4, 0xFF);
        [SerializeField] private Color nextColor = new Color32(0x3E, 0x9A, 0x4B, 0xFF);
        [SerializeField] private Color lockedColor = new Color32(0x6B, 0x7A, 0x90, 0xFF);

        private int tier;

        private void Awake()
        {
            buyButton.onClick.AddListener(OnBuy);
        }

        public void Bind(int truckTier)
        {
            tier = truckTier;
            Refresh();
        }

        public void Refresh()
        {
            var fleet = FleetManager.instance;
            TruckData truck = fleet.GetTruck(tier);
            int owned = fleet.OwnedTier;

            tierText.text = $"T{tier}";
            nameText.text = truck.truckName;
            statsText.text = FleetView.Stats(truck);

            bool isNext = tier == owned + 1;
            buyButton.gameObject.SetActive(isNext);
            stateText.gameObject.SetActive(!isNext);

            if (tier < owned)
            {
                background.color = ownedColor;
                stateText.text = "OWNED";
            }
            else if (tier == owned)
            {
                background.color = drivingColor;
                stateText.text = "DRIVING";
            }
            else if (isNext)
            {
                background.color = nextColor;
                buyButtonText.text = $"<sprite=0>{NumberFormat.Compact(truck.price)}";
                buyButton.interactable = fleet.CanAffordNextTruck;
            }
            else
            {
                background.color = lockedColor;
                stateText.text = $"<sprite=0>{NumberFormat.Compact(truck.price)}";
            }
        }

        private void OnBuy()
        {
            if (FleetManager.instance.BuyNextTruck())
            {
                AudioManager.instance.PlaySound(SoundType.Success);
                transform.DOKill(true);
                transform.DOPunchScale(Vector3.one * 0.05f, 0.35f, 6);
            }
            else
            {
                AudioManager.instance.PlaySound(SoundType.Fail);
            }
        }
    }
}
