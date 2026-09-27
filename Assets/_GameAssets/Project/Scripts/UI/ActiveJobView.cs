using System.Collections;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace OgunWorks.UI
{
    /// <summary>
    /// The regular job on the Active tab. Three states, all decided by JobManager:
    /// running (progress, deadline countdown, burn rate), delivered (claim any time) and missed deadline (OK).
    /// </summary>
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
        [Tooltip("Right side of the deadline row: when the job is due.")]
        [SerializeField] private TextMeshProUGUI dueText;

        public UnityAction<ActiveJobView, bool> OnJobResponse;
        public ActiveJobSaveData assignedJob = null;
        public bool isEmpty = true;
        public Button claimButton;
        [SerializeField] private Button abandonButton;
        [SerializeField] private Image stepProgressBar, timeProgressBar;

        [Header("Burn rate")]
        [SerializeField] private GameObject burnRow;
        [SerializeField] private Slider burnSlider;
        [SerializeField] private TextMeshProUGUI burnValueText, burnInfoText;

        [Header("Empty state")]
        [Tooltip("Shown instead of this panel when there is no regular job.")]
        [SerializeField] private GameObject emptyState;
        [SerializeField] private TextMeshProUGUI emptyTitleText, emptyBodyText;

        [Header("Look per state")]
        [SerializeField] private Image panelImage;
        [SerializeField] private Color runningColor = new Color32(0x8D, 0xA9, 0xC4, 0xFF);
        [SerializeField] private Color deliveredColor = new Color32(0x3E, 0x9A, 0x4B, 0xFF);
        [SerializeField] private Color failedColor = new Color32(0xA8, 0x48, 0x3F, 0xFF);
        [SerializeField] private Color stepBarColor = new Color32(0x6B, 0xCB, 0x77, 0xFF);
        [Tooltip("Progress bar on a missed job: greyed out, it no longer counts.")]
        [SerializeField] private Color failedStepBarColor = new Color(1f, 1f, 1f, 0.45f);
        [Tooltip("Countdown colour once less than a fifth of the time is left.")]
        [SerializeField] private Color hurryColor = new Color32(0xFF, 0xD0, 0x8A, 0xFF);
        [Tooltip("Card height while running (burn rate row shown) and once settled (row hidden).")]
        [SerializeField] private float runningHeight = 740f;
        [SerializeField] private float settledHeight = 560f;

        private DailyBonusManager daily;
        private JobState? shownState;

        private void Awake()
        {
            if (burnSlider) burnSlider.onValueChanged.AddListener(v => JobManager.instance.BurnRate = v);
        }

        public void AssignJob(ActiveJobSaveData job)
        {
            assignedJob = job;
            isEmpty = false;
            shownState = null;

            JobData data = job.jobData;
            jobTypeText.text = $"{data.jobType.ToString().ToUpperInvariant()} · {data.distance:N0} KM";
            cargoTypeText.text = data.cargoType.ToString();
            if (distanceText) distanceText.text = $"<sprite=3>{data.experience:N0}";
            stepsText.text = $"<sprite=1>{data.steps:N0}";
            fuelCostText.text = $"<sprite=4>{data.fuelCost / CurrencyManager.FuelUnit:N0}";
            rewardText.text = $"<sprite=0>{data.reward:N0}";

            gameObject.SetActive(true);
            if (emptyState) emptyState.SetActive(false);
            UpdateStatus();
        }

        public void UpdateStatus()
        {
            if (assignedJob == null) return;

            long total = assignedJob.jobData.steps;
            long left = System.Math.Max(0, assignedJob.stepsLeft);
            stepProgressBar.fillAmount = total <= 0 ? 1f : 1f - left / (float)total;
            stepsLeftText.text = $"<sprite=1>{total - left:N0} / {total:N0}";

            JobState state = assignedJob.state;
            bool changed = shownState != state;
            if (changed) ApplyState(state, left);
            else if (state == JobState.Active && hintText) hintText.text = $"{left:N0} to go";

            RefreshTime();
            RefreshBurn();
            RefreshBonus();

            // Delivered just now (not when the tab opens on an old delivery): make the claim button pop.
            if (changed && shownState.HasValue && state == JobState.Claimable && isActiveAndEnabled)
            {
                claimButton.transform.DOKill(true);
                claimButton.transform.DOPunchScale(Vector3.one * 0.12f, 0.4f, 6);
            }

            shownState = state;
        }

        private void ApplyState(JobState state, long left)
        {
            bool running = state == JobState.Active;
            if (burnRow) burnRow.SetActive(running);
            if (abandonButton) abandonButton.gameObject.SetActive(running);
            claimButton.interactable = !running;
            stepProgressBar.color = state == JobState.Failed ? failedStepBarColor : stepBarColor;
            SetPanel(state == JobState.Claimable ? deliveredColor : state == JobState.Failed ? failedColor : runningColor,
                running ? runningHeight : settledHeight);

            switch (state)
            {
                case JobState.Claimable:
                    stepProgressBar.fillAmount = 1f;
                    if (titleText) titleText.text = "DELIVERED!";
                    if (hintText) hintText.text = "Claim your pay whenever you like.";
                    break;
                case JobState.Failed:
                    if (titleText) titleText.text = "MISSED THE DEADLINE";
                    if (hintText) hintText.text = $"{left:N0} steps short";
                    if (claimButtonText) claimButtonText.text = "OK";
                    break;
                default:
                    if (titleText) titleText.text = "JOB IN PROGRESS";
                    if (hintText) hintText.text = $"{left:N0} to go";
                    break;
            }
        }

        // Countdown row. Ticks every second while the tab is open.
        private void RefreshTime()
        {
            if (assignedJob == null || !timeLeftText) return;

            var job = assignedJob;
            if (timeProgressBar) timeProgressBar.fillAmount = job.TimeUsed01;
            timeLeftText.color = Color.white;

            switch (job.state)
            {
                case JobState.Claimable:
                    timeLeftText.text = "<sprite=5> Delivered in time";
                    if (dueText) dueText.text = "";
                    break;
                case JobState.Failed:
                    timeLeftText.text = "<sprite=5> Time's up";
                    if (dueText) dueText.text = $"was due {FormatDue(job.DeadlineUtc)}";
                    break;
                default:
                    if (job.IsPastDeadline)
                    {
                        timeLeftText.text = "<sprite=5> Time's up, checking steps...";
                        if (dueText) dueText.text = "";
                        break;
                    }

                    timeLeftText.text = $"<sprite=5> {GameClock.FormatDuration(job.TimeLeft)} left";
                    if (job.TimeUsed01 > 0.8f) timeLeftText.color = hurryColor;
                    if (dueText) dueText.text = $"due {FormatDue(job.DeadlineUtc)}";
                    break;
            }
        }

        /// <summary>"today 18:30", "tomorrow 09:00", "Tue 14:00" in the phone's local time.</summary>
        private static string FormatDue(System.DateTime utc)
        {
            System.DateTime local = utc.ToLocalTime();
            System.DateTime today = GameClock.UtcNow.ToLocalTime().Date;
            string time = local.ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture);
            if (local.Date == today) return $"today {time}";
            if (local.Date == today.AddDays(1)) return $"tomorrow {time}";
            return local.ToString("ddd HH:mm", System.Globalization.CultureInfo.InvariantCulture);
        }

        // Momentum bonus is applied at payout, so show what claiming would pay right now.
        public void RefreshBonus()
        {
            if (assignedJob == null) return;
            var bonus = DailyBonusManager.instance;
            long reward = assignedJob.jobData.reward;
            long extra = bonus.ApplyBonus(reward) - reward;
            bool failed = assignedJob.state == JobState.Failed;
            if (bonusText) bonusText.text = extra > 0 && !failed ? $"+{extra:N0} momentum (+{bonus.BonusPercent * 100:0}%)" : "";
            if (claimButtonText && !failed)
                claimButtonText.text = assignedJob.state == JobState.Claimable
                    ? $"CLAIM <sprite=0>{NumberFormat.Compact(bonus.ApplyBonus(reward))}"
                    : "CLAIM";
        }

        // Slider range follows the Burn Rate Booster upgrade; the text spells out what a walked step does.
        private void RefreshBurn()
        {
            if (!burnSlider || !burnSlider.gameObject.activeInHierarchy) return;
            var jobs = JobManager.instance;
            float rate = jobs.BurnRate;
            burnSlider.minValue = 1f;
            burnSlider.maxValue = jobs.MaxBurnRate;
            burnSlider.SetValueWithoutNotify(rate);

            float loss = jobs.BurnLoss(rate);
            burnValueText.text = loss > 0
                ? $"<b>{rate:0.0}x</b> <color=#FF9A8A>-{loss * 100:0}%</color>"
                : $"<b>{rate:0.0}x</b>";

            long bank = CurrencyManager.instance.GetCurrencyAmount(CurrencyType.BankedStep);
            float pulled = rate - 1f;
            if (pulled <= 0f)
                burnInfoText.text = "Bank untouched: only walked steps count.";
            else if (bank <= 0)
                burnInfoText.text = "Step bank is empty, so walked steps count 1:1.";
            else if (loss > 0)
                burnInfoText.text = $"Each step adds {pulled:0.0} from your bank, costing {pulled * (1 + loss):0.00}.";
            else
                burnInfoText.text = $"Each step adds {pulled:0.0} from your bank, no loss.";
        }

        private void OnEnable()
        {
            daily = DailyBonusManager.instance;
            daily.OnChanged += RefreshBonus;
            RefreshBonus();
            RefreshBurn(); // max rate may have changed in Upgrades
            StartCoroutine(TickRoutine());
        }

        // Cached reference: touching a MonoSingleton's instance during teardown can spawn a temporary one.
        private void OnDisable()
        {
            if (daily != null) daily.OnChanged -= RefreshBonus;
        }

        private IEnumerator TickRoutine()
        {
            var wait = new WaitForSeconds(1f);
            while (true)
            {
                RefreshTime();
                yield return wait;
            }
        }

        private void SetPanel(Color color, float height)
        {
            if (panelImage) panelImage.color = color;

            var rect = (RectTransform)transform;
            if (Mathf.Approximately(rect.sizeDelta.y, height)) return;
            rect.sizeDelta = new Vector2(rect.sizeDelta.x, height);
            if (rect.parent is RectTransform parent) LayoutRebuilder.MarkLayoutForRebuild(parent);
        }

        /// <summary>
        /// The big button: CLAIM once delivered, OK after a missed deadline (both wired as "true" in the prefab).
        /// The small one abandons a running job after a prompt.
        /// </summary>
        public void OnResponseButton(bool isAccepted)
        {
            if (assignedJob == null) return;

            if (isAccepted)
            {
                if (assignedJob.state == JobState.Claimable) OnJobResponse?.Invoke(this, true);
                else if (assignedJob.state == JobState.Failed) OnJobResponse?.Invoke(this, false);
                return;
            }

            UIManager.instance.ShowConfirm("ABANDON JOB?",
                $"Drop the <b>{assignedJob.jobData.cargoType}</b> delivery? Progress and the fuel you paid are lost.",
                "ABANDON", "KEEP GOING",
                () => { if (assignedJob != null && assignedJob.IsRunning) OnJobResponse?.Invoke(this, false); });
        }

        public void ClearJobView()
        {
            assignedJob = null;
            shownState = null;
            isEmpty = true;
            gameObject.SetActive(false);
            if (emptyState) emptyState.SetActive(true);
        }

        /// <summary>The empty state reads differently when an express job is on the card above it.</summary>
        public void SetExpressRunning(bool running)
        {
            if (emptyTitleText) emptyTitleText.text = running ? "No regular job" : "No job in progress";
            if (emptyBodyText)
                emptyBodyText.text = running
                    ? "Take one from the Job List too: the same steps count for both."
                    : "Pick one from the Job List. Until then, your steps go to the step bank.";
        }
    }
}
