using System;
using UnityEngine;
using TMPro;
using UnityEngine.Events;

namespace OgunWorks.UI
{
    public class JobOfferView : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI jobTypeText, cargoTypeText, incomePerStepText, incomePerFuelText, distanceText, stepsText, timeText, costText, rewardText;
        public UnityAction<JobOfferView, bool> OnJobResponse;
        public JobData assignedJob = null;
        public bool isEmpty = true;
        
        public void AssignJob(JobData job)
        {
            assignedJob = job;
            isEmpty = false;
            jobTypeText.text = job.jobType.ToString();
            incomePerStepText.text = $"<sprite=0>{job.reward * 1f / job.steps :F2} per <sprite=1>";
            incomePerFuelText.text = $"<sprite=0>{job.reward * 1f / (job.fuelCost/1000) :F2} per <sprite=4>";
            cargoTypeText.text = job.cargoType.ToString();
            costText.text = $"-<sprite=4>{job.fuelCost / 1000}";
            rewardText.text = $"<sprite=0>{job.reward:N0}";
            distanceText.text = $"{job.distance}km";
            stepsText.text = $"<sprite=1>{job.steps:N0}";
            
            int hours = job.timeInMinutes / 60;
            int minutes = job.timeInMinutes % 60;
            timeText.text = (hours > 0) ? $"{hours}:{minutes:00} hrs" : $"{minutes} min";
            
            gameObject.SetActive(true);
        }


        public void Deactivate()
        {
            assignedJob = null;
            isEmpty = true;
            transform.SetAsLastSibling();
            gameObject.SetActive(false);
        }
        
        public void OnResponseButton(bool isAccepted)
        {
            OnJobResponse?.Invoke(this, isAccepted);
        }
    }
}