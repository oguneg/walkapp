using System.Collections.Generic;
using System.Linq;
using OgunWorks.UI;
using UnityEngine;

public class JobListView : MonoBehaviour
{
    [SerializeField] private List<JobOfferView> jobOfferViews;
    private int activeJobCount = 0;
    private void Awake()
    {
        foreach (var element in jobOfferViews)
        {
            element.OnJobResponse += OnJobResponse;
        }

        CurrencyManager.instance.OnCurrencyAmountChanged += OnCurrencyChanged;
    }

    private void OnCurrencyChanged(CurrencyType type, long amount)
    {
        if (type != CurrencyType.Fuel) return;
        foreach (var view in jobOfferViews)
            if (!view.isEmpty) view.RefreshAffordability();
    }

    private void OnJobResponse(JobOfferView jobOfferView, bool isAccepted)
    {
        if (!isAccepted)
        {
            OnJobRemoved(jobOfferView);
            return;
        }

        var job = jobOfferView.assignedJob;

        var jobs = JobManager.instance;

        // A queue slot (upgrade) lets it wait behind the running job instead.
        if (jobs.CanQueue)
        {
            AskToQueue(jobOfferView, job);
            return;
        }

        if (jobs.QueueFull && jobs.activeJob != null && jobs.activeJob.IsRunning)
        {
            UIManager.instance.ShowMessage("QUEUE FULL",
                $"<b>{jobs.queuedJob.jobData.cargoType}</b> is already waiting in your queue. It starts when <b>{jobs.activeJob.jobData.cargoType}</b> is done.",
                "OK");
            return;
        }

        // One job at a time, regular or express: free the slot first (UIManager asks when something would be lost).
        if (!jobs.JobSlotFree)
        {
            UIManager.instance.RequestJobSlot(job.cargoType.ToString(), () =>
            {
                if (jobOfferView.assignedJob == job && JobManager.instance.JobSlotFree) OnJobResponse(jobOfferView, true);
            });
            return;
        }

        TakeJob(jobOfferView, job, queue: false);
    }

    // The clock rule, spelled out: the queued job's deadline runs from now, its steps only after the current job.
    private void AskToQueue(JobOfferView jobOfferView, JobData job)
    {
        var current = JobManager.instance.activeJob;
        string limit = JobOfferView.FormatLimit(job.timeInMinutes);
        UIManager.instance.ShowConfirm("QUEUE THIS JOB?",
            $"<b>{job.cargoType}</b> starts as soon as <b>{current.jobData.cargoType}</b> is done.\n\n" +
            $"Its {limit} deadline starts now: walk {current.stepsLeft:N0} more for <b>{current.jobData.cargoType}</b>, then {job.steps:N0} for <b>{job.cargoType}</b>, all within {limit}.",
            "QUEUE IT", "CANCEL",
            () =>
            {
                if (jobOfferView.assignedJob == job) TakeJob(jobOfferView, job, queue: true);
            });
    }

    // Pay the fuel, then accept or queue. Not enough fuel: offer the fuel station first.
    private void TakeJob(JobOfferView jobOfferView, JobData job, bool queue)
    {
        var currency = CurrencyManager.instance;
        if (!currency.CanAfford(CurrencyType.Fuel, job.fuelCost))
        {
            UIManager.instance.ShowRefuel(job.fuelCost, () =>
            {
                if (jobOfferView.assignedJob == job) TakeJob(jobOfferView, job, queue);
            });
            return;
        }

        currency.AddCurrency(CurrencyType.Fuel, -job.fuelCost);
        var jobs = JobManager.instance;
        bool taken = queue ? jobs.QueueJob(job) : jobs.AcceptJob(job);
        if (!taken)
        {
            currency.AddCurrency(CurrencyType.Fuel, job.fuelCost); // the slot filled up meanwhile
            AudioManager.instance.PlaySound(SoundType.Fail);
            return;
        }

        jobOfferView.Deactivate();
        activeJobCount--;
        AudioManager.instance.PlaySound(SoundType.Button);
    }

    public void AddJob(JobData jobData)
    {
        var firstAvailableView = jobOfferViews.FirstOrDefault(o => o.isEmpty);
        if (firstAvailableView != null)
        {
            firstAvailableView.AssignJob(jobData);
            firstAvailableView.transform.SetSiblingIndex(PinnedChildren + activeJobCount);
            activeJobCount++;
        }
    }

    /// <summary>A newly unlocked kind of job: on top of the list, replacing the bottom offer if it's full.</summary>
    public void AddFeaturedJob(JobData jobData)
    {
        var view = jobOfferViews.FirstOrDefault(o => o.isEmpty);
        if (view != null)
        {
            activeJobCount++;
        }
        else
        {
            view = jobOfferViews.Where(o => !o.isEmpty).OrderBy(o => o.transform.GetSiblingIndex()).LastOrDefault();
            if (view == null) return;
        }

        view.AssignJob(jobData);
        view.transform.SetSiblingIndex(PinnedChildren);
    }

    // Children that aren't offers (the express slot) stay pinned above them.
    private int PinnedChildren
    {
        get
        {
            int n = 0;
            foreach (Transform child in transform)
                if (!child.GetComponent<JobOfferView>()) n++;
            return n;
        }
    }

    private void OnJobRemoved(JobOfferView jobOfferView)
    {
        jobOfferView.Deactivate();
        activeJobCount--;
        AudioManager.instance.PlaySound(SoundType.Button);
    }
}
