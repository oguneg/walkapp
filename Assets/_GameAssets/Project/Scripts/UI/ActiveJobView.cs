using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace OgunWorks.UI
{
    public class ActiveJobView : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI jobTypeText,
            cargoTypeText,
            distanceText,
            stepsText,
            timeText,
            stepsLeftText,
            fuelCostText,
            rewardText,
            timeLeftText;

        [SerializeField] private TextMeshProUGUI titleText, hintText, bonusText, claimButtonText;

        public UnityAction<ActiveJobView, bool> OnJobResponse;
        public ActiveJobSaveData assignedJob = null;
        public bool isEmpty = true;
        public Button claimButton;
        [SerializeField] private Image stepProgressBar, timeProgressBar;

        public void AssignJob(ActiveJobSaveData job)
        {
            assignedJob = job;
            isEmpty = false;

            JobData data = job.jobData;
            if (titleText) titleText.text = "JOB IN PROGRESS";
            jobTypeText.text = $"{data.jobType.ToString().ToUpperInvariant()} · {data.distance:N0} KM";
            cargoTypeText.text = data.cargoType.ToString();
            if (distanceText) distanceText.text = $"<sprite=3>{data.experience:N0}";
            stepsText.text = $"<sprite=1>{data.steps:N0}";
            fuelCostText.text = $"<sprite=4>{data.fuelCost / CurrencyManager.FuelUnit:N0}";
            rewardText.text = $"<sprite=0>{data.reward:N0}";

            claimButton.interactable = false;
            gameObject.SetActive(true);
            assignedJob.state = JobState.Active;
            UpdateStatus();
        }

        public void UpdateStatus()
        {
            if (assignedJob == null || assignedJob.state != JobState.Active) return;

            long total = assignedJob.jobData.steps;
            long left = System.Math.Max(0, assignedJob.stepsLeft);
            stepProgressBar.fillAmount = total <= 0 ? 1f : 1f - left / (float)total;
            stepsLeftText.text = $"<sprite=1>{total - left:N0} / {total:N0}";
            if (hintText)
            {
                long bank = CurrencyManager.instance.GetCurrencyAmount(CurrencyType.BankedStep);
                hintText.text = bank > 0 ? $"{left:N0} to go · banked steps double your speed" : $"{left:N0} to go";
            }

            RefreshBonus();
            if (left <= 0) CompleteJob();
        }

        // Momentum bonus is applied at payout, so show what claiming would pay right now.
        public void RefreshBonus()
        {
            if (assignedJob == null) return;
            var daily = DailyBonusManager.instance;
            long reward = assignedJob.jobData.reward;
            long extra = daily.ApplyBonus(reward) - reward;
            if (bonusText) bonusText.text = extra > 0 ? $"+{extra:N0} momentum (+{daily.BonusPercent * 100:0}%)" : "";
            if (claimButtonText)
                claimButtonText.text = assignedJob.state == JobState.Claimable
                    ? $"CLAIM <sprite=0>{NumberFormat.Compact(daily.ApplyBonus(reward))}"
                    : "CLAIM";
        }

        private DailyBonusManager daily;

        private void OnEnable()
        {
            daily = DailyBonusManager.instance;
            daily.OnChanged += RefreshBonus;
            RefreshBonus();
        }

        // Cached reference: touching a MonoSingleton's instance during teardown can spawn a temporary one.
        private void OnDisable()
        {
            if (daily != null) daily.OnChanged -= RefreshBonus;
        }

        private void CompleteJob()
        {
            assignedJob.state = JobState.Claimable;
            stepProgressBar.fillAmount = 1f;
            if (titleText) titleText.text = "DELIVERED!";
            if (hintText) hintText.text = "Claim your pay.";
            claimButton.interactable = true;
            RefreshBonus();
            claimButton.transform.DOKill(true);
            claimButton.transform.DOPunchScale(Vector3.one * 0.12f, 0.4f, 6);
            UIManager.instance.SetActiveJobTabButtonNotificationLight(true);
        }

        public void OnResponseButton(bool isAccepted)
        {
            OnJobResponse?.Invoke(this, isAccepted);
        }

        public void ClearJobView()
        {
            assignedJob = null;
            StopAllCoroutines();
            isEmpty = true;
            gameObject.SetActive(false);
        }
    }
}
