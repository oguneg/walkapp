using System;
using UnityEngine;
using TMPro;
using UnityEngine.Events;

namespace OgunWorks.UI
{
    public class JobOfferView : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI jobTypeText, cargoTypeText, distanceText, stepsText, timeText;
        public UnityAction<JobOfferView, bool> OnJobResponse;
        public JobData assignedJob = null;
        public bool isEmpty = true;
        
        public void AssignJob(JobData job)
        {
            assignedJob = job;
            isEmpty = false;
            jobTypeText.text = job.jobType.ToString();
            cargoTypeText.text = job.cargoType.ToString();
            distanceText.text = $"{job.distance}km";
            stepsText.text = $"{job.steps:N0} steps";
            
            int hours = job.timeInMinutes / 60;
            int minutes = job.timeInMinutes % 60;
            timeText.text = (hours > 0) ? $"{hours}:{minutes:00} hrs" : $"{minutes} min";
            
            gameObject.SetActive(true);
        }

        public void Deactivate()
        {
            assignedJob = null;
            isEmpty = true;
            gameObject.SetActive(false);
            transform.SetAsLastSibling();
        }
        
        public void OnResponseButton(bool isAccepted)
        {
            OnJobResponse?.Invoke(this, isAccepted);
        }
    }
}