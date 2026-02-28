using System;
using System.Collections;
using UnityEngine;
using TMPro;
using UnityEngine.Events;
using UnityEngine.UI;

namespace OgunWorks.UI
{
    public class ActiveJobView : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI jobTypeText,
            cargoTypeText,
            distanceText,
            stepsText,
            timeText,
            stepsLeftText,
            fuelCostText,
            rewardText,
            timeLeftText;

        public UnityAction<ActiveJobView, bool> OnJobResponse;
        public ActiveJobSaveData assignedJob = null;
        public bool isEmpty = true;
        public Button claimButton;
        private Coroutine progressRoutine;
        long stepsLeft = 0;
        long timeLeftInSeconds = 0;
        [SerializeField] private Image stepProgressBar, timeProgressBar;

        public void AssignJob(ActiveJobSaveData job)
        {
            assignedJob = job;
            isEmpty = false;
            jobTypeText.text = job.jobData.jobType.ToString();
            cargoTypeText.text = job.jobData.cargoType.ToString();
            distanceText.text = $"{job.jobData.distance}km";
            stepsText.text = $"{job.jobData.steps:N0} steps";
            fuelCostText.text = $"{job.jobData.fuelCost / 1000} fuel";
            rewardText.text = $"${job.jobData.reward:N0}";

            int hours = job.jobData.timeInMinutes / 60;
            int minutes = job.jobData.timeInMinutes % 60;
            //timeText.text = (hours > 0) ? $"{hours}:{minutes:00} hrs" : $"{minutes} min";

            claimButton.interactable = false;
            gameObject.SetActive(true);
            assignedJob.state = JobState.Active;
            stepsLeft = job.jobData.steps;
            UpdateStatus();
        }
/*
        private void OnEnable()
        {
            if (!isEmpty)
            {
                progressRoutine = StartCoroutine(UpdateProgress());
            }
        }

        private void OnDisable()
        {
            if (progressRoutine != null)
            {
                StopCoroutine(progressRoutine);
            }
        }
           
        void OnApplicationPause(bool paused)
        {
            if (!paused)
            {
                UpdateTimeLeft();
                if (assignedJob.state == JobState.TimeOver)
                {
                    OnTimeOver();
                }
            }
        }
        
        public IEnumerator UpdateProgress()
        {
            WaitForSeconds wfs = new WaitForSeconds(1f);
            while (true)
            {
                UpdateStatus();
                yield return wfs;
            }
        }
*/

        public void UpdateStatus()
        {
            if (assignedJob.state == JobState.Active)
            {
                //UpdateStepsLeft();
                //UpdateTimeLeft();
                CheckForCompletion();
            }
        }
        
        private void UpdateStepsLeft()
        {
            stepProgressBar.fillAmount = 1f - stepsLeft * 1f / assignedJob.jobData.steps;
        }

        private void UpdateTimeLeft()
        {
            if (assignedJob.TimeRemainingSeconds < 0 && assignedJob.state != JobState.Claimable)
            {
                /*
                if (progressRoutine != null)
                {
                    StopCoroutine(progressRoutine);
                }
                */

                timeProgressBar.fillAmount = 1;
                timeLeftText.text = "Time Over - Job Failed";
                assignedJob.state = JobState.TimeOver;
                Debug.Log($"time over");

                OnTimeOver();
            }
            else
            {
                timeProgressBar.fillAmount =
                    1f - (float)assignedJob.TimeRemainingSeconds / (assignedJob.jobData.timeInMinutes * 60);
                timeLeftText.text = $"{FormatTimeRemaining(assignedJob.TimeRemainingSeconds)} until delivery";
            }
        }

        private void OnTimeOver()
        {
            Debug.Log($"time over - querying steps");

            //StepDisplayManager.instance.GetStepsInTimePeriod(assignedJob.AcceptTime, assignedJob.DeadlineTime,
            //    TimeOverResponse);
        }

        private void FailJob()
        {
            assignedJob.state = JobState.Failed;
            Debug.Log("job dummy failed");
        }

        private void TimeOverResponse(int steps)
        {
            Debug.Log($"{steps} steps taken between {assignedJob.AcceptTime} - {assignedJob.DeadlineTime}");
            if (steps >= assignedJob.jobData.steps)
            {
                CompleteJob();
            }
            else
            {
                FailJob();
            }
        }


        private void CheckForCompletion()
        {
            stepsLeft = assignedJob.stepsLeft;
            stepsLeftText.text = $"{stepsLeft:N0} steps left";
            UpdateStepsLeft();
            if (stepsLeft <= 0)
            {
                stepsLeft = 0;
                stepProgressBar.fillAmount = 1f;
                stepsLeftText.text = $"Job Complete!";
                CompleteJob();
            }
        }

        private void CompleteJob()
        {
            Debug.Log($"job complete");
            assignedJob.state = JobState.Claimable;
            claimButton.interactable = true;
            if (progressRoutine != null)
            {
                StopCoroutine(progressRoutine);
            }
        }

        public void OnResponseButton(bool isAccepted)
        {
            OnJobResponse?.Invoke(this, isAccepted);
        }

        public string FormatTimeRemaining(double timeInSeconds)
        {
            if (timeInSeconds < 0) timeInSeconds = 0;

            TimeSpan t = TimeSpan.FromSeconds(timeInSeconds);
            return string.Format("{0:00}:{1:00}", (int)t.TotalHours, t.Minutes);
        }

        public void ClearJobView()
        {
            assignedJob = null;
            StopAllCoroutines();
            isEmpty = true;
            gameObject.SetActive(false);
        }
    }
}