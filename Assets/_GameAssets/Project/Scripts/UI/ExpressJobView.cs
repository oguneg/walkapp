using System;
using DG.Tweening;
using TMPro;
using UnityEngine;

namespace OgunWorks.UI
{
    /// <summary>
    /// Express deliveries, in two places:
    ///   Board  (top of the Job List): idle (when the next offer comes, dispatcher call) and offer (accept/decline).
    ///   Active (Active tab): the accepted job - countdown, progress, pace, then claim or acknowledge a miss.
    /// UIManager shows the board slot while there's no express job and the Active card while there is one.
    /// </summary>
    public class ExpressJobView : MonoBehaviour
    {
        public enum Slot { Board, Active }

        [SerializeField] private Slot slot = Slot.Board;
        [SerializeField] private TextMeshProUGUI titleText, detailText, timerText, timerLabelText;
        [SerializeField] private TextMeshProUGUI progressText, hintText, rewardText;
        [SerializeField] private UnityEngine.UI.Image panelImage, progressFill;
        [SerializeField] private GameObject progressGroup;
        [SerializeField] private UnityEngine.UI.Button primaryButton, secondaryButton;
        [SerializeField] private TextMeshProUGUI primaryButtonText, secondaryButtonText;

        [Header("Panel tint per state")]
        [SerializeField] private Color idleColor = new Color32(0x6E, 0x8F, 0xB3, 0xFF);
        [SerializeField] private Color offerColor = new Color32(0xE8, 0x89, 0x2B, 0xFF);
        [SerializeField] private Color activeColor = new Color32(0xD2, 0x65, 0x1F, 0xFF);
        [SerializeField] private Color completedColor = new Color32(0x3E, 0x9A, 0x4B, 0xFF);
        [SerializeField] private Color failedColor = new Color32(0xA8, 0x48, 0x3F, 0xFF);

        [Header("Height (the parent layout group stacks the regular job below)")]
        [SerializeField] private float compactHeight = 300f;
        [SerializeField] private float fullHeight = 470f;
        [Tooltip("Offers have no progress bar, so the card can be shorter.")]
        [SerializeField] private float offerHeight = 350f;

        private ExpressJobManager manager;
        private ExpressStatus? lastStatus;

        private void Awake()
        {
            primaryButton.onClick.AddListener(OnPrimary);
            secondaryButton.onClick.AddListener(OnSecondary);
        }

        private void OnEnable()
        {
            manager = ExpressJobManager.instance;
            manager.OnChanged += Refresh;
            manager.OnTick += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            if (manager == null) return;
            manager.OnChanged -= Refresh;
            manager.OnTick -= Refresh;
        }

        private void Refresh()
        {
            if (slot == Slot.Active)
            {
                ExpressJob job = manager.Job;
                if (job != null) ShowJob(job);
                return;
            }

            if (manager.HasOffer) ShowOffer(manager.Offer);
            else ShowIdle();
        }

        private void ShowIdle()
        {
            lastStatus = null;
            SetPanel(idleColor, compactHeight);
            titleText.text = "EXPRESS DELIVERIES";
            detailText.text = manager.CompletedCount > 0
                ? $"Rush jobs that pay 3-5x.\nDelivered so far: <b>{manager.CompletedCount}</b>"
                : "Rush jobs that pay 3-5x.\nCan't wait? Call the dispatcher.";
            TimeSpan eta = manager.NextOfferUtc - GameClock.UtcNow;
            timerText.text = eta > TimeSpan.Zero ? GameClock.FormatDuration(eta) : "soon";
            timerLabelText.text = "next offer";
            rewardText.text = "";
            progressGroup.SetActive(false);

            long cost = manager.DispatcherCost;
            SetButtons($"CALL <sprite=0>{NumberFormat.Compact(cost)}", null);
            primaryButton.interactable = manager.CanCallDispatcher && CurrencyManager.instance.CanAfford(CurrencyType.Coin, cost);
        }

        private void ShowOffer(ExpressOffer offer)
        {
            lastStatus = null;
            SetPanel(offerColor, offerHeight);
            titleText.text = "EXPRESS OFFER";
            detailText.text = $"{offer.cargoType}: walk <b>{offer.targetSteps:N0}</b> steps\nwithin <b>{offer.durationMinutes} min</b> of accepting";
            timerText.text = GameClock.FormatClock(GameClock.FromUnix(offer.expiresUnix) - GameClock.UtcNow);
            timerLabelText.text = "to accept";
            rewardText.text = Reward(offer);
            progressGroup.SetActive(false);
            SetButtons("ACCEPT", "DECLINE");
        }

        private void ShowJob(ExpressJob job)
        {
            ExpressOffer offer = job.offer;
            progressGroup.SetActive(true);
            progressFill.fillAmount = job.Progress01;
            progressText.text = $"<sprite=1>{job.bestProgress:N0} / {offer.targetSteps:N0}";
            rewardText.text = Reward(offer);

            switch (job.status)
            {
                case ExpressStatus.Active:
                    SetPanel(activeColor, fullHeight);
                    titleText.text = "EXPRESS DELIVERY";
                    detailText.text = $"{offer.cargoType}: {offer.targetSteps:N0} steps in {offer.durationMinutes} min";
                    timerText.text = GameClock.FormatClock(job.TimeLeft);
                    timerLabelText.text = "left";
                    hintText.text = PaceHint(job);
                    SetButtons(null, "GIVE UP");
                    break;

                case ExpressStatus.Resolving:
                    SetPanel(activeColor, fullHeight);
                    titleText.text = "EXPRESS DELIVERY";
                    timerText.text = "00:00";
                    timerLabelText.text = "time's up";
                    hintText.text = "Checking your steps...";
                    SetButtons(null, null);
                    break;

                case ExpressStatus.Completed:
                    SetPanel(completedColor, fullHeight);
                    titleText.text = "DELIVERED!";
                    detailText.text = $"{offer.cargoType} arrived on time.";
                    timerText.text = "DONE";
                    timerLabelText.text = "";
                    progressFill.fillAmount = 1f;
                    progressText.text = $"<sprite=1>{Math.Max(job.finalSteps, job.bestProgress):N0} / {offer.targetSteps:N0}";
                    hintText.text = "";
                    SetButtons($"CLAIM <sprite=0>{NumberFormat.Compact(DailyBonusManager.instance.ApplyBonus(offer.reward))}", null);
                    break;

                case ExpressStatus.Failed:
                    SetPanel(failedColor, fullHeight);
                    titleText.text = "MISSED THE DEADLINE";
                    detailText.text = $"{offer.cargoType} arrived late. {job.StepsLeft:N0} steps short.";
                    timerText.text = "LATE";
                    timerLabelText.text = "";
                    hintText.text = "";
                    SetButtons("OK", null);
                    break;
            }

            if (lastStatus != job.status && lastStatus.HasValue && job.status == ExpressStatus.Completed)
                panelImage.transform.DOPunchScale(Vector3.one * 0.06f, 0.4f, 6).SetTarget(this);
            lastStatus = job.status;
        }

        private static string Reward(ExpressOffer offer) =>
            $"<sprite=0>{offer.reward:N0}   <sprite=3>{offer.experience:N0}";

        private static string PaceHint(ExpressJob job)
        {
            long left = job.StepsLeft;
            if (left <= 0) return "Target reached!";

            double minutes = Math.Max(1.0, job.TimeLeft.TotalMinutes);
            int pace = (int)Math.Ceiling(left / minutes);
            if (pace > 130) return $"Needs {pace} steps/min. Run!";
            if (pace > 90) return $"Walk fast: ~{pace} steps/min";
            return $"Walk ~{pace} steps/min to make it";
        }

        private void SetPanel(Color color, float height)
        {
            if (panelImage) panelImage.color = color;

            var rect = (RectTransform)transform;
            if (Mathf.Approximately(rect.sizeDelta.y, height)) return;
            rect.sizeDelta = new Vector2(rect.sizeDelta.x, height);
            if (rect.parent is RectTransform parent)
                UnityEngine.UI.LayoutRebuilder.MarkLayoutForRebuild(parent);
        }

        private void SetButtons(string primary, string secondary)
        {
            primaryButton.interactable = true;
            primaryButton.gameObject.SetActive(primary != null);
            if (primary != null) primaryButtonText.text = primary;
            secondaryButton.gameObject.SetActive(secondary != null);
            if (secondary != null) secondaryButtonText.text = secondary;
        }

        private void OnPrimary()
        {
            ExpressJob job = manager.Job;
            if (job == null)
            {
                if (manager.HasOffer)
                {
                    UIManager.instance.AcceptExpressOffer();
                }
                else if (!manager.CallDispatcher())
                {
                    AudioManager.instance.PlaySound(SoundType.Fail);
                    return;
                }
            }
            else if (job.status == ExpressStatus.Completed)
            {
                manager.Claim();
            }
            else if (job.status == ExpressStatus.Failed)
            {
                manager.Dismiss();
            }

            AudioManager.instance.PlaySound(SoundType.Button);
            Refresh();
        }

        private void OnSecondary()
        {
            ExpressJob job = manager.Job;
            if (job == null)
            {
                if (manager.HasOffer) manager.DeclineOffer();
            }
            else if (job.status == ExpressStatus.Active)
            {
                // Same prompt as abandoning a regular job, so a stray tap doesn't throw away an hour of walking.
                UIManager.instance.ShowConfirm("GIVE UP EXPRESS?",
                    $"Drop the <b>{job.offer.cargoType}</b> rush delivery? The steps you walked stay in your step bank.",
                    "GIVE UP", "KEEP GOING",
                    () => manager.Abandon());
            }

            AudioManager.instance.PlaySound(SoundType.Button);
            Refresh();
        }
    }
}
