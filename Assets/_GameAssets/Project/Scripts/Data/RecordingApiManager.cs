using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
#if UNITY_ANDROID
using UnityEngine.Android;
#endif

public class RecordingApiManager : MonoSingleton<RecordingApiManager>
{
    private const string TAG = "RecordingAPI";
    [SerializeField] private TextMeshProUGUI last7DaysText, last24HoursText;

    public enum BucketUnit
    {
        Minutes,
        Hours,
        Days
    }

    public void InitializeRecordingAPI()
    {
        if (Application.platform != RuntimePlatform.Android) return;

        using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
        {
            AndroidJavaObject context = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");

            AndroidJavaClass apiAvailClass =
                new AndroidJavaClass("com.google.android.gms.common.GoogleApiAvailability");
            AndroidJavaObject apiAvail = apiAvailClass.CallStatic<AndroidJavaObject>("getInstance");

            AndroidJavaClass localClientClass =
                new AndroidJavaClass("com.google.android.gms.fitness.LocalRecordingClient");
            int minVersion = localClientClass.GetStatic<int>("LOCAL_RECORDING_CLIENT_MIN_VERSION_CODE");

            int resultCode = apiAvail.Call<int>("isGooglePlayServicesAvailable", context, minVersion);
            if (resultCode != 0)
            {
                Debug.LogError($"{TAG}: Google Play Services needs an update to use the Recording API.");
                return;
            }

            AndroidJavaClass fitnessLocal = new AndroidJavaClass("com.google.android.gms.fitness.FitnessLocal");
            AndroidJavaObject localRecordingClient =
                fitnessLocal.CallStatic<AndroidJavaObject>("getLocalRecordingClient", context);

            AndroidJavaClass localDataTypeClass =
                new AndroidJavaClass("com.google.android.gms.fitness.data.LocalDataType");
            AndroidJavaObject typeStepCountDelta =
                localDataTypeClass.GetStatic<AndroidJavaObject>("TYPE_STEP_COUNT_DELTA");

            AndroidJavaObject task = localRecordingClient.Call<AndroidJavaObject>("subscribe", typeStepCountDelta);

            task.Call<AndroidJavaObject>("addOnSuccessListener",
                new TaskSuccessListener(result =>
                {
                    Debug.Log($"{TAG}: Successfully subscribed! The OS is now batching steps silently.");
                }));

            task.Call<AndroidJavaObject>("addOnFailureListener",
                new TaskFailureListener(exception =>
                {
                    Debug.LogError($"{TAG}: Failed to subscribe. " + exception.Call<string>("getMessage"));
                }));
        }
    }

    // --- THE NEW FLEXIBLE QUERY METHOD ---
    public void ReadStepData(long startTimeUnixSeconds, long endTimeUnixSeconds, int bucketDuration, BucketUnit unit,
        Action<Dictionary<long, int>> onDataRetrieved)
    {
        if (Application.platform != RuntimePlatform.Android) return;

        using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
        {
            AndroidJavaObject context = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");

            AndroidJavaClass fitnessLocal = new AndroidJavaClass("com.google.android.gms.fitness.FitnessLocal");
            AndroidJavaObject localRecordingClient =
                fitnessLocal.CallStatic<AndroidJavaObject>("getLocalRecordingClient", context);

            AndroidJavaClass localDataTypeClass =
                new AndroidJavaClass("com.google.android.gms.fitness.data.LocalDataType");
            AndroidJavaObject typeStepCountDelta =
                localDataTypeClass.GetStatic<AndroidJavaObject>("TYPE_STEP_COUNT_DELTA");

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

            AndroidJavaObject builder =
                new AndroidJavaObject("com.google.android.gms.fitness.request.LocalDataReadRequest$Builder");
            builder.Call<AndroidJavaObject>("aggregate", typeStepCountDelta);
            builder.Call<AndroidJavaObject>("bucketByTime", bucketDuration, javaBucketUnit);
            builder.Call<AndroidJavaObject>("setTimeRange", startTimeUnixSeconds, endTimeUnixSeconds, secondsUnit);

            AndroidJavaObject readRequest = builder.Call<AndroidJavaObject>("build");
            AndroidJavaObject task = localRecordingClient.Call<AndroidJavaObject>("readData", readRequest);

            task.Call<AndroidJavaObject>("addOnSuccessListener", new TaskSuccessListener(response =>
            {
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

                            AndroidJavaClass fieldClass =
                                new AndroidJavaClass("com.google.android.gms.fitness.data.LocalField");
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

            task.Call<AndroidJavaObject>("addOnFailureListener",
                new TaskFailureListener(exception =>
                {
                    Debug.LogError($"{TAG}: Failed to read data. " + exception.Call<string>("getMessage"));
                }));
        }
    }

// --- THE TEST FUNCTION (Hook this to your UI Button) ---
    public void TestLast7DaysData()
    {
        Debug.Log("--- INITIATING STRICT-SNAP RECORDING API TEST ---");

        // Get the absolute current Unix time in seconds
        long nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        // 1. STRICT 15-MIN MATH (Modulo 900 seconds)
        // This physically forces the start time to XX:00, XX:15, XX:30, or XX:45
        long startUnix15Min = nowUnix - (nowUnix % 900) - (12 * 3600); // Back 12 hours
        long endUnix15Min = nowUnix - (nowUnix % 900) + 900; // Include current ongoing block

        // 2. STRICT HOURLY MATH (Modulo 3600 seconds)
        // Forces start time to exactly XX:00:00
        long startUnixHours = nowUnix - (nowUnix % 3600) - (7 * 24 * 3600); // Back 7 Days
        long endUnixHours = nowUnix - (nowUnix % 3600) + 3600; // Include current hour

        // 3. STRICT DAILY MATH (Modulo 86400 seconds)
        // We add local timezone offset first so "midnight" aligns with your physical clock, not London's.
        long localOffsetSeconds = (long)DateTimeOffset.Now.Offset.TotalSeconds;
        long localNowUnix = nowUnix + localOffsetSeconds;

        long startLocalUnixDays = localNowUnix - (localNowUnix % 86400) - (7 * 86400);
        long startUnixDays = startLocalUnixDays - localOffsetSeconds; // Convert back to UTC for the API
        long endUnixDays = startUnixDays + (8 * 86400); // Include today fully

        // --- FIRE THE QUERIES ---

        // 15-Minute Request
        ReadStepData(startUnix15Min, endUnix15Min, 15, BucketUnit.Minutes, (Dictionary<long, int> minLogs) =>
        {
            Debug.Log($"\n=== 15-MINUTE BUCKETS (Last 12 Hours) | Found {minLogs.Count} active intervals ===");
            foreach (var log in minLogs)
            {
                DateTime startTime = DateTimeOffset.FromUnixTimeSeconds(log.Key).LocalDateTime;
                DateTime endTime = startTime.AddMinutes(15);
                Debug.Log($"[15-MIN] {startTime:yyyy-MM-dd HH:mm}-{endTime:HH:mm} | Steps: {log.Value}");
            }
        });

        // Hourly Request
        ReadStepData(startUnixHours, endUnixHours, 1, BucketUnit.Hours, (Dictionary<long, int> hourlyLogs) =>
        {
            Debug.Log($"\n=== HOURLY BUCKETS (Last 7 Days) | Found {hourlyLogs.Count} active hours ===");
            foreach (var log in hourlyLogs)
            {
                DateTime startTime = DateTimeOffset.FromUnixTimeSeconds(log.Key).LocalDateTime;
                DateTime endTime = startTime.AddHours(1);
                Debug.Log($"[HOURLY] {startTime:yyyy-MM-dd HH:mm}-{endTime:HH:mm} | Steps: {log.Value}");
            }
        });

        // Daily Request
        ReadStepData(startUnixDays, endUnixDays, 1, BucketUnit.Days, (Dictionary<long, int> dailyLogs) =>
        {
            Debug.Log($"\n=== DAILY BUCKETS (Midnight to Midnight) | Found {dailyLogs.Count} active days ===");
            foreach (var log in dailyLogs)
            {
                DateTime startTime = DateTimeOffset.FromUnixTimeSeconds(log.Key).LocalDateTime;
                // For daily, we just print the date it belongs to
                Debug.Log($"[DAILY] {startTime:yyyy-MM-dd} | Steps: {log.Value}");
            }
        });
    }

    public void GeneratePrintableLogs()
    {
        // Get the absolute current Unix time in seconds
        long nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        // 2. STRICT HOURLY MATH (Modulo 3600 seconds)
        // Forces start time to exactly XX:00:00
        long startUnixHours = nowUnix - (nowUnix % 3600) - (7 * 24 * 3600); // Back 7 Days
        long endUnixHours = nowUnix - (nowUnix % 3600) + 3600; // Include current hour

        // 3. STRICT DAILY MATH (Modulo 86400 seconds)
        // We add local timezone offset first so "midnight" aligns with your physical clock, not London's.
        long localOffsetSeconds = (long)DateTimeOffset.Now.Offset.TotalSeconds;
        long localNowUnix = nowUnix + localOffsetSeconds;

        long startLocalUnixDays = localNowUnix - (localNowUnix % 86400) - (7 * 86400);
        long startUnixDays = startLocalUnixDays - localOffsetSeconds; // Convert back to UTC for the API
        long endUnixDays = startUnixDays + (8 * 86400); // Include today fully

        int[] dailyStepCounts =  new int[7];
        int[] hourlyStepCounts =  new int[24];
        /*
        for (int i = 0; i < 7; i++)
        {
            GetTotalStepsInRange(endUnixDays - 86400 * i, endUnixDays - 86400 - 86400 * i, result =>
            {
                dailyStepCounts[i] = result;
            });
        }*/
        
        for (int i = 0; i < 24; i++)
        {
            var i1 = i;
            GetTotalStepsInRange(endUnixDays - 3600 - 3600 * i, endUnixDays - 3600 * i, result =>
            {
                hourlyStepCounts[23-i1] = result;
                
                StringBuilder sbHour =  new StringBuilder();
                for (int i = 0; i < 24; i++)
                {
                    sbHour.Append($"{i} - {hourlyStepCounts[i]} steps");
                    sbHour.AppendLine();
                }
        
                last24HoursText.text = sbHour.ToString();
            });
        }
    }
    
    // Call this to get a single total step count for a specific timeframe
    public void GetTotalStepsInRange(long startTimeUnixSeconds, long endTimeUnixSeconds, Action<int> onTotalRetrieved)
    {
        if (Application.platform != RuntimePlatform.Android) 
        {
            Debug.LogWarning($"{TAG}: Not on Android. Returning 0 steps.");
            onTotalRetrieved?.Invoke(0);
            return;
        }

        using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
        {
            AndroidJavaObject context = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");

            AndroidJavaClass fitnessLocal = new AndroidJavaClass("com.google.android.gms.fitness.FitnessLocal");
            AndroidJavaObject localRecordingClient = fitnessLocal.CallStatic<AndroidJavaObject>("getLocalRecordingClient", context);

            AndroidJavaClass localDataTypeClass = new AndroidJavaClass("com.google.android.gms.fitness.data.LocalDataType");
            AndroidJavaObject typeStepCountDelta = localDataTypeClass.GetStatic<AndroidJavaObject>("TYPE_STEP_COUNT_DELTA");

            AndroidJavaClass timeUnitClass = new AndroidJavaClass("java.util.concurrent.TimeUnit");
            AndroidJavaObject secondsUnit = timeUnitClass.GetStatic<AndroidJavaObject>("SECONDS");
            
            // We bucket by 1 minute to ensure the API respects your exact start/end boundaries
            // without snapping to an hour or day boundary and accidentally including extra steps.
            AndroidJavaObject minutesUnit = timeUnitClass.GetStatic<AndroidJavaObject>("MINUTES");

            AndroidJavaObject builder = new AndroidJavaObject("com.google.android.gms.fitness.request.LocalDataReadRequest$Builder");
            builder.Call<AndroidJavaObject>("aggregate", typeStepCountDelta);
            builder.Call<AndroidJavaObject>("bucketByTime", 1, minutesUnit);
            builder.Call<AndroidJavaObject>("setTimeRange", startTimeUnixSeconds, endTimeUnixSeconds, secondsUnit);
            
            AndroidJavaObject readRequest = builder.Call<AndroidJavaObject>("build");
            AndroidJavaObject task = localRecordingClient.Call<AndroidJavaObject>("readData", readRequest);

            task.Call<AndroidJavaObject>("addOnSuccessListener", new TaskSuccessListener(response => {
                int grandTotalSteps = 0;
                
                AndroidJavaObject buckets = response.Call<AndroidJavaObject>("getBuckets");
                int bucketCount = buckets.Call<int>("size");

                for (int i = 0; i < bucketCount; i++)
                {
                    AndroidJavaObject bucket = buckets.Call<AndroidJavaObject>("get", i);
                    AndroidJavaObject dataSets = bucket.Call<AndroidJavaObject>("getDataSets");
                    int dsCount = dataSets.Call<int>("size");
                    
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
                            grandTotalSteps += val.Call<int>("asInt");
                        }
                    }
                }

                // Pass the final single integer back to your game logic
                onTotalRetrieved?.Invoke(grandTotalSteps);
            }));

            task.Call<AndroidJavaObject>("addOnFailureListener", new TaskFailureListener(exception => {
                Debug.LogError($"{TAG}: Failed to read step total. " + exception.Call<string>("getMessage"));
                // Return 0 so your game logic doesn't hang waiting for an answer
                onTotalRetrieved?.Invoke(0);
            }));
        }
    }

    // --- NATIVE JAVA PROXIES ---
    private class TaskSuccessListener : AndroidJavaProxy
    {
        private Action<AndroidJavaObject> callback;

        public TaskSuccessListener(Action<AndroidJavaObject> action) : base(
            "com.google.android.gms.tasks.OnSuccessListener")
        {
            callback = action;
        }

        public void onSuccess(AndroidJavaObject result) => callback?.Invoke(result);
    }

    private class TaskFailureListener : AndroidJavaProxy
    {
        private Action<AndroidJavaObject> callback;

        public TaskFailureListener(Action<AndroidJavaObject> action) : base(
            "com.google.android.gms.tasks.OnFailureListener")
        {
            callback = action;
        }

        public void onFailure(AndroidJavaObject exception) => callback?.Invoke(exception);
    }
}