using UnityEngine;
using UnityEngine.UI;
using System;
using TMPro;

public class HealthConnectController : MonoSingleton<HealthConnectController>
{
    // Drag a Text UI element here in the Inspector to see the results
    public TextMeshProUGUI statusText; 

    public AndroidJavaObject HealthPlugin => _pluginInstance;

    // WARNING: This string MUST match the 'package' line in your Kotlin file EXACTLY.
    // + .HealthConnectManager (the class name)
    private const string PLUGIN_CLASS_NAME = "com.ogunworks.walkapp.healthconnect.HealthConnectManager";

    private AndroidJavaObject _pluginInstance;

    public void InitializePlugin()
    {
        if (_pluginInstance != null) return;
        
        // 1. Check Platform
        if (Application.platform != RuntimePlatform.Android)
        {
            Debug.LogWarning("Health Connect only works on Android.");
            return;
        }

        try
        {
            // 2. Instantiate the Plugin
            // This looks for the class in your .aar file
            _pluginInstance = new AndroidJavaObject(PLUGIN_CLASS_NAME);

            if (_pluginInstance == null)
            {
                Debug.LogError("FATAL: Plugin Instance is NULL. Check the Package Name!");
                return;
            }

            Debug.Log("Plugin Instance created successfully.");

            // 3. Initialize the Kotlin side
            // This calls the 'initialize()' function we wrote in Kotlin
            _pluginInstance.Call("initialize");
            
            OnConnectionEstablished();
            
            Debug.Log("Kotlin Initialize() called.");
        }
        catch (System.Exception e)
        {
            Debug.LogError("C# Error during Init: " + e.Message);
            Debug.LogError("Did you update the Package Name in the C# script to match the Kotlin file?");
        }
    }

    private void OnConnectionEstablished()
    {
        FindFirstObjectByType<StepDataHandler>().OnConnectionEstablished();
        FindFirstObjectByType<StepDisplayManager>().OnConnectionEstablished();
    }

    public void RequestPermissions()
    {
        InitializePlugin();
        if (_pluginInstance != null)
        {
            _pluginInstance.Call("requestPermissions");
            PermissionsManager.instance.OnPermissionsGiven();
        }
        else
        {
            Debug.LogError("Cannot request permissions: Plugin is null.");
            #if UNITY_EDITOR
            PermissionsManager.instance.OnPermissionsGiven();
            #endif
        }
    }

    // Call this from a UI Button
    public void RequestStepsLast24Hours()
    {
        if (_pluginInstance == null) return;
        //healthPlugin.Call("requestPermissions");

        // Calculate time in milliseconds
        long endTime = DateTimeOffset.Now.ToUnixTimeMilliseconds();
        long startTime = DateTimeOffset.Now.AddHours(-24).ToUnixTimeMilliseconds();

        UpdateStatus("Requesting steps...");

        // Call the Kotlin function: getSteps(start, end, GameObjectName, CallbackMethodName)
        _pluginInstance.Call("getSteps", startTime, endTime, this.gameObject.name, "OnStepsReceived");
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