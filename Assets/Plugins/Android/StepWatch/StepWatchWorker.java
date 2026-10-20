package com.ogunworks.walkapp;

import android.app.AlarmManager;
import android.app.Notification;
import android.app.NotificationChannel;
import android.app.NotificationManager;
import android.app.PendingIntent;
import android.content.Context;
import android.content.Intent;
import android.content.SharedPreferences;
import android.util.Log;

import androidx.work.WorkManager;
import androidx.work.Worker;
import androidx.work.WorkerParameters;

import com.google.android.gms.fitness.FitnessLocal;
import com.google.android.gms.fitness.LocalRecordingClient;
import com.google.android.gms.fitness.data.LocalBucket;
import com.google.android.gms.fitness.data.LocalDataPoint;
import com.google.android.gms.fitness.data.LocalDataSet;
import com.google.android.gms.fitness.data.LocalDataType;
import com.google.android.gms.fitness.data.LocalField;
import com.google.android.gms.fitness.request.LocalDataReadRequest;
import com.google.android.gms.fitness.result.LocalDataReadResponse;
import com.google.android.gms.tasks.Tasks;

import org.json.JSONArray;
import org.json.JSONObject;

import java.util.List;
import java.util.concurrent.TimeUnit;

/**
 * Reads the steps walked since the game was paused and walks through the plan in order:
 * each watch needs "steps" real steps before its "deadline"; when one ends (delivered, or the deadline passed)
 * the next one starts counting from that moment, like the job queue does in the game.
 *
 * Plan JSON (written by NotificationScheduler.cs):
 * { "start": unixSeconds, "channel": "jobs",
 *   "watches": [ { "steps": n, "deadline": unixSeconds, "reminderId": id or 0,
 *                  "doneTitle": "", "doneText": "", "missTitle": "", "missText": "... {short} ..." } ] }
 */
public class StepWatchWorker extends Worker {
    private static final String TAG = StepWatch.TAG;

    public StepWatchWorker(Context context, WorkerParameters params) {
        super(context, params);
    }

    @Override
    public Result doWork() {
        Context context = getApplicationContext();
        try {
            SharedPreferences prefs = context.getSharedPreferences(StepWatch.PREFS, Context.MODE_PRIVATE);
            String planJson = prefs.getString(StepWatch.KEY_PLAN, null);
            if (planJson == null) {
                stop(context);
                return Result.success();
            }

            JSONObject plan = new JSONObject(planJson);
            long start = plan.getLong("start");
            String channel = plan.optString("channel", "jobs");
            JSONArray watches = plan.getJSONArray("watches");
            long now = System.currentTimeMillis() / 1000L;
            if (watches.length() == 0 || now <= start) {
                if (watches.length() == 0) stop(context);
                return Result.success();
            }

            // Step history since the pause, in buckets fine enough to tell when a job's steps were reached.
            long window = now - start;
            int bucketMinutes = window <= 6 * 3600 ? 1 : window <= 48 * 3600 ? 5 : 15;
            LocalRecordingClient client = FitnessLocal.getLocalRecordingClient(context);
            LocalDataReadRequest request = new LocalDataReadRequest.Builder()
                    .aggregate(LocalDataType.TYPE_STEP_COUNT_DELTA)
                    .bucketByTime(bucketMinutes, TimeUnit.MINUTES)
                    .setTimeRange(start, now, TimeUnit.SECONDS)
                    .build();
            LocalDataReadResponse response = Tasks.await(client.readData(request), 25, TimeUnit.SECONDS);
            List<LocalBucket> buckets = response.getBuckets();

            long cursor = start;      // the current watch counts steps walked after this moment
            boolean stillWaiting = false;
            int count = Math.min(watches.length(), StepWatch.MAX_WATCHES);

            for (int i = 0; i < count; i++) {
                JSONObject watch = watches.getJSONObject(i);
                long need = watch.getLong("steps");
                long deadline = watch.getLong("deadline");

                long walked = 0;
                long doneAt = -1;
                if (need <= 0) doneAt = cursor;
                for (int b = 0; b < buckets.size() && doneAt < 0; b++) {
                    LocalBucket bucket = buckets.get(b);
                    long bucketStart = bucket.getStartTime(TimeUnit.SECONDS);
                    long bucketEnd = bucket.getEndTime(TimeUnit.SECONDS);
                    if (bucketEnd <= cursor || bucketStart >= deadline) continue;
                    walked += stepsIn(bucket);
                    if (walked >= need) doneAt = Math.min(bucketEnd, now);
                }

                String key = "notified_" + i;
                if (doneAt >= 0) {
                    if (!prefs.getBoolean(key, false)) {
                        notify(context, channel, i, watch.optString("doneTitle"), watch.optString("doneText"));
                        cancelReminder(context, watch.optInt("reminderId", 0));
                        prefs.edit().putBoolean(key, true).apply();
                    }
                    cursor = doneAt;
                } else if (now >= deadline) {
                    if (!prefs.getBoolean(key, false)) {
                        String text = watch.optString("missText").replace("{short}", String.format("%,d", Math.max(0, need - walked)));
                        notify(context, channel, i, watch.optString("missTitle"), text);
                        prefs.edit().putBoolean(key, true).apply();
                    }
                    cursor = Math.max(cursor, deadline);
                } else {
                    stillWaiting = true; // this one is still on the road; later ones wait behind it
                    break;
                }
            }

            if (!stillWaiting) stop(context);
            return Result.success();
        } catch (Throwable t) {
            Log.e(TAG, "step check failed", t);
            return Result.success(); // try again at the next period
        }
    }

    private static int stepsIn(LocalBucket bucket) {
        int steps = 0;
        for (LocalDataSet dataSet : bucket.getDataSets())
            for (LocalDataPoint point : dataSet.getDataPoints())
                steps += point.getValue(LocalField.FIELD_STEPS).asInt();
        return steps;
    }

    private static void stop(Context context) {
        WorkManager.getInstance(context).cancelUniqueWork(StepWatch.WORK_NAME);
    }

    private static void notify(Context context, String channelId, int index, String title, String text) {
        if (title == null || title.isEmpty()) return;
        NotificationManager manager = (NotificationManager) context.getSystemService(Context.NOTIFICATION_SERVICE);
        if (manager == null) return;

        // The game registers this channel on first launch; make sure it exists anyway.
        if (manager.getNotificationChannel(channelId) == null)
            manager.createNotificationChannel(new NotificationChannel(channelId, "Jobs", NotificationManager.IMPORTANCE_DEFAULT));

        Intent launch = context.getPackageManager().getLaunchIntentForPackage(context.getPackageName());
        PendingIntent open = launch == null ? null : PendingIntent.getActivity(context, StepWatch.NOTIFICATION_ID_BASE + index, launch,
                PendingIntent.FLAG_UPDATE_CURRENT | PendingIntent.FLAG_IMMUTABLE);

        Notification.Builder builder = new Notification.Builder(context, channelId)
                .setSmallIcon(context.getApplicationInfo().icon)
                .setContentTitle(title)
                .setContentText(text)
                .setStyle(new Notification.BigTextStyle().bigText(text))
                .setAutoCancel(true);
        if (open != null) builder.setContentIntent(open);
        manager.notify(StepWatch.NOTIFICATION_ID_BASE + index, builder.build());
    }

    // A delivered job doesn't need its "due in 60 min" reminder any more. That reminder is an alarm scheduled by
    // Unity's Mobile Notifications plugin: cancel it the same way the plugin does.
    private static void cancelReminder(Context context, int reminderId) {
        if (reminderId == 0) return;
        try {
            Class<?> receiver = Class.forName("com.unity.androidnotifications.UnityNotificationManager");
            Intent intent = new Intent(context, receiver);
            PendingIntent broadcast = PendingIntent.getBroadcast(context, reminderId, intent,
                    PendingIntent.FLAG_NO_CREATE | PendingIntent.FLAG_IMMUTABLE);
            if (broadcast == null) return;
            AlarmManager alarms = (AlarmManager) context.getSystemService(Context.ALARM_SERVICE);
            if (alarms != null) alarms.cancel(broadcast);
            broadcast.cancel();
        } catch (Throwable t) {
            Log.w(TAG, "could not cancel reminder " + reminderId, t);
        }
    }
}
