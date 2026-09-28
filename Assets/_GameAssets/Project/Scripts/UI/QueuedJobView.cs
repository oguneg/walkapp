using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OgunWorks.UI
{
    /// <summary>
    /// The queued job, under the running one on the Active tab. Its deadline is already running; its steps start
    /// counting when the current job ends. UIManager shows it only while a job is queued.
    /// </summary>
    public class QueuedJobView : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI titleText, bodyText;
        [SerializeField] private Button removeButton;

        private void Awake()
        {
            if (removeButton) removeButton.onClick.AddListener(OnRemove);
        }

        private void OnEnable() => StartCoroutine(TickRoutine());

        private IEnumerator TickRoutine()
        {
            var wait = new WaitForSeconds(1f);
            while (true)
            {
                Refresh();
                yield return wait;
            }
        }

        public void Refresh()
        {
            var job = JobManager.instance.queuedJob;
            if (job == null) return;

            JobData data = job.jobData;
            titleText.text = $"Next up: {data.cargoType}";
            bodyText.text =
                $"{data.jobType.ToString().ToUpperInvariant()} · <sprite=1>{data.steps:N0} · <sprite=0>{data.reward:N0}\n" +
                $"<sprite=5> {GameClock.FormatDuration(job.TimeLeft)} left. The clock is running; steps count once the current job ends.";
        }

        private void OnRemove()
        {
            var job = JobManager.instance.queuedJob;
            if (job == null) return;
            UIManager.instance.ShowConfirm("REMOVE FROM QUEUE?",
                $"Take <b>{job.jobData.cargoType}</b> out of the queue? You get its fuel back.",
                "REMOVE", "KEEP",
                () => JobManager.instance.RemoveQueued());
        }
    }
}
