using System;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_ANDROID
using UnityEngine.Android;
#endif

public class RecordingApiManager : MonoSingleton<RecordingApiManager>
{
    private const string TAG = "RecordingAPI";

    public enum BucketUnit { Minutes, Hours, Days }

    public void InitializeRecordingAPI()
    {
        if (Application.platform != RuntimePlatform.Android) return;

        using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
        {
            AndroidJavaObject context = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");

            AndroidJavaClass apiAvailClass = new AndroidJavaClass("com.google.android.gms.common.GoogleApiAvailability");
            AndroidJavaObject apiAvail = apiAvailClass.CallStatic<AndroidJavaObject>("getInstance");

            AndroidJavaClass localClientClass = new AndroidJavaClass("com.google.android.gms.fitness.LocalRecordingClient");
            int minVersion = localClientClass.GetStatic<int>("LOCAL_RECORDING_CLIENT_MIN_VERSION_CODE");

            int resultCode = apiAvail.Call<int>("isGooglePlayServicesAvailable", context, minVersion);
            if (resultCode != 0) 
            {
                Debug.LogError($"{TAG}: Google Play Services needs an update to use the Recording API.");
                return;
            }

            AndroidJavaClass fitnessLocal = new AndroidJavaClass("com.google.android.gms.fitness.FitnessLocal");
            AndroidJavaObject localRecordingClient = fitnessLocal.CallStatic<AndroidJavaObject>("getLocalRecordingClient", context);

            AndroidJavaClass localDataTypeClass = new AndroidJavaClass("com.google.android.gms.fitness.data.LocalDataType");
            AndroidJavaObject typeStepCountDelta = localDataTypeClass.GetStatic<AndroidJavaObject>("TYPE_STEP_COUNT_DELTA");

            AndroidJavaObject task = localRecordingClient.Call<AndroidJavaObject>("subscribe", typeStepCountDelta);

            task.Call<AndroidJavaObject>("addOnSuccessListener", new TaskSuccessListener(result => {
                Debug.Log($"{TAG}: Successfully subscribed! The OS is now batching steps silently.");
            }));

            task.Call<AndroidJavaObject>("addOnFailureListener", new TaskFailureListener(exception => {
                Debug.LogError($"{TAG}: Failed to subscribe. " + exception.Call<string>("getMessage"));
            }));
        }
    }

    // --- THE NEW FLEXIBLE QUERY METHOD ---
    public void ReadStepData(long startTimeUnixSeconds, long endTimeUnixSeconds, int bucketDuration, BucketUnit unit, Action<Dictionary<long, int>> onDataRetrieved)
    {
        if (Application.platform != RuntimePlatform.Android) return;

        using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
        {
            AndroidJavaObject context = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");

            AndroidJavaClass fitnessLocal = new AndroidJavaClass("com.google.android.gms.fitness.FitnessLocal");
            AndroidJavaObject localRecordingClient = fitnessLocal.CallStatic<AndroidJavaObject>("getLocalRecordingClient", context);

            AndroidJavaClass localDataTypeClass = new AndroidJavaClass("com.google.android.gms.fitness.data.LocalDataType");
            AndroidJavaObject typeStepCountDelta = localDataTypeClass.GetStatic<AndroidJavaObject>("TYPE_STEP_COUNT_DELTA");

            AndroidJavaClass timeUnitClass = new AndroidJavaClass("java.util.concurrent.TimeUnit");
            AndroidJavaObject secondsUnit = timeUnitClass.GetStatic<AndroidJavaObject>("SECONDS");
            
            // Map our C# Enum to the Java TimeUnit
            AndroidJavaObject javaBucketUnit;
            if (unit == BucketUnit.Days)
                javaBucketUnit = timeUnitClass.GetStatic<AndroidJavaObject>("DAYS");
            else if (unit == BucketUnit.Hours)
                javaBucketUnit = timeUnitClass.GetStatic<AndroidJavaObject>("HOURS");
            else 
                javaBucketUnit = timeUnitClass.GetStatic<AndroidJavaObject>("MINUTES");

            AndroidJavaObject builder = new AndroidJavaObject("com.google.android.gms.fitness.request.LocalDataReadRequest$Builder");
            builder.Call<AndroidJavaObject>("aggregate", typeStepCountDelta);
            builder.Call<AndroidJavaObject>("bucketByTime", bucketDuration, javaBucketUnit);
            builder.Call<AndroidJavaObject>("setTimeRange", startTimeUnixSeconds, endTimeUnixSeconds, secondsUnit);
            
            AndroidJavaObject readRequest = builder.Call<AndroidJavaObject>("build");
            AndroidJavaObject task = localRecordingClient.Call<AndroidJavaObject>("readData", readRequest);

            task.Call<AndroidJavaObject>("addOnSuccessListener", new TaskSuccessListener(response => {
                Dictionary<long, int> extractedLogs = new Dictionary<long, int>();
                
                AndroidJavaObject buckets = response.Call<AndroidJavaObject>("getBuckets");
                int bucketCount = buckets.Call<int>("size");

                for (int i = 0; i < bucketCount; i++)
                {
                    AndroidJavaObject bucket = buckets.Call<AndroidJavaObject>("get", i);
                    long bucketStartSec = bucket.Call<long>("getStartTime", secondsUnit);
                    
                    AndroidJavaObject dataSets = bucket.Call<AndroidJavaObject>("getDataSets");
                    int dsCount = dataSets.Call<int>("size");
                    
                    int stepsInBucket = 0;
                    for (int j = 0; j < dsCount; j++)
                    {
                        AndroidJavaObject dataSet = dataSets.Call<AndroidJavaObject>("get", j);
                        AndroidJavaObject dataPoints = dataSet.Call<AndroidJavaObject>("getDataPoints");
                        int dpCount = dataPoints.Call<int>("size");
                        
                        for (int k = 0; k < dpCount; k++)
                        {
                            AndroidJavaObject dp = dataPoints.Call<AndroidJavaObject>("get", k);
                            
                            AndroidJavaClass fieldClass = new AndroidJavaClass("com.google.android.gms.fitness.data.LocalField");
                            AndroidJavaObject fieldSteps = fieldClass.GetStatic<AndroidJavaObject>("FIELD_STEPS");
                            
                            AndroidJavaObject val = dp.Call<AndroidJavaObject>("getValue", fieldSteps);
                            stepsInBucket += val.Call<int>("asInt");
                        }
                    }

                    if (stepsInBucket > 0)
                    {
                        extractedLogs.Add(bucketStartSec, stepsInBucket);
                    }
                }

                onDataRetrieved?.Invoke(extractedLogs);
            }));

            task.Call<AndroidJavaObject>("addOnFailureListener", new TaskFailureListener(exception => {
                Debug.LogError($"{TAG}: Failed to read data. " + exception.Call<string>("getMessage"));
            }));
        }
    }

// --- THE TEST FUNCTION (Hook this to your UI Button) ---
    public void TestLast7DaysData()
    {
        Debug.Log("--- INITIATING RECORDING API BUCKET TEST ---");

        DateTimeOffset now = DateTimeOffset.UtcNow;
        long endUnix = now.ToUnixTimeSeconds();

        // 1. Math for Hourly Snapping (Floor to XX:00:00)
        DateTimeOffset topOfHour = new DateTimeOffset(now.Year, now.Month, now.Day, now.Hour, 0, 0, now.Offset);
        long startUnix7Days = topOfHour.AddDays(-7).ToUnixTimeSeconds();

        // 2. Math for 15-Min Snapping (Floor to XX:00, XX:15, XX:30, XX:45)
        int flooredMinute = (now.Minute / 15) * 15;
        DateTimeOffset topOf15Min = new DateTimeOffset(now.Year, now.Month, now.Day, now.Hour, flooredMinute, 0, now.Offset);
        long startUnix12Hours = topOf15Min.AddHours(-12).ToUnixTimeSeconds();

        // Fire the Hourly Request (Last 7 Days)
        ReadStepData(startUnix7Days, endUnix, 1, BucketUnit.Hours, (Dictionary<long, int> hourlyLogs) => {
            Debug.Log($"\n=== HOURLY BUCKETS (Last 7 Days) | Found {hourlyLogs.Count} active hours ===");
            foreach (var log in hourlyLogs)
            {
                DateTime startTime = DateTimeOffset.FromUnixTimeSeconds(log.Key).LocalDateTime;
                DateTime endTime = startTime.AddHours(1); 
                
                // Format: 2026-03-11 11:00-12:00
                Debug.Log($"[HOURLY] {startTime:yyyy-MM-dd HH:mm}-{endTime:HH:mm} | Steps: {log.Value}");
            }
        });

        // Fire the Aggressive 15-Minute Request (Last 12 Hours Only)
        ReadStepData(startUnix12Hours, endUnix, 15, BucketUnit.Minutes, (Dictionary<long, int> minLogs) => {
            Debug.Log($"\n=== 15-MINUTE BUCKETS (Last 12 Hours) | Found {minLogs.Count} active intervals ===");
            foreach (var log in minLogs)
            {
                DateTime startTime = DateTimeOffset.FromUnixTimeSeconds(log.Key).LocalDateTime;
                DateTime endTime = startTime.AddMinutes(15); 
                
                // Format: 2026-03-11 11:00-11:15
                Debug.Log($"[15-MIN] {startTime:yyyy-MM-dd HH:mm}-{endTime:HH:mm} | Steps: {log.Value}");
            }
        });
    }

    // --- NATIVE JAVA PROXIES ---
    private class TaskSuccessListener : AndroidJavaProxy
    {
        private Action<AndroidJavaObject> callback;
        public TaskSuccessListener(Action<AndroidJavaObject> action) : base("com.google.android.gms.tasks.OnSuccessListener")
        {
            callback = action;
        }
        public void onSuccess(AndroidJavaObject result) => callback?.Invoke(result);
    }

    private class TaskFailureListener : AndroidJavaProxy
    {
        private Action<AndroidJavaObject> callback;
        public TaskFailureListener(Action<AndroidJavaObject> action) : base("com.google.android.gms.tasks.OnFailureListener")
        {
            callback = action;
        }
        public void onFailure(AndroidJavaObject exception) => callback?.Invoke(exception);
    }
}