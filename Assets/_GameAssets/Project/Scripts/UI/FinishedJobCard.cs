using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OgunWorks.UI
{
    /// <summary>
    /// A small card for a job that ended while a queued job took over the main card: delivered (claim) or missed (OK).
    /// It sits where the queue card was, so a finished job and the next one can both be ready to claim on one screen.
    /// UIManager clones one per job in JobManager.finishedJobs.
    /// </summary>
    public class FinishedJobCard : MonoBehaviour
    {
        [SerializeField] private Image panelImage;
        [SerializeField] private TextMeshProUGUI titleText, bodyText, buttonLabel;
        [SerializeField] private Button button;
        [SerializeField] private Color deliveredColor = new Color32(0x3E, 0x9A, 0x4B, 0xFF);
        [SerializeField] private Color failedColor = new Color32(0xA8, 0x48, 0x3F, 0xFF);
        [SerializeField] private Color claimButtonColor = new Color32(0xFF, 0xB4, 0x01, 0xFF);
        [SerializeField] private Color claimLabelColor = new Color32(0x0B, 0x25, 0x45, 0xFF);

        public ActiveJobSaveData Job { get; private set; }

        private DailyBonusManager daily;

        private void Awake()
        {
            button.onClick.AddListener(OnButton);
        }

        // The claim amount includes the momentum bonus, which can change while the card waits.
        private void OnEnable()
        {
            daily = DailyBonusManager.instance;
            daily.OnChanged += Rebind;
            Rebind();
        }

        private void OnDisable()
        {
            if (daily != null) daily.OnChanged -= Rebind;
        }

        private void Rebind()
        {
            if (Job != null) Bind(Job, false);
        }

        public void Bind(ActiveJobSaveData job, bool isNew)
        {
            Job = job;
            JobData data = job.jobData;
            bool delivered = job.state == JobState.Claimable;

            panelImage.color = delivered ? deliveredColor : failedColor;
            titleText.text = delivered ? $"DELIVERED! {data.cargoType}" : $"MISSED: {data.cargoType}";
            bodyText.text = delivered
                ? $"{data.jobType.ToString().ToUpperInvariant()} · <sprite=1>{data.steps:N0} · <sprite=3>{data.experience:N0}"
                : $"{job.stepsLeft:N0} steps short of {data.steps:N0}";
            buttonLabel.text = delivered
                ? $"CLAIM <sprite=0>{NumberFormat.Compact(DailyBonusManager.instance.ApplyBonus(data.reward))}"
                : "OK";
            button.image.color = delivered ? claimButtonColor : new Color32(0x0B, 0x25, 0x45, 0x73);
            buttonLabel.color = delivered ? claimLabelColor : Color.white;

            if (isNew && delivered && isActiveAndEnabled)
            {
                button.transform.DOKill(true);
                button.transform.DOPunchScale(Vector3.one * 0.12f, 0.4f, 6);
            }
        }

        private void OnButton()
        {
            if (Job == null) return;
            bool delivered = Job.state == JobState.Claimable;
            AudioManager.instance.PlaySound(delivered ? SoundType.Success : SoundType.Button);
            JobManager.instance.CollectFinished(Job);
        }
    }
}
