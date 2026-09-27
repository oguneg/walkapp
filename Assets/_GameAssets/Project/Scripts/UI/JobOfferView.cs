using DG.Tweening;
using UnityEngine;
using TMPro;
using UnityEngine.Events;

namespace OgunWorks.UI
{
    public class JobOfferView : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI jobTypeText, cargoTypeText, incomePerStepText, incomePerFuelText, distanceText, expRewardText, stepsText, timeText, costText, rewardText;
        [SerializeField] private UnityEngine.UI.Image accentImage;
        [SerializeField] private Color shortColor = new Color32(0x6B, 0xCB, 0x77, 0xFF);
        [SerializeField] private Color mediumColor = new Color32(0xF2, 0xA5, 0x41, 0xFF);
        [SerializeField] private Color longColor = new Color32(0xE4, 0x57, 0x2E, 0xFF);
        [SerializeField] private Color fuelOkColor = Color.white;
        [SerializeField] private Color fuelShortColor = new Color32(0xFF, 0x8A, 0x80, 0xFF);

        public UnityAction<JobOfferView, bool> OnJobResponse;
        public JobData assignedJob = null;
        public bool isEmpty = true;

        // Offers made before the saved fuel loaded (first frame) must not stay red.
        private void OnEnable() => RefreshAffordability();

        public void AssignJob(JobData job)
        {
            assignedJob = job;
            isEmpty = false;

            jobTypeText.text = $"{job.jobType.ToString().ToUpperInvariant()} · {job.distance:N0} KM";
            cargoTypeText.text = job.cargoType.ToString();
            stepsText.text = $"<sprite=1>{job.steps:N0}";
            costText.text = $"<sprite=4>{job.fuelCost / CurrencyManager.FuelUnit:N0}";
            expRewardText.text = $"<sprite=3>{job.experience:N0}";
            rewardText.text = $"<sprite=0>{job.reward:N0}";
            incomePerStepText.text = $"{job.reward / (float)Mathf.Max(1, job.steps):0.00} per step";
            if (incomePerFuelText) incomePerFuelText.text = $"<sprite=0>{job.reward * 1f / Mathf.Max(1, job.fuelCost / CurrencyManager.FuelUnit):F2} per <sprite=4>";
            if (distanceText) distanceText.text = $"{job.distance}km";
            if (timeText) timeText.text = $"<sprite=5>{FormatLimit(job.timeInMinutes)}";

            if (accentImage)
            {
                accentImage.color = job.jobType == JobType.Short ? shortColor
                    : job.jobType == JobType.Medium ? mediumColor
                    : longColor;
            }

            RefreshAffordability();
            gameObject.SetActive(true);
        }

        /// <summary>Time limit at a glance: "45m", "1h 30m", "8h", "5d 12h".</summary>
        public static string FormatLimit(int minutes)
        {
            if (minutes < 60) return $"{minutes}m";
            if (minutes < 48 * 60) return minutes % 60 == 0 ? $"{minutes / 60}h" : $"{minutes / 60}h {minutes % 60}m";
            int hours = minutes / 60;
            return hours % 24 == 0 ? $"{hours / 24}d" : $"{hours / 24}d {hours % 24}h";
        }

        /// <summary>Fuel cost turns red when the tank can't cover it (accepting then offers a refuel).</summary>
        public void RefreshAffordability()
        {
            if (assignedJob == null || !costText) return;
            bool canAfford = CurrencyManager.instance.CanAfford(CurrencyType.Fuel, assignedJob.fuelCost);
            costText.color = canAfford ? fuelOkColor : fuelShortColor;
        }

        public void Deactivate()
        {
            assignedJob = null;
            isEmpty = true;
            transform.DOScale(0, 0.2f).SetEase(Ease.InOutSine).OnComplete(() =>
            {
                gameObject.SetActive(false);
                transform.SetAsLastSibling();
                transform.localScale = Vector3.one;
            });
        }

        public void OnResponseButton(bool isAccepted)
        {
            OnJobResponse?.Invoke(this, isAccepted);
        }
    }
}
