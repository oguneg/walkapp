package com.ogunworks.walkapp;

import android.app.NotificationManager;
import android.content.Context;
import android.util.Log;

import androidx.work.ExistingPeriodicWorkPolicy;
import androidx.work.PeriodicWorkRequest;
import androidx.work.WorkManager;

import java.util.concurrent.TimeUnit;

/**
 * Step-based notifications while the game is closed, without a foreground service or a permanent notification.
 *
 * When the game goes to the background it hands over a plan (arm): the real steps each job still needs and its
 * deadline. A periodic WorkManager job (StepWatchWorker, about every 15 minutes, when Android allows) reads the
 * steps walked since then from the local Recording API and posts "delivered" / "missed" notifications.
 * When the game comes back it takes over again (disarm).
 *
 * The game remains the source of truth: it recomputes everything from the step history on resume. This only
 * decides when to notify.
 */
public final class StepWatch {
    static final String TAG = "StepWatch";
    static final String PREFS = "walkapp_stepwatch";
    static final String KEY_PLAN = "plan";
    static final String WORK_NAME = "walkapp_stepwatch";
    static final int NOTIFICATION_ID_BASE = 7100;
    static final int MAX_WATCHES = 8;

    private StepWatch() {}

    /** Called from Unity (NotificationScheduler) when the app is paused. */
    public static void arm(Context context, String planJson) {
        try {
            Context app = context.getApplicationContext();
            app.getSharedPreferences(PREFS, Context.MODE_PRIVATE).edit().clear().putString(KEY_PLAN, planJson).commit();

            PeriodicWorkRequest request = new PeriodicWorkRequest.Builder(StepWatchWorker.class, 15, TimeUnit.MINUTES)
                    .setInitialDelay(5, TimeUnit.MINUTES)
                    .build();
            WorkManager.getInstance(app).enqueueUniquePeriodicWork(
                    WORK_NAME, ExistingPeriodicWorkPolicy.CANCEL_AND_REENQUEUE, request);
        } catch (Throwable t) {
            Log.e(TAG, "arm failed", t);
        }
    }

    /** Called from Unity when the app comes back: stop watching and clear what was posted. */
    public static void disarm(Context context) {
        try {
            Context app = context.getApplicationContext();
            WorkManager.getInstance(app).cancelUniqueWork(WORK_NAME);
            app.getSharedPreferences(PREFS, Context.MODE_PRIVATE).edit().clear().commit();

            NotificationManager manager = (NotificationManager) app.getSystemService(Context.NOTIFICATION_SERVICE);
            if (manager != null)
                for (int i = 0; i < MAX_WATCHES; i++) manager.cancel(NOTIFICATION_ID_BASE + i);
        } catch (Throwable t) {
            Log.e(TAG, "disarm failed", t);
        }
    }
}
