using UnityEngine;
using UnityEngine.UI;
using System;
using TMPro;

public class HealthConnectController : MonoBehaviour
{
    // Drag a Text UI element here in the Inspector to see the results
    public TextMeshProUGUI statusText; 

    public AndroidJavaObject healthPlugin;

    void Start()
    {
        if (Application.platform == RuntimePlatform.Android)
        {
            try 
            {
                AndroidJavaClass pluginClass = new AndroidJavaClass("com.ogunworks.walkapp.healthconnect.HealthConnectManager");
                healthPlugin = pluginClass.GetStatic<AndroidJavaObject>("instance");
                healthPlugin.Call("initialize");
                UpdateStatus("Plugin Initialized");

                // --- NEW CODE: Check & Request Permissions ---
                bool hasPerms = healthPlugin.Call<bool>("hasPermissions");
                if (hasPerms)
                {
                    UpdateStatus("Permissions: Granted");
                }
                else
                {
                    UpdateStatus("Asking for Permissions...");
                    healthPlugin.Call("requestPermissions");
                }
                // ---------------------------------------------
            }
            catch (Exception e)
            {
                UpdateStatus("Error init: " + e.Message);
            }
        }
    }

    // Call this from a UI Button
    public void RequestStepsLast24Hours()
    {
        if (healthPlugin == null) return;
        FindFirstObjectByType<StepDataHandler>().OnConnectionEstablished();
        // Calculate time in milliseconds
        long endTime = DateTimeOffset.Now.ToUnixTimeMilliseconds();
        long startTime = DateTimeOffset.Now.AddHours(-24).ToUnixTimeMilliseconds();

        UpdateStatus("Requesting steps...");

        // Call the Kotlin function: getSteps(start, end, GameObjectName, CallbackMethodName)
        healthPlugin.Call("getSteps", startTime, endTime, this.gameObject.name, "OnStepsReceived");
    }

    // This function is called by Kotlin when data is ready
    public void OnStepsReceived(string stepsCount)
    {
        UpdateStatus("Steps last 24h: " + stepsCount);
        Debug.Log("Steps received: " + stepsCount);
    }

    // Helper to update UI text safely
    private void UpdateStatus(string message)
    {
        if (statusText != null)
        {
            statusText.text = message;
        }
    }
}