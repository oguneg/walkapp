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

        // One job at a time, regular or express: free the slot first (UIManager asks when something would be lost).
        if (!JobManager.instance.JobSlotFree)
        {
            UIManager.instance.RequestJobSlot(job.cargoType.ToString(), () =>
            {
                if (jobOfferView.assignedJob == job && JobManager.instance.JobSlotFree) OnJobResponse(jobOfferView, true);
            });
            return;
        }

        if (CurrencyManager.instance.CanAfford(CurrencyType.Fuel, job.fuelCost))
        {
            CurrencyManager.instance.AddCurrency(CurrencyType.Fuel, -job.fuelCost);
            OnJobAccepted(jobOfferView);
        }
        else
        {
            // Not enough fuel: offer the fuel station, then take the job if the player refuels.
            UIManager.instance.ShowRefuel(job.fuelCost, () =>
            {
                if (jobOfferView.assignedJob == job) OnJobResponse(jobOfferView, true);
            });
        }
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

    private void OnJobAccepted(JobOfferView jobOfferView)
    {
        JobManager.instance.AcceptJob(jobOfferView.assignedJob);
        jobOfferView.Deactivate();
        activeJobCount--;
        AudioManager.instance.PlaySound(SoundType.Button);
    }

    private void OnJobRemoved(JobOfferView jobOfferView)
    {
        jobOfferView.Deactivate();
        activeJobCount--;
        AudioManager.instance.PlaySound(SoundType.Button);
    }
}
