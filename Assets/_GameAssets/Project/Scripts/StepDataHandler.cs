using System;
using System.Collections;
using System.Linq;
using TMPro;
using UnityEngine;

public class StepDataHandler : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI stepTextTotal,
        stepTextSession,
        stepText30Mins,
        stepTextToday,
        stepTextYesterday,
        stepTextLast7Days;
    
    private AndroidJavaObject healthPlugin;
    private long sessionStartTime;

    private void Start()
    {
        sessionStartTime = DateTimeOffset.Now.ToUnixTimeMilliseconds();
    }

    public void OnConnectionEstablished()
    {
        healthPlugin = FindFirstObjectByType<HealthConnectController>().HealthPlugin;
        StartCoroutine(UpdateRoutine());
    }

    private IEnumerator UpdateRoutine()
    {
        WaitForSeconds wfs = new WaitForSeconds(5f);
        while (true)
        {
            RequestAll();
            yield return wfs;
        }
    }
    
    public void RequestAll()
    {
        if (healthPlugin == null) return;
        RequestStepsLast30Minutes();
        RequestStepsToday();
        RequestStepsYesterday();
        RequestStepsSession();
        RequestStepsLastWeek();
        RequestStepsTotal();
    }
    
    public void RequestStepsTotal()
    {
        long endTime = DateTimeOffset.Now.ToUnixTimeMilliseconds();
        long startTime = DateTimeOffset.Now.AddYears(-10).ToUnixTimeMilliseconds();

        healthPlugin.Call("getSteps", startTime, endTime, this.gameObject.name, "OnTotalStepsReceived");
    }
    
    public void RequestStepsToday()
    {
        // 1. Get "Today" at 00:00:00 cleanly
        DateTime todayMidnight = DateTime.Today; 

        // 2. FORCE the offset to match the phone's current setting.
        // This prevents the "Unspecified Kind" trap where C# might guess UTC.
        TimeSpan currentOffset = TimeZoneInfo.Local.GetUtcOffset(DateTime.Now);
        DateTimeOffset startOffset = new DateTimeOffset(todayMidnight, currentOffset);
    
        long startTime = startOffset.ToUnixTimeMilliseconds();
        long endTime = DateTimeOffset.Now.ToUnixTimeMilliseconds();

        Debug.Log($"Requesting Today: {startTime} to {endTime} (Offset: {currentOffset.TotalHours})");

        healthPlugin.Call("getSteps", startTime, endTime, this.gameObject.name, "OnTodayStepsReceived");
    }
    
    public void RequestStepsLastWeek()
    {
        DateTime now = DateTime.Now;

        // Create a new DateTime for "Today at 00:00:00"
        DateTime startOfDay = new DateTime(now.Year, now.Month, now.Day, 0, 0, 0);

        // Convert to Unix Milliseconds (using DateTimeOffset to handle timezone correctly)
        long startTime = new DateTimeOffset(startOfDay).AddDays(-6).ToUnixTimeMilliseconds();
    
        // End time is still "Right Now"
        long endTime = DateTimeOffset.Now.ToUnixTimeMilliseconds();

        healthPlugin.Call("getSteps", startTime, endTime, this.gameObject.name, "OnWeeklyStepsReceived");
    }
    
    public void RequestStepsYesterday()
    {
        DateTime now = DateTime.Now;

        // Create a new DateTime for "Today at 00:00:00"
        DateTime startOfDay = new DateTime(now.Year, now.Month, now.Day, 0, 0, 0);

        // Convert to Unix Milliseconds (using DateTimeOffset to handle timezone correctly)
        long endTime = new DateTimeOffset(startOfDay).ToUnixTimeMilliseconds();
    
        // End time is still "Right Now"
        long startTime = new DateTimeOffset(startOfDay).AddDays(-1).ToUnixTimeMilliseconds();

        healthPlugin.Call("getSteps", startTime, endTime, this.gameObject.name, "OnYesterdayStepsReceived");
    }
    
    public void RequestStepsLast30Minutes()
    {
        long endTime = DateTimeOffset.Now.ToUnixTimeMilliseconds();
        long startTime = DateTimeOffset.Now.AddMinutes(-30).ToUnixTimeMilliseconds();

        healthPlugin.Call("getSteps", startTime, endTime, this.gameObject.name, "On30MinuteStepsReceived");
    }
    
    public void RequestStepsSession()
    {
        long endTime = DateTimeOffset.Now.ToUnixTimeMilliseconds();

        healthPlugin.Call("getSteps", sessionStartTime, endTime, this.gameObject.name, "OnSessionStepsReceived");
    }

    public void On30MinuteStepsReceived(string stepsCount)
    {
        stepText30Mins.text = stepsCount;
    }
    
    public void OnTodayStepsReceived(string stepsCount)
    {
        stepTextToday.text = stepsCount;
    }
    
    public void OnSessionStepsReceived(string stepsCount)
    {
        stepTextSession.text = stepsCount;
    }
    
    public void OnWeeklyStepsReceived(string stepsCount)
    {
        stepTextLast7Days.text = stepsCount;
    }
    
    public void OnYesterdayStepsReceived(string stepsCount)
    {
        stepTextYesterday.text = stepsCount;
    }
    
    public void OnTotalStepsReceived(string stepsCount)
    {
        stepTextTotal.text = stepsCount;
    }
}
