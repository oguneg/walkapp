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
    
    public void OnConnectionEstablished()
    {
        healthPlugin = FindFirstObjectByType<HealthConnectController>().healthPlugin;
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
        DateTime now = DateTime.Now;

        // Create a new DateTime for "Today at 00:00:00"
        DateTime startOfDay = new DateTime(now.Year, now.Month, now.Day, 0, 0, 0);

        // Convert to Unix Milliseconds (using DateTimeOffset to handle timezone correctly)
        long startTime = new DateTimeOffset(startOfDay).ToUnixTimeMilliseconds();
    
        // End time is still "Right Now"
        long endTime = DateTimeOffset.Now.ToUnixTimeMilliseconds();

        healthPlugin.Call("getSteps", startTime, endTime, this.gameObject.name, "OnTodayStepsReceived");
    }
    
    public void RequestStepsLast30Minutes()
    {
        long endTime = DateTimeOffset.Now.ToUnixTimeMilliseconds();
        long startTime = DateTimeOffset.Now.AddMinutes(-30).ToUnixTimeMilliseconds();

        healthPlugin.Call("getSteps", startTime, endTime, this.gameObject.name, "On30MinuteStepsReceived");
    }

    public void On30MinuteStepsReceived(string stepsCount)
    {
        stepText30Mins.text = stepsCount;
    }
    
    public void OnTodayStepsReceived(string stepsCount)
    {
        stepTextToday.text = stepsCount;
    }
    
    public void OnTotalStepsReceived(string stepsCount)
    {
        stepTextTotal.text = stepsCount;
    }
}
