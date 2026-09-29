using System;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_ANDROID
using Unity.Notifications.Android;
#endif

/// <summary>
/// Local notifications, planned when the app goes to the background and cancelled when it comes back.
/// Everything here is time-based (deadlines, express offers), so it needs no background work and no
/// permanent notification. Step-based alerts ("your job is delivered") need a background step check; see
/// the notes in the design doc. Android only for now; iOS is on hold.
/// </summary>
public class NotificationScheduler : MonoSingleton<NotificationScheduler>
{
    public struct Planned
    {
        public DateTime fireUtc;
        public string title, text;
    }

    private const string ChannelId = "jobs";
    private const string AskedKey = "Notifications.PermissionAsked";

    [Tooltip("Remind this long before a regular job's deadline.")]
    [SerializeField] private int jobReminderMinutes = 60;
    [Tooltip("Remind this long before an express job's deadline.")]
    [SerializeField] private int expressReminderMinutes = 10;
    [Tooltip("With no job running, nudge the player after this long away.")]
    [SerializeField] private int idleReminderHours = 20;

    private static bool IsDevice => Application.platform == RuntimePlatform.Android;

    public override void Init()
    {
#if UNITY_ANDROID
        if (!IsDevice) return;
        AndroidNotificationCenter.RegisterNotificationChannel(new AndroidNotificationChannel
        {
            Id = ChannelId,
            Name = "Jobs",
            Importance = Importance.Default,
            Description = "Deadlines and express deliveries"
        });
#endif
    }

    private void Start() => CancelAll();

    /// <summary>
    /// Android 13+ asks the player once. Called when it makes sense (first job taken, or notifications turned on
    /// in settings), not at launch.
    /// </summary>
    public void RequestPermission(bool onlyOnce = false)
    {
        if (onlyOnce && PlayerPrefs.GetInt(AskedKey, 0) == 1) return;
        PlayerPrefs.SetInt(AskedKey, 1);
#if UNITY_ANDROID
        if (!IsDevice) return;
        if (AndroidNotificationCenter.UserPermissionToPost != PermissionStatus.Allowed)
            new PermissionRequest();
#endif
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused) ScheduleAll();
        else CancelAll();
    }

    private void OnApplicationFocus(bool focused)
    {
        if (focused) CancelAll();
    }

    private void ScheduleAll()
    {
        CancelAll();
        if (!GameSettings.NotificationsOn) return;

        foreach (var n in Plan())
        {
#if UNITY_ANDROID
            if (!IsDevice) continue;
            // Real clock, not GameClock: the debug time offset must not move real notifications.
            DateTime fire = DateTime.Now + (n.fireUtc - GameClock.UtcNow);
            AndroidNotificationCenter.SendNotification(new AndroidNotification
            {
                Title = n.title,
                Text = n.text,
                FireTime = fire,
                ShouldAutoCancel = true
            }, ChannelId);
#endif
        }
    }

    private void CancelAll()
    {
#if UNITY_ANDROID
        if (IsDevice) AndroidNotificationCenter.CancelAllNotifications();
#endif
    }

    /// <summary>What would be scheduled if the app went to the background now (also used by tests).</summary>
    public List<Planned> Plan()
    {
        var list = new List<Planned>();
        DateTime now = GameClock.UtcNow;
        void Add(DateTime at, string title, string text)
        {
            if (at > now.AddMinutes(1)) list.Add(new Planned { fireUtc = at, title = title, text = text });
        }

        // Regular job: a reminder before the deadline, with the steps left when the player last looked.
        var jobs = JobManager.instance;
        var job = jobs.activeJob;
        if (job != null && job.IsRunning)
        {
            Add(job.DeadlineUtc.AddMinutes(-jobReminderMinutes), $"{job.jobData.cargoType} is due in {jobReminderMinutes} min",
                $"{job.stepsLeft:N0} steps to go when you last checked. Open Walkapp to see where you are.");
        }

        // The queued job's deadline runs from when it was queued; it may or may not have started by then.
        var queued = jobs.queuedJob;
        if (queued != null)
            Add(queued.DeadlineUtc.AddMinutes(-jobReminderMinutes), $"{queued.jobData.cargoType} is due in {jobReminderMinutes} min",
                "Your queued job's deadline is close. Open Walkapp to see where you are.");

        // Express: the offer is about to expire, the job is about to end, or the next offer arrives.
        var express = ExpressJobManager.instance;
        if (ProgressionManager.instance.IsUnlocked(Feature.Express))
        {
            if (express.Job != null && express.Job.status == ExpressStatus.Active)
            {
                Add(express.Job.DeadlineUtc.AddMinutes(-expressReminderMinutes), $"Express: {expressReminderMinutes} minutes left",
                    $"{express.Job.offer.cargoType}: {express.Job.StepsLeft:N0} steps to go when you last checked. Keep walking!");
            }
            else if (express.HasOffer)
            {
                var offer = express.Offer;
                Add(GameClock.FromUnix(offer.expiresUnix).AddMinutes(-5), "Express offer ends in 5 minutes",
                    $"Walk {offer.targetSteps:N0} steps in {offer.durationMinutes} min for {offer.reward:N0} coins.");
            }
            else if (express.Job == null)
            {
                Add(express.NextOfferUtc, "New express delivery",
                    "A rush job just came in. It pays 3 to 5 times more, but the clock is tight.");
            }
        }

        // Nothing on the road: one gentle nudge.
        if (job == null && express.Job == null)
            Add(now.AddHours(idleReminderHours), "Your truck is parked",
                "Pick a job and let your steps pay. Banked steps are waiting too.");

        list.Sort((a, b) => a.fireUtc.CompareTo(b.fireUtc));
        return list;
    }
}
