using System;
using System.Collections.Generic;
using System.Linq;
using OgunWorks.UI;
using UnityEngine;

public class JobListView : MonoBehaviour
{
    [SerializeField] private List<JobOfferView>  jobOfferViews;
    
    private void Awake()
    {
        foreach (var element in jobOfferViews)
        {
            element.OnJobResponse+= OnJobResponse;
        }
    }

    private void OnJobResponse(JobOfferView jobOfferView, bool isAccepted)
    {
        if (isAccepted)
        {
            OnJobAccepted(jobOfferView);
        }
        else
        {
            OnJobRemoved(jobOfferView);
        }
    }

    public void AddJob(JobData jobData)
    {
        jobOfferViews.FirstOrDefault(o => o.isEmpty)?.AssignJob(jobData);
    }

    private void OnJobAccepted(JobOfferView jobOfferView)
    {
        JobManager.instance.AcceptJob(jobOfferView.assignedJob);
        jobOfferView.Deactivate();
    }

    private void OnJobRemoved(JobOfferView jobOfferView)
    {
        jobOfferView.Deactivate();
    }
}
