using System;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_ANDROID
using Unity.Notifications.Android;
#endif

/// <summary>
/// Local notifications, planned when the app goes to the background and cancelled when it comes back.
/// Two kinds, neither needs a foreground service or a permanent notification:
///   - time-based reminders (deadlines, express offers), scheduled as alarms;
///   - step-based alerts ("Grain delivered!"): the plan of real steps each job still needs is handed to a native
///     background worker (Plugins/Android/StepWatch) that checks the step history about every 15 minutes.
/// Android only for now; iOS is on hold.
/// </summary>
public class NotificationScheduler : MonoSingleton<NotificationScheduler>
{
    public struct Planned
    {
        public DateTime fireUtc;
        public string title, text;
        public int id; // fixed id for reminders the step watch may cancel; 0 = any
    }

    [Serializable]
    public class Watch
    {
        public long steps;      // real walked steps still needed (bank burn already taken into account)
        public long deadline;   // unix seconds, real clock
        public int reminderId;  // the deadline reminder to cancel once delivered
        public string doneTitle, doneText, missTitle, missText;
    }

    [Serializable]
    public class WatchPlan
    {
        public long start;
        public string channel = ChannelId;
        public List<Watch> watches = new List<Watch>();
    }

    private const int JobReminderId = 9101, QueuedReminderId = 9102, ExpressReminderId = 9103;

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
            var notification = new AndroidNotification
            {
                Title = n.title,
                Text = n.text,
                FireTime = fire,
                ShouldAutoCancel = true
            };
            if (n.id != 0) AndroidNotificationCenter.SendNotificationWithExplicitID(notification, ChannelId, n.id);
            else AndroidNotificationCenter.SendNotification(notification, ChannelId);
#endif
        }

        ArmStepWatch();
    }

    // ---------------------------------------------------------------- step watch (native background worker)

    private void ArmStepWatch()
    {
        if (!IsDevice || !StepHistory.IsSupported) return;
        WatchPlan plan = PlanStepWatch();
        if (plan.watches.Count == 0) return;
        CallStepWatch("arm", JsonUtility.ToJson(plan));
    }

    private static void CallStepWatch(string method, string json = null)
    {
#if UNITY_ANDROID
        try
        {
            using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var watch = new AndroidJavaClass("com.ogunworks.walkapp.StepWatch"))
            {
                if (json != null) watch.CallStatic(method, activity, json);
                else watch.CallStatic(method, activity);
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"StepWatch.{method} failed: {e.Message}");
        }
#endif
    }

    /// <summary>
    /// What the background worker should watch for: for each job in order (running, then queued), the real steps
    /// it still needs at the current burn rate and bank, and its deadline. Also used by tests.
    /// </summary>
    public WatchPlan PlanStepWatch()
    {
        long realNow = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        long Real(DateTime gameUtc) => realNow + (long)(gameUtc - GameClock.UtcNow).TotalSeconds;
        var plan = new WatchPlan { start = realNow };

        var jobs = JobManager.instance;
        long bank = CurrencyManager.instance.GetCurrencyAmount(CurrencyType.BankedStep);

        var job = jobs.activeJob;
        if (job != null && job.IsRunning)
            plan.watches.Add(JobWatch(job, jobs.WalkedStepsToFinish(job.stepsLeft, ref bank), Real(job.DeadlineUtc), JobReminderId));

        var queued = jobs.queuedJob;
        if (queued != null && job != null && job.IsRunning)
            plan.watches.Add(JobWatch(queued, jobs.WalkedStepsToFinish(queued.stepsLeft, ref bank), Real(queued.DeadlineUtc), QueuedReminderId));

        var express = ExpressJobManager.instance.Job;
        if (express != null && express.status == ExpressStatus.Active)
        {
            plan.watches.Add(new Watch
            {
                steps = express.StepsLeft,
                deadline = Real(express.DeadlineUtc),
                reminderId = ExpressReminderId,
                doneTitle = "Express delivered!",
                doneText = $"{express.offer.cargoType} made it in time. Open Walkapp to claim {express.offer.reward:N0} coins.",
                missTitle = "Express delivery missed",
                missText = $"{express.offer.cargoType} ran out of time, {{short}} steps short."
            });
        }

        return plan;
    }

    private static Watch JobWatch(ActiveJobSaveData job, long realSteps, long deadline, int reminderId) => new Watch
    {
        steps = realSteps,
        deadline = deadline,
        reminderId = reminderId,
        doneTitle = $"{job.jobData.cargoType} delivered!",
        doneText = $"Your pay is waiting: {job.jobData.reward:N0} coins. Open Walkapp to claim it.",
        missTitle = $"{job.jobData.cargoType} missed its deadline",
        missText = "It ended {short} steps short."
    };

    private void CancelAll()
    {
#if UNITY_ANDROID
        if (!IsDevice) return;
        AndroidNotificationCenter.CancelAllNotifications();
        CallStepWatch("disarm");
#endif
    }

    /// <summary>What would be scheduled if the app went to the background now (also used by tests).</summary>
    public List<Planned> Plan()
    {
        var list = new List<Planned>();
        DateTime now = GameClock.UtcNow;
        void Add(DateTime at, string title, string text, int id = 0)
        {
            if (at > now.AddMinutes(1)) list.Add(new Planned { fireUtc = at, title = title, text = text, id = id });
        }

        // Regular job: a reminder before the deadline, with the steps left when the player last looked.
        var jobs = JobManager.instance;
        var job = jobs.activeJob;
        if (job != null && job.IsRunning)
        {
            Add(job.DeadlineUtc.AddMinutes(-jobReminderMinutes), $"{job.jobData.cargoType} is due in {jobReminderMinutes} min",
                $"{job.stepsLeft:N0} steps to go when you last checked. Open Walkapp to see where you are.", JobReminderId);
        }

        // The queued job's deadline runs from when it was queued; it may or may not have started by then.
        var queued = jobs.queuedJob;
        if (queued != null)
            Add(queued.DeadlineUtc.AddMinutes(-jobReminderMinutes), $"{queued.jobData.cargoType} is due in {jobReminderMinutes} min",
                "Your queued job's deadline is close. Open Walkapp to see where you are.", QueuedReminderId);

        // Express: the offer is about to expire, the job is about to end, or the next offer arrives.
        var express = ExpressJobManager.instance;
        if (ProgressionManager.instance.IsUnlocked(Feature.Express))
        {
            if (express.Job != null && express.Job.status == ExpressStatus.Active)
            {
                Add(express.Job.DeadlineUtc.AddMinutes(-expressReminderMinutes), $"Express: {expressReminderMinutes} minutes left",
                    $"{express.Job.offer.cargoType}: {express.Job.StepsLeft:N0} steps to go when you last checked. Keep walking!", ExpressReminderId);
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
