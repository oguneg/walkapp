using System;
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
    }

    private void OnJobResponse(JobOfferView jobOfferView, bool isAccepted)
    {
        if (isAccepted)
        {
            if (CurrencyManager.instance.CanAfford(CurrencyType.Fuel, jobOfferView.assignedJob.fuelCost))
            {
                CurrencyManager.instance.AddCurrency(CurrencyType.Fuel, -jobOfferView.assignedJob.fuelCost);
                OnJobAccepted(jobOfferView);
            }
        }
        else
        {
            OnJobRemoved(jobOfferView);
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
    }

    private void OnJobRemoved(JobOfferView jobOfferView)
    {
        jobOfferView.Deactivate();
        activeJobCount--;
    }
}