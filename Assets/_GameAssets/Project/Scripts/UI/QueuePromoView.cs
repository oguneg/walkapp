using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OgunWorks.UI
{
    /// <summary>
    /// Under the running job on the Active tab, in the queue card's place, for players who can buy the Job Queue
    /// but haven't: what a queue does, and a button to get it. UIManager shows it (JobManager.CanPromoteQueue).
    /// </summary>
    public class QueuePromoView : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI titleText, bodyText, buttonLabel;
        [SerializeField] private Button button;

        private void Awake()
        {
            button.onClick.AddListener(() =>
            {
                AudioManager.instance.PlaySound(SoundType.Button);
                UIManager.instance.OpenQueueStore();
            });
        }

        public void Refresh()
        {
            var jobs = JobManager.instance;
            var job = jobs.activeJob;
            var upgrade = jobs.QueueUpgrade;
            if (job == null || upgrade == null) return;

            titleText.text = "Line up your next job";
            bodyText.text =
                $"With a <b>Job Queue</b>, your next job starts the moment <b>{job.jobData.cargoType}</b> is done, even with your phone in your pocket. No steps lost to a full depot.";
            buttonLabel.text = $"GET IT <sprite=0>{NumberFormat.Compact(upgrade.CostAtLevel(0))}";
        }
    }
}
