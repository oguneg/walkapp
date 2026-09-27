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

        // One regular job at a time: accepting used to silently replace the running job (and its progress).
        if (JobManager.instance.activeJob != null)
        {
            AudioManager.instance.PlaySound(SoundType.Fail);
            UIManager.instance.ForceTab(TabType.ActiveJobs);
            return;
        }

        var job = jobOfferView.assignedJob;
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
            firstAvailableView.transform.SetSiblingIndex(activeJobCount);
            activeJobCount++;
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
